# FAQ：原理、手动步骤与常见问题

一键安装的用户不用读这篇，直接看 [README.md](./README.md)。这里是改包到底改了什么的解释、不依赖脚本的手动做法、升级流程，以及实际会撞到的问题。

## 一、改包做了什么

Wave Link 3.x 的 MSIX 里，`AppxManifest.xml` 声明 `MinVersion="10.0.22000.0"`（22000 是 Windows 11 首个版本），Windows 10 的 AppX 安装器据此直接拒绝安装。`scripts\patch_manifest.ps1` 做四件事：

1. **解包**：优先用 `makeappx unpack`；找不到 `makeappx.exe` 时退回 .NET 的 `ZipFile::ExtractToDirectory`。MSIX 本身就是一个 zip。
2. **改 Manifest**：把 `MinVersion="10.0.22000.0"` 替换成 `MinVersion="10.0.<MinBuild>.0"`，默认 `MinBuild` 为 19041。同一趟把 `<Identity>` 的 `Publisher` 改成 `CN=WaveLinkPatch`。
3. **删掉 `AppxSignature.p7x`**：manifest 改了，原签名必然失效，留着只会让安装报签名错误。
4. **重打包**：`makeappx pack` 或 `ZipFile::CreateFromDirectory`，默认输出到 `<输入目录>\WaveLink_Win10_patched.msix`。

改完的包没有任何签名。装它有两种方式，取决于系统版本：

- Windows 10 **2004（19041）及以上**有 `Add-AppxPackage -AllowUnsigned`，开发者模式下可以直接装，不需要证书。`scripts\setup_wavelink_win10.ps1` 走这条。
- **1809 / 1909** 没有这个参数。GUI 安装器这时会自己签：生成一张自签证书（FriendlyName 为 `WaveLinkPatch`，密钥用途 DigitalSignature，EKU 是代码签名），用 `certutil -addstore -ent -f Root` 装进 `LocalMachine\Root`，再用 `signtool sign /fd SHA256 /sha1 <指纹>` 签名。`Add-AppxPackage` 只要求签名可信，不要求签发者是 Elgato。

签名工具的来源按顺序找：先看 `C:\Program Files (x86)\Windows Kits\10\bin` 和 `C:\Program Files\Windows Kits\10\bin` 下有没有现成的 `signtool.exe`；没有就下 NuGet 上可再分发的 `Microsoft.Windows.SDK.BuildTools` 包（版本 `10.0.28000.2705`），把其中的 x64 工具解到 `%LOCALAPPDATA%\WaveLinkWin10Setup\signtool\` 并缓存，下次直接复用。目标机器因此不需要装 Windows SDK，这次下载也只发生一次。

`Publisher` 必须和签名主体一致，这就是第 2 步要改 `<Identity>` 的原因。`scripts\patch_manifest.ps1` 只替换主 `<Identity>` 的那一处；如果匹配数不是 1，它会打印一条 warning 然后继续。

## 二、手动重签名路线

系统不支持 `-AllowUnsigned`，或者你想用自己的证书时，用这条：

```bat
:: 1) 改包，得到 input\WaveLink_Win10_patched.msix
PowerShell -ExecutionPolicy Bypass -File scripts\patch_manifest.ps1 -InputMsix input\你的.msix

:: 2) 用你的 .pfx 签名（需要 Windows SDK 的 signtool）
signtool sign /fd SHA256 /f 你的证书.pfx /p 密码 input\WaveLink_Win10_patched.msix

:: 3) 把证书公钥导入受信任根，再安装
Import-Certificate -FilePath 你的证书.cer -CertStoreLocation Cert:\LocalMachine\TrustedRoot
Add-AppxPackage input\WaveLink_Win10_patched.msix
```

第 3 步的写法可参考 `scripts\install.ps1`，那份脚本是当初在原始环境里跑的版本，里面的路径是那台机器的绝对路径，照抄前先改掉。

本仓库不含任何 `.pfx` 私钥。`certs\WaveLinkPatch.cer` 只是当初那台机器上自签证书的公钥，没有对应私钥，签不了别人的 MSIX。

## 三、手动装驱动

1. 以管理员身份打开 CMD：`Win` → 输入 `cmd` → 右键 → 以管理员身份运行。
2. 执行（路径按你实际解压位置改）：

```bat
cd /d "C:\wavelink-win10-driver\driver"
msiexec /i WaveLinkDriver_3.0.0.466_x64.msi /qn /norestart /l*v msi_install.log
```

3. `msi_install.log` 末尾应该是 `Return Value 0`。成功码也可能是 3010，表示需要重启。

**不要用 Git Bash 或 MSYS 启动 msiexec。** 它们会把 `C:\Users\...` 改写成 `/Users/...` 传给 msiexec，服务端报 `Note 1314`，`MainEngineThread returning 2`，退出码 **83**。用 `cmd.exe` 或 PowerShell，路径保持 `C:\...` 形式。

安装器不只以 MSI 的退出码作为判据：`msiexec` 返回 0 或 3010，**或者** `ElgatoVirtUsbAudioEmu`、`ElgatoUsbAudio`、`ElgatoUsbAudioks` 三个服务已经在 SCM 中就位，都算成功。两条都不成立时，才会对 `driver\elgato\` 下每一个 `.inf` 跑 `pnputil /add-driver "<inf>" /install`。全部失败才会报错，并且报的是 `msiexec` 的真实退出码。

## 四、怎么检查和安装新版本

GUI 安装器的 **检查并更新 Wave Link** 按钮做完整流程：

1. 查询 `elgato.zendesk.com` 的 release notes 文章列表，取版本号最高的那篇 Windows 文章；
2. 从文章正文里正则抽出官方 CDN 的 MSIX 直链；
3. 用 `Get-AppxPackage -Name Elgato.WaveLink` 读当前已装版本并比对，已经不低于最新就直接结束；
4. 否则下载到 `input\`（约 180 MB，日志每 5 MB 报一次进度），随后自动发起提权安装。

下载完成后会做一次体积检查：小于 100 MB 视为被拦截，直接报错，不会拿一个坏文件去安装。

手动做等价的事：去 Elgato 官方 release notes 找最新 Windows 版的 MSIX 直链，下载后放进 `input\`，然后点 **一键运行全部**；或者直接跑脚本：

```bat
PowerShell -ExecutionPolicy Bypass -File scripts\setup_wavelink_win10.ps1 -MsixPath input\Elgato.WaveLink_3.3.0.4529_x64.msix
```

自动发版仓库 `wavelink-win10-autorelease` 每天 06:00 UTC 会做同样的事并在有新版时发 Release，标签形如 `wavelink-3.3`。它把已发布版本号写进 `wavelink-app-version.txt`，同版本不会重复发版。

## 五、为什么自动发版一度卡在 3.2.10

检测脚本按文章标题抽版本号，正则写的是 `Elgato Wave Link (\d+\.\d+\.\d+) \(Windows\) Release Notes`，要求版本号必须是三段。

Elgato 把 3.3 那篇文章命名为 "Elgato Wave Link 3.3 (Windows) Release Notes"，版本号只有两段。正则匹配不上，这一篇被直接跳过，脚本于是继续认为 3.2.10 是最新版，自动发版就停在 3.2.10 不再前进。

`scripts/detect-wavelink-msix.ps1` 和安装器里的 `Updater.cs` 都把正则改成了 `\d+(?:\.\d+)*`，版本比较也改成按任意段数逐段比。现在两段、三段、四段的版本号都能被正确取出和比较。

## 六、Wave Link 3.3 改了什么

Windows 版 3.3（3.3.0.4529，发布日 2026-09-16）的改动：

- 支持 Corsair VOXARA USB 电容麦克风，含 Voice Focus 和 RGB 模式；
- 修复了调整音频效果顺序后输入音被掐断、必须禁用效果或重启 Wave Link 才恢复的问题；
- 修复了 Wave XLR Pro 在硬件混音器与软件混音器切换过程中 Wave Link 崩溃的问题。

3.3.0.4529 取代此前打包的 3.2.10.4073。

## 七、应用升级后驱动要不要一起更新

不用。驱动和应用的版本线是分开的，驱动一直是 `WaveLinkDriver_3.0.0.466_x64.msi`，Elgato 没有发新驱动，本项目也没改过它。

升级应用时驱动通常原样保留。如果被清理掉了，重装驱动即可：

```bat
PowerShell -ExecutionPolicy Bypass -File scripts\setup_wavelink_win10.ps1 -SkipApp
```

或者双击 `scripts\reinstall_wavelink_driver.bat`，并注意这个 bat 要求驱动 MSI 与它同目录（它不认 `driver\`）。

从 3.3 的上游改动看，它修的都是应用侧的效果链和崩溃问题，没有涉及驱动，所以升级到 3.3 不需要动驱动。装了 3.3 之后如果音频路由出问题，先按 [README.md](./README.md) 的验证流程查驱动库，而不是先怀疑驱动版本。

## 八、常见问题

**Q：一键脚本一闪而过，好像什么都没做？**
A：在仓库根目录用管理员 PowerShell 手动跑 `PowerShell -ExecutionPolicy Bypass -File scripts\setup_wavelink_win10.ps1`，看完整输出；脚本会把过程写进 `setup_wavelink_win10.log`。部分杀软会拦截开发者模式修改或 AppX 安装，需要临时放行。

**Q：`Add-AppxPackage` 报不支持 `-AllowUnsigned`？**
A：该参数要求 Windows 10 2004（19041）及以上，并且开发者模式已开。1809 / 1909 请用「二、手动重签名路线」，或直接用 GUI 安装器（它自己签名，不依赖这个参数）。

**Q：msiexec 退出码 83？**
A：几乎都是 Git Bash / MSYS 的路径转换导致的。换成原生 `cmd.exe` 或 PowerShell，路径保持 `C:\...`。

**Q：日志里 `Found 0 drivers` 是不是驱动没装上？**
A：不是。这是应用运行时向服务端查更新，Windows 10 拿不到下发结果。同一份日志里有 `Found current driver version: 3.0.0.466`，说明本地驱动已经就位。判断安装成功请看 `pnputil /enum-drivers` 里有没有那三个 INF。

**Q：安装完服务显示 `Stopped` 或干脆查不到，是失败了吗？**
A：不是。内核态驱动服务只在 PnP 枚举时才向 SCM 注册，安装完立刻查询看不到是正常的，它按需启动。真正的判据是驱动库里有没有 `elgatousbaudio.inf`、`elgatousbaudioks.inf`、`elgatovirtusbaudioemu.inf`。

**Q：驱动库和 SCM 服务，为什么以驱动库为准？**
A：驱动库的写入发生在安装过程中，是同步的；SCM 服务注册滞后到设备实际枚举时。拿滞后的信号做判据会误报失败。

**Q：我的系统是 Windows 10 1809 / 1909 / 21H1，能用吗？**
A：驱动层在 1809（17763）及以上都可以，`ElgatoVirtUsbAudioEmu` 这个子驱动可以下探到 1803。应用安装要看系统：2004 及以上走 `-AllowUnsigned`，1809 / 1909 必须重签名。实测只覆盖了 22H2，更早的版本请自行验证。

**Q：Windows 11 上能用吗？**
A：能，但没必要。Windows 11 是官方支持的目标平台，直接装官方包即可，本项目所有改动都是为 Windows 10 准备的。

**Q：能不能只装驱动、不碰应用？**
A：可以。GUI 里勾 **跳过应用安装**，或命令行加 `-SkipApp`。

**Q：运行 exe 需要先装 .NET 吗？**
A：不需要。它是自包含单文件，.NET 8 运行时已经打进去了，双击就能跑。

**Q：只下载了 Release 里的单个 exe，旁边没有 `driver\` 和 `input\`？**
A：把官方 MSIX 放进 exe 同目录的 `input\`。驱动 MSI 不在 `driver\` 时，exe 会尝试调用同目录 `scripts\fetch_driver.bat` 去官方 CDN 下载；如果你只有那一个 exe，没有 `scripts\`，这一步不会触发，需要手动把 `WaveLinkDriver_3.0.0.466_x64.msi` 放到 exe 同目录的 `driver\` 下。最省事的办法是下整个 zip，把 exe 放进去。

**Q：exe 和 PowerShell 脚本版有什么区别？**
A：流程一样（放 MSIX → 改包 → 装应用 → 装驱动 → 验证）。区别在交互和两处实现：exe 有窗口、实时日志、进度条和自动提权，并且用自签证书签名（1809 / 1909 也能用）；脚本版是纯命令行，用 `-AllowUnsigned` 免签名安装，因此需要 2004 及以上。

**Q：exe 为什么有 150 MB？**
A：自包含单文件把整个 .NET 8 运行时打了进去，换来的是目标机不用预装 .NET。想小一点可以自己构建成依赖框架的版本，但目标机就得有 .NET 8 运行时。

**Q：界面语言怎么切？**
A：窗口里的 **语言** 下拉，中文 / English。选择写入 `%LOCALAPPDATA%\WaveLinkWin10Setup\lang.cfg`，下次启动和提权后的子进程都沿用。默认跟随系统显示语言：系统是中文就是中文，否则英文。

**Q：exe 提示"应用安装失败 / 未找到 Elgato.WaveLink"？**
A：先确认 `input\` 里的 MSIX 是完整的官方包（186 MB 左右，用 zip 工具能打开）。如果是 1809 / 1909，改用自签证书路线；GUI 安装器自带这条路线，重跑即可。也可以升级到 Windows 10 2004+ 走免签名路径。

**Q：仓库能不能完全不含二进制？**
A：可以。删掉 `driver/*.msi` 和 `driver/elgato/`，改用 `scripts\fetch_driver.bat` 从官方 CDN 下载。应用 MSIX 本来就不在仓库里。

## 九、装完还要做什么

脚本能验证的都验证了，剩下两件要人来判断：

1. 启动 Wave Link，打开 `设置 → 音频路由 (Output Routing)`，确认 17 个端点都在、都能选。
2. 播一路音频（比如把浏览器声音路由到某个 Wave 输出），确认电平在动、声音能听见。

启动后的前两分钟里，日志可能出现 `hr=0x88890004`。这是 Thesycon pipeline 在重建期间的初始化竞态，设备本身已经 `started`，日志里也有大量 pipeline 正常的事件，稍等就会稳定。如果某一路在实际播放时反复出现这个错误并且没有声音，多半是音频会话或设备被别的程序占用了，从这方面排查。
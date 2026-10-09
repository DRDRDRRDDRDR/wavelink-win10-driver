# Wave Link 3.x on Windows 10：安装器与补丁源码

Elgato 的 Wave Link 3 for Windows 声明的最低系统版本是 Windows 11（MSIX 的 `AppxManifest.xml` 里写死 `MinVersion="10.0.22000.0"`），安装器直接拒绝在 Windows 10 上安装。就算装上了，应用运行时还会向服务端查询驱动，而 Elgato 服务端对 Windows 10 上报的 `osVersion` 返回空设备列表，所以它会一直停在 "Install driver"。

这两个环节都需要单独处理：

1. 把 MSIX 的 `MinVersion` 降到 Windows 10 的构建号，改掉 `<Identity>` 的 Publisher，删掉失效的 `AppxSignature.p7x`，重打包后安装；
2. 绕开服务端下发，用官方驱动 MSI 把音频驱动直接装进系统。

驱动本身没有 Windows 11 锁，这是整件事成立的前提。

本仓库是**补丁仓库**：存放安装器源码、官方驱动 MSI、脚本、报告和文档。自动检测 Elgato 最新版本并打包发版在配套的 [`wavelink-win10-autorelease`](https://github.com/DRDRDRRDDRDR/wavelink-win10-autorelease)。

与 Elgato / Corsair 无关联。专有资产的使用限制见 [NOTICE](./NOTICE)。

## 上游版本

截至 2026-10-09 查询 Elgato Zendesk API（`elgato.zendesk.com`）的结果：Windows 版最新为 **Wave Link 3.3**，发布日 2026-09-16，文章 id `50957117157265`。

- x64 包：`https://edge.elgato.com/egc/windows/ewlw/3.3/Stable/Elgato.WaveLink_3.3.0.4529_x64.msix`
- 大小 195,150,702 字节（186.11 MB），SHA256 `50DDDC68ACBB3C95F193B9A3A56F1EDE1C7D575E91423D5BBA597ECAD88B04C9`
- Wave Link 3 免费，且可用于非 Elgato 麦克风；Wave Link 2 已停止支持（EOL）
- 3.3.0.4529 取代此前打包的 3.2.10.4073

3.3 的改动见 [FAQ.md](./FAQ.md)「Wave Link 3.3 改了什么」。

## 兼容性

驱动 MSI 内嵌 4 个 INF，各自的 OS 装饰决定了下限（INF 文本留档在 `evidence/inf/`）：

| INF | 最低 Windows 10 | Build |
|---|---|---|
| `ElgatoUsbAudio.inf` | 1809 | 17763 |
| `ElgatoUsbAudioks.inf` | 1809 | 17763 |
| `ElgatoVirtUsbAudioEmu.inf` | 1803 | 17134 |
| `ElgatoUsbAudio_dfu.inf` | 无下限 | — |

| 项目 | 状态 |
|---|---|
| Windows 10 22H2（19045） | 实测通过 |
| Windows 10 1809（17763）— 22H2 | 理论支持，未逐版本实测 |
| Windows 10 1709（16299）及更早 | 不支持（低于 17134） |
| Windows 11 | 官方原生支持，无需本项目 |
| 22000+ 硬门槛 | 4 个 INF 中均不存在 |

查看自己的构建号：`Win + R` → 输入 `winver` → 回车。

驱动安装层在 1809 及以上都能走通。应用安装分两条路：

- **2004（19041）及以上**：可以用 `Add-AppxPackage -AllowUnsigned` 免签名安装（PowerShell 脚本走这条）。
- **1809 / 1909**：没有 `-AllowUnsigned` 参数，必须先用受信任证书给改包后的 MSIX 签名再正常安装。GUI 安装器自带这一步（生成自签证书、装进 `LocalMachine\Root`、再用 `signtool` 签名），所以不依赖 `-AllowUnsigned`。

`-MinBuild` 默认 19041，这是写进 Manifest 的系统下限。它和安装方式是两回事：下限决定这个包最低能装到哪个构建号，而 `-AllowUnsigned` 本身要求 2004（19041）及以上，所以把下限设得再低也不会让 1809 具备免签名安装能力。

## 两个仓库怎么分工

| 仓库 | 职责 | 产物 |
|---|---|---|
| [`wavelink-win10-driver`](https://github.com/DRDRDRRDDRDR/wavelink-win10-driver)（本仓库） | 安装器源码、官方驱动、脚本、文档 | `wavelink-patch-bundle.zip`（安装器 + 驱动，不含 MSIX） |
| [`wavelink-win10-autorelease`](https://github.com/DRDRDRRDDRDR/wavelink-win10-autorelease) | 每日检测上游新版本、下载 MSIX、从仓库内 `patch/` 的源码副本构建安装器、拼完整包发版 | `wavelink-win10-complete.zip`（安装器 + 驱动 + 脚本 + MSIX） |

两个仓库的工作流都用 GitHub 内置的 `GITHUB_TOKEN`，不需要个人 PAT。

只想装 Wave Link 的用户去自动发版仓库下载完整包，不需要看这个仓库的构建流程。

## 获取并安装完整包

1. 打开 [`wavelink-win10-autorelease` 的 Releases](https://github.com/DRDRDRRDDRDR/wavelink-win10-autorelease/releases)，下载最新的 `wavelink-win10-complete.zip`（标签形如 `wavelink-3.3`）。
2. 解压到任意目录。会得到 `WaveLinkWin10Setup.exe`、`driver\`、`scripts\`、`input\`（`input\` 内是官方 MSIX）。
3. **开启开发者模式**：`设置` → `更新和安全` → `开发者选项` → 选中 `开发人员模式`。PowerShell 脚本路线靠这一步做免签名安装，不开会直接失败。GUI 安装器自带证书签名，但开着不影响。
4. 右键 `WaveLinkWin10Setup.exe` → **以管理员身份运行**。安装类操作会自己弹 UAC 提权，手动提权只是省一次确认。
5. 先点 **环境检查 (干跑)**，确认构建号、管理员状态、`input\` 里的 MSIX 和 `driver\` 里的 MSI 都能找到。
6. 点 **一键运行全部**。它会依次改包重打包、签名并安装应用、安装驱动 MSI，最后跑一遍验证。
7. 验证结果见 [如何验证](#如何验证)。

如果你手上已有自己的官方 MSIX，把它放进 `input\`（文件名任意，扩展名 `.msix`），在安装器里用 **浏览** 选中它，或直接留空（会自动取 `input\` 里第一个 `.msix`）。

## GUI 安装器

`WaveLinkWin10Setup.exe` 是 C# / .NET 8 WinForms 程序，发布为自包含单文件 win-x64，约 150 MB，目标机不需要预装 .NET 运行时。

界面提供这些操作：

| 控件 | 作用 |
|---|---|
| MSIX 路径 + **浏览** | 指定官方 MSIX；留空则用 `input\` 下的第一个 |
| **跳过应用安装 / 跳过驱动安装** | 只做其中一半 |
| 最低 Win10 版本 (build) | 改包写入的 `MinVersion`，默认 19041 |
| **一键运行全部** | 完整流程：改包 → 装应用 → 装驱动 → 验证 |
| **仅装应用** / **仅装驱动** | 单步执行 |
| **验证** | 只跑验证，不改动系统 |
| **环境检查 (干跑)** | 只读检查，不需要管理员权限 |
| **检查并更新 Wave Link** | 查询 Elgato 最新 MSIX，比对已装版本，下载后自动发起提权安装 |
| 日志框 | 实时滚动输出每一步的命令与结果 |
| 进度条 | 步骤进行中显示跑马灯，完成后显示百分比 |
| **语言** 下拉 | 中文 / English，写入 `%LOCALAPPDATA%\WaveLinkWin10Setup\lang.cfg`，提权子进程沿用 |

安装类操作在非管理员状态下会用 `runas` 重新拉起自身并带上原参数（`--run`、`--msix`、`--minbuild`、`--skipapp`、`--skipdriver`）。`patch_manifest.ps1` 以嵌入资源形式编进 exe，所以没有 `scripts\` 目录也能改包。

界面是 Telegram 风格的浅色 / 深色主题，圆角卡片、扁平强调色按钮、悬停过渡和按下水波纹。

命令行参数（`--check` 走只读模式，把结果同时打印并写入 exe 同目录的 `env_check.txt`）：

```bat
WaveLinkWin10Setup.exe --check
```

## 从源码构建

需要 [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8.0)。在仓库根目录：

```bat
PowerShell -ExecutionPolicy Bypass -File build_exe.ps1
```

`build_exe.ps1` 实际执行的是：

```bat
dotnet publish src/WaveLinkWin10Setup/WaveLinkWin10Setup.csproj -c Release -r win-x64 ^
  --self-contained true -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true ^
  -p:PublishTrimmed=false -p:DebugType=None -o dist
```

产物是 `dist\WaveLinkWin10Setup.exe`。`csproj` 里的 `BundleAssetsAfterPublish` 目标会在 publish 后把 `driver\` 和 `scripts\` 复制进 `dist\`，`build_exe.ps1` 再补一份 `input\`，因此 `dist\` 可以直接分发。

本仓库的工作流 `.github/workflows/build.yml` 在推 `v*` 标签或手动 `workflow_dispatch` 时构建，打成 `wavelink-patch-bundle.zip`（安装器 + `driver\`）发布到本仓库 Release。另有 `.github/workflows/release.yml`，在推 `wavelink-*` 标签或手动触发时只发布安装器，资产名是 `wavelink-win10-installer.zip`。

## 命令行一键脚本

`scripts\setup_wavelink_win10.ps1` 是不带 GUI 的等价流程。它在非管理员时会自行提权，并把完整输出写进仓库根目录的 `setup_wavelink_win10.log`。

```bat
PowerShell -ExecutionPolicy Bypass -File scripts\setup_wavelink_win10.ps1
```

| 参数 | 说明 |
|---|---|
| `-MsixPath <路径>` | 指定官方 MSIX；省略则取 `input\` 下第一个 `.msix` |
| `-SkipApp` | 跳过应用安装，只装或重装驱动 |
| `-SkipDriver` | 跳过驱动安装，只装应用 |
| `-MinBuild 19041` | 改包写入的 Win10 下限，默认 19041 |

只重装驱动：

```bat
PowerShell -ExecutionPolicy Bypass -File scripts\setup_wavelink_win10.ps1 -SkipApp
```

脚本的验证环节读的是服务状态（`ElgatoVirtUsbAudioEmu`、`ElgatoUsbAudio`、`ElgatoUsbAudioks`），服务不是 `Running` 时会以警告结束。内核态驱动服务只在 PnP 枚举时向 SCM 注册，所以刚装完显示 `Stopped` 或查不到是正常的。判断安装成功要看驱动库，见下一节。

只装驱动也可以用 `scripts\reinstall_wavelink_driver.bat`：右键以管理员身份运行，内部执行 `msiexec /i ... /qn /norestart`，日志写到 `scripts\msi_reinstall.log`。注意这个 bat 假设 `WaveLinkDriver_3.0.0.466_x64.msi` 就在它旁边，它不会去 `driver\` 找，所以要么在 `driver\` 目录里运行，要么先把 MSI 复制同目录。

驱动 MSI 丢失时，`scripts\fetch_driver.bat` 会从官方 CDN 重新下载到 `driver\`。

## 如何验证

安装成功与否有两个独立的判据，都直接从系统读，不看日志措辞。

**驱动**：INF 包是否已暂存进 Windows 驱动库。

```bat
pnputil /enum-drivers
```

输出中应能同时找到 `elgatousbaudio.inf`、`elgatousbaudioks.inf`、`elgatovirtusbaudioemu.inf`。驱动库的写入是同步的，不像 SCM 服务注册会滞后，所以这是最可靠的证据。

**应用**：

```powershell
Get-AppxPackage -Name Elgato.WaveLink | Select-Object Name, Version
```

有版本号返回即已安装。

GUI 安装器的 **验证** 按钮跑的就是这两项，并把服务状态一并打印出来；服务是 `Stopped` 或尚未注册只报告，不计为失败。

驱动加载后的最终确认在 Wave Link 里做：打开 `设置 → 音频路由 (Output Routing)`，确认这 17 个端点都在且可选：

`Wave Mic 1–4`、`Game`、`Music`、`Chat Mix`、`Voice Chat`、`Browser`、`SFX`、`System`、`Aux 1/2`、`Aux Mix`、`Personal Mix`、`Stream Mix`、`Recording Mix`，共 17 个

这一步需要你手动操作，脚本无法代劳。实测核验记录见 `reports/wavelink_verify_report.md`，驱动兼容性判定见 `reports/wavelink_driver_install_report.md`。

## 已知限制

- 只有 Windows 10 22H2（19045）经过实测。1809 及以上是依据 INF 的 OS 装饰推导的理论范围，没有逐版本验证过。
- 启动后的前 2 分钟左右，Wave Link 日志里可能出现 `hr=0x88890004`（Thesycon pipeline 初始化竞态），设备本身已 `started`，通常自行恢复。
- 应用日志里的 `Found 0 drivers` 是运行时向服务端查更新的结果，Windows 10 拿不到下发，和本地驱动是否装好无关。同一份日志里会写 `Found current driver version: 3.0.0.466`。
- 用 Git Bash / MSYS 启动 `msiexec` 会把 `C:\...` 路径转成 `/Users/...`，安装以退出码 **83** 失败。请用 `cmd.exe` 或 PowerShell。GUI 安装器不受影响。
- 音频是否真的能出声、推流混音是否正常，需要你在 Wave Link 里实际播放一路音频确认。
- 仓库不含任何 `.pfx` 私钥。`certs\WaveLinkPatch.cer` 是改包时那台机器上的自签证书公钥，对别人的 MSIX 没有用处。

## 目录结构

```
wavelink-win10-driver/
├── README.md / README_EN.md
├── FAQ.md / FAQ_EN.md
├── NOTICE                      合规与版权
├── LICENSE                     MIT（仅覆盖自创脚本与文档）
├── .gitignore
├── build_exe.ps1               构建自包含安装器
├── .github/workflows/
│   ├── build.yml               推 v* 标签 / 手动 → wavelink-patch-bundle.zip
│   └── release.yml             推 wavelink-* 标签 / 手动 → wavelink-win10-installer.zip
├── src/WaveLinkWin10Setup/
│   ├── WaveLinkWin10Setup.csproj
│   ├── App.manifest
│   ├── Program.cs              入口，--check 走无界面模式
│   ├── MainForm.cs             窗口与控件
│   ├── Installer.cs            改包 / 装应用 / 装驱动 / 验证
│   ├── Updater.cs              从 Elgato Zendesk 查最新版并下载 MSIX
│   ├── Lang.cs                 中英文字符串表
│   ├── EnvironmentCheck.cs     --check 的只读检查
│   └── Resources/patch_manifest.ps1   内嵌的改包脚本
├── driver/
│   ├── WaveLinkDriver_3.0.0.466_x64.msi   官方公共 CDN 驱动（2.91 MB）
│   └── elgato/                            4 个已签名驱动包目录，pnputil 兜底用
├── scripts/
│   ├── setup_wavelink_win10.ps1       一键流程（改包 + 装应用 + 装驱动 + 验证）
│   ├── patch_manifest.ps1             改 MinVersion 与 Publisher、删签名、重打包
│   ├── reinstall_wavelink_driver.bat  单独重装驱动
│   ├── fetch_driver.bat               从官方 CDN 重新下载 MSI
│   ├── fetch_app.bat                  MSIX 放置说明
│   ├── install.ps1                    导入证书 + Add-AppxPackage 的入口示例
│   ├── envcheck.ps1                   环境检查
│   ├── patch_manifest.py              早期 Python 版改包脚本（同原理）
│   └── extract_cab.py / find_cab.py / list_streams.py   抽取 MSI 内嵌 CAB
├── reports/
│   ├── wavelink_driver_install_report.md
│   └── wavelink_verify_report.md
├── certs/WaveLinkPatch.cer
├── input/                      放官方 MSIX 的位置（*.msix 被 .gitignore 忽略）
│   ├── README.txt
│   └── .gitkeep
└── evidence/
    ├── inf/                    4 个 Elgato INF（版权归 Elgato）
    ├── a_log.txt               应用运行日志摘录
    ├── duc_win10.json / duc_win11.json   设备更新检查请求与响应
    ├── pnp.txt                 PnP 虚拟设备状态
    ├── envcheck.txt / exitcode.txt / extract_log2.txt
    ├── install_log.txt
    └── msi_install*.log        msiexec 安装日志
```

`scripts\` 下的 `.py` 和 `envcheck.ps1`、`install.ps1` 是研究过程留档，跑通流程不需要它们。

## 合规

Elgato 专有资产有三类，版权归 Elgato：

| 资产 | 在本仓库的形态 |
|---|---|
| `driver/WaveLinkDriver_3.0.0.466_x64.msi` | 官方公共 CDN 直链下载后原样存放，来自 `https://edge.elgato.com/egc/windows/ewlw/drivers/WaveLinkDriver_3.0.0.466_x64.msi` |
| `driver/elgato/` 下的 4 个 INF 与驱动包 | 从官方 MSI 提取 |
| `evidence/inf/*.inf` 的 4 份 INF 文本 | 同上，仅供阅读 |

Wave Link 应用 MSIX 从不进入本仓库：`input/*.msix` 已被 `.gitignore` 忽略，自动发版仓库只在构建时从官方 CDN 拉取一次并打包进 Release 压缩包。详见 [`NOTICE`](./NOTICE)。

想做成完全不含二进制的仓库，删掉 `driver/*.msi` 和 `driver/elgato/`，改用 `scripts\fetch_driver.bat` 下载。

自创的脚本与文档以 MIT 许可发布，见 `LICENSE`。

## 参考资料

- [FAQ.md](./FAQ.md)：绕过原理、手动重签名路线、手动装驱动、常见问题
- `reports/wavelink_driver_install_report.md`：驱动安装与 INF 兼容性判定
- `reports/wavelink_verify_report.md`：启动实测核验（17/17 端点）
- 官方驱动 CDN：`https://edge.elgato.com/egc/windows/ewlw/drivers/WaveLinkDriver_3.0.0.466_x64.msi`
- 英文版：[README_EN.md](./README_EN.md)
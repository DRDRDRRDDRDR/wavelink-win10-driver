# FAQ 与手动原理（中文）

> 本页收录：绕过 Win11 门槛的原理、手动重签名路线、手动安装驱动，以及常见问题。
> 一键用户无需阅读，直接看 [README.md](./README.md)。

## 一、绕过 MSIX Win11 门槛的原理（手动理解用）

Wave Link 3.x 的 MSIX 里 `AppxManifest.xml` 写着 `MinVersion="10.0.22000.0"`（22000 = Win11 首发版本），
Windows 10 的 AppX 安装器因此直接拒绝。要装上，需要做四件事：

1. **解包 MSIX**（makeappx unpack 或任意 zip 工具）→ 得到 `AppxManifest.xml`；
2. **改 Manifest**：把 `MinVersion="10.0.22000.0"` 降到 `10.0.19041.0`（Win10 2004），
   并把 `<Identity>` 的 `Publisher` 改成自签主体 `CN=WaveLinkPatch`（与签名一致即可，此处仅为占位）；
3. **删掉旧的 `AppxSignature.p7x`**（manifest 已变，原签名必然失效）；
4. **重打包 + 安装**：在**开发者模式**下用 `Add-AppxPackage -AllowUnsigned` 免签名安装
   （无需 Elgato 私钥，最通用）。

脚本 `scripts/patch_manifest.ps1` 正是自动化上述"解包 → 改 → 重打包"部分；
`scripts/setup_wavelink_win10.ps1` 在此基础上再自动开启开发者模式并完成安装。

## 二、手动重签名路线（仅 1809 / 1909，或你想用证书）

若你的系统不支持 `-AllowUnsigned`（如 1809/1909），需对改包后的 MSIX 用受信任证书重签名：

```bat
:: 1) 改包（得到 input\WaveLink_Win10_patched.msix）
PowerShell -ExecutionPolicy Bypass -File scripts\patch_manifest.ps1 -InputMsix input\你的.msix

:: 2) 用你的 .pfx 重签名（需 Windows SDK 的 signtool）
signtool sign /fd SHA256 /a /f 你的证书.pfx /p 密码 input\WaveLink_Win10_patched.msix

:: 3) 导入证书到受信任根（install.ps1 已示范），再安装
Add-AppxPackage input\WaveLink_Win10_patched.msix
```

> 注：本仓库**不含**任何 .pfx 私钥；`certs/WaveLinkPatch.cer` 仅为当初改包用的自签证书公钥示例，
> 没有对应私钥无法对他人的 MSIX 重签名。

## 三、手动安装驱动（复制粘贴）

若你想手动执行驱动安装，而非用 `reinstall_wavelink_driver.bat` 一键：

1. 以**管理员**打开 CMD：`Win` → 输入 `cmd` → 右键 → 以管理员身份运行。
2. 逐行执行（路径改成你实际解压位置）：
```bat
cd /d "C:\wavelink-win10-driver\driver"
msiexec /i WaveLinkDriver_3.0.0.466_x64.msi /qn /norestart /l*v msi_install.log
```
3. 查看 `msi_install.log` 末尾应有 `Return Value 0`。

> ⚠️ **关键坑（必读）**：务必用 **Windows 原生宿主（cmd.exe / PowerShell）** 启动 `msiexec`。
> 在 **Git Bash / MSYS** 下，它会把 `C:\Users\...` 转成 `/Users\...` 传给 msiexec，导致服务端报 `Note 1314`、退出码 **83** 失败。若你用 Git Bash，请改用上面的 CMD 方式或 `scripts/install.ps1`。

## 四、常见问题 FAQ

**Q1：一键脚本一闪而过 / 没反应？**
A：请在仓库根目录以管理员打开 PowerShell 手动执行 `PowerShell -ExecutionPolicy Bypass -File scripts\setup_wavelink_win10.ps1`，看完整输出；或部分杀软会拦截开发者模式/AppX 安装，请临时放行。

**Q2：`Add-AppxPackage` 报"不支持 -AllowUnsigned"？**
A：该参数需要 Windows 10 **2004（19041）及以上**的开发者模式。若你是 1809/1909，请改用「二、手动重签名路线」（用受信任 .pfx 签名后安装）。

**Q3：msiexec 退出码 83？**
A：几乎都是 Git Bash/MSYS 路径转换导致。改用原生 CMD 或 `install.ps1`，路径保持 `C:\...` 形式。详见「三、手动安装驱动」的警告。

**Q4：日志里 `Found 0 drivers` 是不是没装上？**
A：不是。这是运行时向服务端查更新（Win10 服务端不下发），非本地缺失。同一日志会显示 `Found current driver version: 3.0.0.466`，证明本地驱动已就位。

**Q5：能不能完全不含二进制上传？**
A：可以。删除 `driver/*.msi`，改用 `fetch_driver.bat` 下载；应用 MSIX 本就不在仓库内（放 `input/` 且被 `.gitignore` 忽略）。

**Q6：我的 Windows 10 是 1809 / 1909 / 21H1 等更老版本，能用吗？**
A：驱动安装层**理论支持 1809（17763）及以上全系**（VirtUsbAudioEmu 子驱动可下至 1803）。应用自动安装路径需 2004+（因 `-AllowUnsigned`）；1809/1909 见「二、手动重签名路线」。仅 22H2 实测通过，更老版本请自行验证并反馈。

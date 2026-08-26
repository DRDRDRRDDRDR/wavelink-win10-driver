# Wave Link 3.x 在 Windows 10 上的端到端安装（研究 / 复现包）

> ⚠️ **免责声明**：本项目与 Elgato / Corsair 无任何关联，仅用于技术研究、兼容性验证与本地复现。
> 所有 Elgato 专有资产（驱动 MSI、INF、MSIX 安装包）的版权归 Elgato 所有，使用须遵守其许可协议。
> 不建议用于商业用途或规避正常的产品授权机制。详见 [`NOTICE`](./NOTICE)。
> 英文说明见 [README_EN.md](./README_EN.md)。

---

## 项目简介

Elgato Wave Link 3.x 官方仅支持 Windows 11。本仓库提供在 **Windows 10** 上从「应用装不上」到「驱动就绪」的**端到端**流程产物与一键脚本：

1. **第 0 步（应用本体）**：把官方 MSIX 的 `MinVersion=22000`（Win11 门槛）改到 Win10，重打包后在开发者模式下免签名安装；
2. **第 1 步（驱动）**：绕过「服务端不下发驱动」限制，用官方驱动 MSI 把音频驱动强制装好并验证。

**不含任何 Elgato 专有应用二进制**（MSIX 由你本地提供，见 `input/`），仅保留研究产物、脚本与官方公开 CDN 驱动（约 3 MB）。

## 背景

Elgato Wave Link 3.x 官方仅支持 Windows 11（MSIX 包 `MinVersion=22000`）。
在 **Windows 10 22H2（19045）** 上运行时，应用卡在 "Install driver" —— 根因是
Wave Link 运行时向服务端 `device-update-check.php` 请求驱动，服务端对
`osVersion=10.0.19045.0` 返回空列表（`appDevices:[]`），不下发音频驱动。

经逆向与实测确认：**系统级封锁只存在于两层（MSIX 版本门槛 + 服务端不下发驱动），
驱动二进制本身对 Windows 10 开放。** 本仓库提供绕过封锁、在 Win10 上装好应用与驱动并验证
的完整流程产物与一键脚本。

## 兼容性范围

基于驱动 MSI 内嵌 4 个 INF 的 OS 版本装饰分析（文件见 `evidence/inf/`）：

| INF | 最低 Windows 10 版本 | Build |
|---|---|---|
| ElgatoUsbAudio.inf | 1809 | 17763 |
| ElgatoUsbAudioks.inf | 1809 | 17763 |
| ElgatoVirtUsbAudioEmu.inf | 1803 | 17134 |
| ElgatoUsbAudio_dfu.inf | 无下限（适用于全部 NT 版本） | — |

关键结论：

- **已实测通过**：Windows 10 22H2（19045）。
- **理论支持（驱动层）**：Windows 10 **1809（17763）及以上全系列**（1809 / 1903 / 1909 / 2004 / 20H1 / 20H2 / 21H1 / 21H2 / 22H2）。其中主驱动（UsbAudio、UsbAudioks）要求 1809+；VirtUsbAudioEmu 子驱动可下探至 1803。
- **无 Win11 硬锁**：4 个 INF 的装饰中**均无 22000+（Win11）专属门槛**，对 Windows 10 全系开放。
- **不支持**：Windows 10 1709（16299）及更早版本（低于 17134）。
- **应用自动安装的前提**：第 0 步的"免签名安装"依赖 Windows 10 **2004（19041）及以上**的开发者模式 `-AllowUnsigned` 能力；若你在 **1809 / 1909** 上安装应用，需自行用受信任证书（.pfx）对改包后的 MSIX 重签名（见「第 0 步」手动说明）。**驱动 MSI 本身在 1809+ 均支持**。

> 说明：1809+ 为基于 INF OS 装饰的**理论支持**，目前仅 22H2 经过实测。若你在其他 Win10 版本安装成功，欢迎在仓库提交反馈以扩展实测矩阵。

## 第 0 步：绕过 MSIX 的 Win11 门槛（让应用本体装到 Windows 10）

> 这一步解决"应用装不上"。本仓库提供 `scripts/setup_wavelink_win10.ps1` 一键完成本步 + 第 1 步。
> 下面先讲清楚原理，再看一键命令。

### 0.1 原理（手动理解用）

Wave Link 3.x 的 MSIX 里 `AppxManifest.xml` 写着 `MinVersion="10.0.22000.0"`（22000 = Win11 首发版本），
Windows 10 的 AppX 安装器因此直接拒绝。要装上，需要做四件事：

1. **解包 MSIX**（makeappx unpack 或任意 zip 工具）→ 得到 `AppxManifest.xml`；
2. **改 Manifest**：把 `MinVersion="10.0.22000.0"` 降到 `10.0.19041.0`（Win10 2004），
   并把 `<Identity>` 的 `Publisher` 改成自签主体 `CN=WaveLinkPatch`（与签名一致即可，此处仅为占位）；
3. **删掉旧的 `AppxSignature.p7x`**（manifest 已变，原签名必然失效）；
4. **重打包 + 安装**：在**开发者模式**下用 `Add-AppxPackage -AllowUnsigned` 免签名安装
   （无需 Elgato 私钥，最通用）。

脚本 `scripts/patch_manifest.ps1` 正是自动化上述 1–4 的"解包→改→重打包"部分。

### 0.2 一键命令（推荐）

先把官方 MSIX 放进 `input/` 目录，再以管理员运行：

```bat
:: 1) 把官方 Wave Link 3.x 的 MSIX 放到 input\ 下（文件名任意，扩展名 .msix）
::    例如 input\Elgato.WaveLink_3.2.10.4073_x64_Win10.msix

:: 2) 一键完成：开发者模式 + 改包安装应用 + 安装驱动 + 验证
PowerShell -ExecutionPolicy Bypass -File scripts\setup_wavelink_win10.ps1
```

脚本会自动：检测管理员权限（不够会弹 UAC 自提权）→ 开启开发者模式 → 改包并重打包 MSIX →
`Add-AppxPackage -AllowUnsigned` 安装应用 → 安装官方驱动 MSI → 校验三个核心服务是否 Running。
全程日志写入 `setup_wavelink_win10.log`。

可选参数：

| 参数 | 说明 |
|---|---|
| `-MsixPath <路径>` | 直接指定官方 MSIX；省略则自动用 `input\` 下第一个 `.msix` |
| `-SkipApp` | 跳过应用安装（应用已装好，只装/重装驱动） |
| `-SkipDriver` | 跳过驱动安装（只装应用） |
| `-MinBuild 19041` | 改包后的目标 Win10 下限（默认 19041 = 2004） |

例：只重装驱动（应用已装好）
```bat
PowerShell -ExecutionPolicy Bypass -File scripts\setup_wavelink_win10.ps1 -SkipApp
```

### 0.3 手动重签名路线（仅 1809 / 1909，或你想用证书）

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

## 流程总览

| 步骤 | 内容 | 产物 / 脚本 |
|---|---|---|
| 0. 应用绕过 | 改 MSIX 的 MinVersion + 开发者模式免签名安装 | `scripts/setup_wavelink_win10.ps1` → `scripts/patch_manifest.ps1` |
| 1. 定位驱动源 | 逆向 `Elgato.BaseClasses.Core.dll` 找到官方端点，伪装 Win11 参数抓包 | `driver/WaveLinkDriver_3.0.0.466_x64.msi` |
| 2. 兼容性判定 | 读取 MSI 内嵌 CAB 的 4 个 INF，确认无 Win11 硬锁 | `evidence/inf/*.inf` |
| 3. 安装驱动 | `msiexec /i` 安装（关键：用 Windows 原生宿主，避免 MSYS 路径转换坑） | 退出码 0，无需重启 |
| 4. 核验 | 启动 Wave Link 3.x，抓日志确认 17/17 虚拟路由端点就绪 | `reports/wavelink_verify_report.md` |

核心结论：驱动二进制对 Win10 开放（`LaunchConditions` 通过，4 个 INF 均无 Win11 装饰），
原"卡在 Install driver"的根因（服务端不下发）已被绕过。

## 目录结构

```
wavelink_win10_driver/
├── README.md                      # 本文件（中文）
├── README_EN.md                   # 英文版
├── NOTICE                         # 合规 / 版权说明
├── LICENSE                        # MIT（仅覆盖自创脚本与文档）
├── .gitignore
├── driver/
│   └── WaveLinkDriver_3.0.0.466_x64.msi   # 官方公共 CDN 驱动（约 3MB）
├── scripts/
│   ├── setup_wavelink_win10.ps1          # ★ 一键端到端：改包装应用 + 装驱动 + 验证
│   ├── patch_manifest.ps1                # ★ 改 MSIX AppxManifest MinVersion 并重打包（绕过 Win11 门槛）
│   ├── reinstall_wavelink_driver.bat     # 单独一键重装驱动（管理员提权 + msiexec /qn）
│   ├── fetch_driver.bat                  # 从官方 CDN 重新下载 MSI（替代二进制）
│   ├── fetch_app.bat                     # MSIX 获取说明（应用本体本仓库不托管）
│   ├── extract_cab.py / find_cab.py / list_streams.py  # 研究用：抽取 MSI 内嵌 CAB
│   ├── patch_manifest.py                 # 研究用：早期 Python 版改包脚本（同原理）
│   ├── envcheck.ps1                      # 研究用：环境检查（管理员 / MSI 路径）
│   └── install.ps1                       # 研究用：证书导入 + Add-AppxPackage 安装入口
├── reports/
│   ├── wavelink_driver_install_report.md  # 安装与 INF 兼容性报告
│   └── wavelink_verify_report.md          # 启动实测核验报告（17/17 端点）
├── certs/
│   └── WaveLinkPatch.cer                  # 改包用的自签证书公钥示例（仅示意，无私钥）
├── input/                                # ★ 你放官方 MSIX 的地方（*.msix 已被 .gitignore 忽略）
│   ├── README.txt
│   └── .gitkeep
└── evidence/
    ├── inf/                               # 4 个 Elgato INF（Win10 兼容证明，版权归 Elgato）
    │   ├── ElgatoUsbAudio.inf
    │   ├── ElgatoUsbAudio_dfu.inf
    │   ├── ElgatoUsbAudioks.inf
    │   └── ElgatoVirtUsbAudioEmu.inf
    ├── duc_win10.json / duc_win11.json    # 设备更新检查请求/响应（Win10 vs Win11）
    ├── pnp.txt                            # PnP 虚拟设备状态
    ├── a_log.txt                          # 应用运行日志摘录
    ├── msi_install*.log                   # msiexec 安装日志（含成功退出码 0）
    ├── extract_log2.txt / envcheck.txt / install_log.txt / exitcode.txt
```

> ★ = 端到端自动化核心脚本。其余 `.py` / 旧 `.ps1` 为研究过程留档，可单独阅读但非必需。

## 使用教程（保姆级）

### 前置条件

- **Windows 10 2004（19041）或更高**（已实测 22H2 / 19045）。若你在 **1809/1909**，应用自动安装需用受信任证书重签名（见 0.3）；驱动 MSI 本身在 1809+ 均支持。查看方法：按 `Win + R` → 输入 `winver` → 回车。
- **官方 Wave Link 3.x 的 MSIX 安装包**（你自己提供，放入 `input/`）。
- 管理员权限（安装应用与内核驱动必须）。
- 本仓库已下载到本地。

### 步骤 1：获取本仓库

**方式 A — 下载 ZIP（最简单）**
1. 在 GitHub 页面点击绿色的 **Code → Download ZIP**。
2. 解压到任意目录，例如 `C:\wavelink-win10-driver\`。

**方式 B — 用 git 克隆**
```bat
git clone https://github.com/<你的用户名>/wavelink-win10-driver.git
cd wavelink-win10-driver
```

### 步骤 2：放好官方 MSIX

把官方 Wave Link 3.x 的 MSIX 放到仓库的 `input\` 目录，例如：
`input\Elgato.WaveLink_3.2.10.4073_x64_Win10.msix`。
（获取途径：Elgato 官方下载器导出 / 应用商店导出 / 你的备份。本仓库不托管此文件。）

### 步骤 3：一键端到端安装（推荐）

以管理员打开 PowerShell（或直接在资源管理器地址栏输入 `powershell`），
进入仓库根目录，执行：

```bat
PowerShell -ExecutionPolicy Bypass -File scripts\setup_wavelink_win10.ps1
```

脚本会自动：提权（若尚未管理员）→ 开开发者模式 → 改包并重打包 MSIX →
免签名安装应用 → 安装官方驱动 MSI → 校验 `ElgatoVirtUsbAudioEmu / ElgatoUsbAudio / ElgatoUsbAudioks`
三个服务是否为 Running。日志见 `setup_wavelink_win10.log`。

只重装驱动（应用已装好）：追加 `-SkipApp`。
仅重装应用（驱动已装好）：追加 `-SkipDriver`。

### 步骤 4（备选）：仅手动安装驱动

若你只想单独装/重装驱动：

#### 方式 A：一键
1. 进入 `scripts\`。
2. **右键** `reinstall_wavelink_driver.bat` → **以管理员身份运行**。
3. 静默安装（`/qn`），约几十秒，无需重启。

#### 方式 B：手动（复制粘贴）
1. 以**管理员**打开 CMD：`Win` → 输入 `cmd` → 右键 → 以管理员身份运行。
2. 逐行执行（路径改成你实际解压位置）：
```bat
cd /d "C:\wavelink-win10-driver\driver"
msiexec /i WaveLinkDriver_3.0.0.466_x64.msi /qn /norestart /l*v msi_install.log
```
3. 查看 `msi_install.log` 末尾应有 `Return Value 0`。

> ⚠️ **关键坑（必读）**：务必用 **Windows 原生宿主（cmd.exe / PowerShell）** 启动 `msiexec`。
> 在 **Git Bash / MSYS** 下，它会把 `C:\Users\...` 转成 `/Users\...` 传给 msiexec，导致服务端报 `Note 1314`、退出码 **83** 失败。若你用 Git Bash，请改用上面的 CMD 方式或 `scripts/install.ps1`。

### 步骤 5：验证

1. 启动 Wave Link 3.x（若用一键脚本，应用已一并装好）。
2. 打开 **Wave Link 设置 → 音频路由（Output Routing）**，确认以下端点全部可见且可选：
   `Wave Mic 1–4、Game、Music、Chat Mix、Voice Chat、Browser、SFX、System、Aux 1/2、Aux Mix、Personal Mix、Stream Mix、Recording Mix`（共 17 个）。
3. 若全部出现，驱动即安装成功。详细核验证据见 `reports/wavelink_verify_report.md`。

> 启动初期约 2 分钟内可能出现 `hr=0x88890004`（pipeline 初始化抖动），属正常现象，稍候即稳。

### 步骤 6：缺驱动包 / 缺应用包时重新获取

- **驱动 MSI 丢失**：进入 `scripts\`，双击 `fetch_driver.bat`，从官方公共 CDN 重新下载到 `driver\`。
- **应用 MSIX 获取说明**：进入 `scripts\`，双击 `fetch_app.bat` 查看放置指引；把官方 MSIX 放进 `input\` 后重跑一键脚本。

### 常见问题 FAQ

**Q1：一键脚本一闪而过 / 没反应？**
A：请在仓库根目录以管理员打开 PowerShell 手动执行 `PowerShell -ExecutionPolicy Bypass -File scripts\setup_wavelink_win10.ps1`，看完整输出；或部分杀软会拦截开发者模式/AppX 安装，请临时放行。

**Q2：`Add-AppxPackage` 报"不支持 -AllowUnsigned"？**
A：该参数需要 Windows 10 **2004（19041）及以上**的开发者模式。若你是 1809/1909，请改用「0.3 手动重签名路线」（用受信任 .pfx 签名后安装）。

**Q3：msiexec 退出码 83？**
A：几乎都是 Git Bash/MSYS 路径转换导致。改用原生 CMD 或 `install.ps1`，路径保持 `C:\...` 形式。详见步骤 4 的警告。

**Q4：日志里 `Found 0 drivers` 是不是没装上？**
A：不是。这是运行时向服务端查更新（Win10 服务端不下发），非本地缺失。同一日志会显示 `Found current driver version: 3.0.0.466`，证明本地驱动已就位。

**Q5：能不能完全不含二进制上传？**
A：可以。删除 `driver/*.msi`，改用 `fetch_driver.bat` 下载；应用 MSIX 本就不在仓库内（放 `input/` 且被 `.gitignore` 忽略）。

**Q6：我的 Windows 10 是 1809 / 1909 / 21H1 等更老版本，能用吗？**
A：驱动安装层**理论支持 1809（17763）及以上全系**（VirtUsbAudioEmu 子驱动可下至 1803）。应用自动安装路径需 2004+（因 `-AllowUnsigned`）；1809/1909 见 0.3 手动签名。仅 22H2 实测通过，更老版本请自行验证并反馈。

## 合规与排除项

本仓库**刻意排除**以下 Elgato 专有 / 大体积内容，仅保留研究产物与官方公开驱动：

| 排除项 | 体量 | 原因 |
|---|---|---|
| 两个 MSIX 应用包 | ~191MB ×2 | Elgato 专有应用，版权 / 体积 |
| `src\` 解包应用 DLL | ~390MB | Elgato 专有应用代码 |
| 驱动二进制 `.sys`/`.dll`/`.cat` | — | 由 MSI 自身提供，单独散布有版权风险 |
| `win11_update.xml` | 52MB | 服务端响应缓存 |

**纳入**：报告、脚本、证据日志、官方公开 CDN 驱动 MSI（3MB）、自签证书、**4 个 INF 文本**。
应用 MSIX 由使用者本地提供（放 `input/`，已被 `.gitignore` 忽略），不进入仓库。

> 若你希望仓库完全不含任何二进制，可删除 `driver/*.msi` 并改用 `scripts/fetch_driver.bat` 下载。

## 已知限制

- 第 0 步的"免签名应用安装"依赖 Windows 10 **2004（19041）及以上**的开发者模式 `-AllowUnsigned`；1809/1909 需用受信任证书重签名（本仓库不含 .pfx）。
- 启动初期约 2 分钟内可能出现 `hr=0x88890004`（Thesycon pipeline 初始化抖动），非致命。
- 日志中 `Found 0 drivers` 是运行时向服务端查更新（Win10 服务端不下发），非本地缺失——同一日志明确 `Found current driver version: 3.0.0.466`。
- 如需 GUI 交互级最终验证（播放音频确认电平走动），需你侧手动操作。
- 兼容性仅 Windows 10 22H2 经过实测；1809+ 为基于 INF 的理论支持，未经逐版本实测。

## 参考资料

- 安装报告：`reports/wavelink_driver_install_report.md`
- 核验报告：`reports/wavelink_verify_report.md`
- 官方驱动 CDN：`https://edge.elgato.com/egc/windows/ewlw/drivers/WaveLinkDriver_3.0.0.466_x64.msi`

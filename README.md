# Wave Link 3.x 在 Windows 10 上的端到端安装（研究 / 复现包）

> ⚠️ **免责声明**：本项目与 Elgato / Corsair 无任何关联，仅用于技术研究、兼容性验证与本地复现。
> 所有 Elgato 专有资产（驱动 MSI、INF、MSIX 安装包）的版权归 Elgato 所有，使用须遵守其许可协议。
> 不建议用于商业用途或规避正常的产品授权机制。详见 [`NOTICE`](./NOTICE)。
> 英文说明见 [README_EN.md](./README_EN.md)；原理与手动步骤见 [FAQ.md](./FAQ.md)。

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
- **应用自动安装的前提**：第 0 步的"免签名安装"依赖 Windows 10 **2004（19041）及以上**的开发者模式 `-AllowUnsigned` 能力；若你在 **1809 / 1909** 上安装应用，需自行用受信任证书（.pfx）对改包后的 MSIX 重签名（见 [FAQ.md](./FAQ.md)「手动重签名路线」）。**驱动 MSI 本身在 1809+ 均支持**。

> 说明：1809+ 为基于 INF OS 装饰的**理论支持**，目前仅 22H2 经过实测。若你在其他 Win10 版本安装成功，欢迎在仓库提交反馈以扩展实测矩阵。

## 使用教程（保姆级）

> 本教程以「一键脚本」为主线，一条命令同时完成 *绕过 Win11 门槛装应用 + 装驱动 + 验证*。
> 想了解背后的原理或需要手动方式，见 [FAQ.md](./FAQ.md)。

## 图形界面安装器（WaveLinkWin10Setup.exe）

若你不想敲命令行，本仓库同时提供**原生 Windows GUI 安装器**（C#/.NET 8 编译的单个 exe），把"放 MSIX → 改包 → 装应用 → 装驱动 → 验证"全部收进一个窗口，实时显示日志。它是脚本版（`scripts/setup_wavelink_win10.ps1`）的同功能封装，二者任选其一。

### 获取 exe
- **GitHub Releases**：在仓库 Releases 页面下载 `WaveLinkWin10Setup.exe`（self-contained 单文件，约 60–80MB，无需预装 .NET 运行时）。
- 或自行构建（见下方「从源码构建」）。

### 使用
1. 把官方 MSIX 放进与 exe 同目录下的 `input\`。
2. 双击 `WaveLinkWin10Setup.exe`。
3. 窗口内操作：
   - 「浏览」选 MSIX（留空则自动用 `input\` 下第一个）；
   - 按需勾选「跳过应用安装 / 跳过驱动安装」；
   - 点 **一键运行全部**（或仅装应用 / 仅装驱动 / 验证 / 环境检查）。
4. 安装类操作会**自动请求管理员提权（UAC）**，确认即可。
5. 日志框实时滚动；结束三个 Elgato 服务显示 `Running` 即成功。

> exe 已内嵌改包脚本（`patch_manifest.ps1`），运行时只需旁挂 `driver\`（官方 MSI）与 `input\`（你的 MSIX）。`dist/` 即为完整分发包。

### 从源码构建
需要本机安装 [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8.0)。在仓库根目录执行：
```bat
PowerShell -ExecutionPolicy Bypass -File build_exe.ps1
```
产物：`dist/WaveLinkWin10Setup.exe`（脚本会自动把 `driver/` 与 `input/` 复制到 `dist/`，可直接分发）。

### 前置条件

- **Windows 10 2004（19041）或更高**（已实测 22H2 / 19045）。若你在 **1809/1909**，应用自动安装需用受信任证书重签名（见 FAQ「手动重签名路线」）；驱动 MSI 本身在 1809+ 均支持。查看方法：按 `Win + R` → 输入 `winver` → 回车。
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

> 想了解脚本如何「绕过 Win11 门槛 / 为什么免签名能装」，见 [FAQ.md](./FAQ.md)「绕过 MSIX Win11 门槛的原理」。

### 步骤 4：只装 / 重装驱动（应用已装好）

进入 `scripts\`，**右键** `reinstall_wavelink_driver.bat` → **以管理员身份运行**。
静默安装（`/qn`），约几十秒，无需重启。

> 手动复制粘贴方式见 [FAQ.md](./FAQ.md)「手动安装驱动」。

### 步骤 5：验证

1. 启动 Wave Link 3.x（若用一键脚本，应用已一并装好）。
2. 打开 **Wave Link 设置 → 音频路由（Output Routing）**，确认以下端点全部可见且可选：
   `Wave Mic 1–4、Game、Music、Chat Mix、Voice Chat、Browser、SFX、System、Aux 1/2、Aux Mix、Personal Mix、Stream Mix、Recording Mix`（共 17 个）。
3. 若全部出现，驱动即安装成功。详细核验证据见 `reports/wavelink_verify_report.md`。

> 启动初期约 2 分钟内可能出现 `hr=0x88890004`（pipeline 初始化抖动），属正常现象，稍候即稳。

### 步骤 6：缺驱动包 / 缺应用包时重新获取

- **驱动 MSI 丢失**：进入 `scripts\`，双击 `fetch_driver.bat`，从官方公共 CDN 重新下载到 `driver\`。
- **应用 MSIX 获取说明**：进入 `scripts\`，双击 `fetch_app.bat` 查看放置指引；把官方 MSIX 放进 `input\` 后重跑一键脚本。

## 流程总览（研究产物如何产生）

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
├── README.md                      # 本文件（中文，保姆级教程）
├── README_EN.md                   # 英文版
├── FAQ.md                         # 原理 / 手动步骤 / 常见问题（中文）
├── FAQ_EN.md                      # FAQ 英文版
├── NOTICE                         # 合规 / 版权说明
├── LICENSE                        # MIT（仅覆盖自创脚本与文档）
├── .gitignore
├── build_exe.ps1                  # 构建 GUI 安装器（dotnet publish self-contained）
├── src/WaveLinkWin10Setup/        # GUI 安装器源码（C#/.NET 8 WinForms，patch_manifest.ps1 内嵌为资源）
└── dist/                          # ★ 构建产物（已被 .gitignore 忽略，通过 Releases 分发）
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
- 常见问题与手动步骤：[FAQ.md](./FAQ.md)
- 官方驱动 CDN：`https://edge.elgato.com/egc/windows/ewlw/drivers/WaveLinkDriver_3.0.0.466_x64.msi`

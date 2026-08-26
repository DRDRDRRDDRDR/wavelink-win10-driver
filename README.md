# Wave Link 3.x 驱动强制安装于 Windows 10（研究 / 复现包）

> ⚠️ **免责声明**：本项目与 Elgato / Corsair 无任何关联，仅用于技术研究、兼容性验证与本地复现。
> 所有 Elgato 专有资产（驱动 MSI、INF）的版权归 Elgato 所有，使用须遵守其许可协议。
> 不建议用于商业用途或规避正常的产品授权机制。详见 [`NOTICE`](./NOTICE)。
> 英文说明见 [README_EN.md](./README_EN.md)。

---

## 项目简介

Elgato Wave Link 3.x 官方仅支持 Windows 11。本仓库提供在 **Windows 10** 上绕过「服务端不下发驱动」限制、把官方音频驱动装好并验证可用的完整流程产物——含逆向取证、兼容性判定、安装日志与一键重装脚本。**不含任何 Elgato 专有应用二进制**，仅保留研究产物与官方公开 CDN 驱动（约 3 MB）。

## 背景

Elgato Wave Link 3.x 官方仅支持 Windows 11（MSIX 包 `MinVersion=22000`）。
在 **Windows 10 22H2（19045）** 上运行时，应用卡在 "Install driver" —— 根因是
Wave Link 运行时向服务端 `device-update-check.php` 请求驱动，服务端对
`osVersion=10.0.19045.0` 返回空列表（`appDevices:[]`），不下发音频驱动。

经逆向与实测确认：**系统级封锁只存在于两层（MSIX 版本门槛 + 服务端不下发驱动），
驱动二进制本身对 Windows 10 开放。** 本仓库提供绕过封锁、在 Win10 上装好驱动并验证
的完整流程产物。

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
- **理论支持**：Windows 10 **1809（17763）及以上全系列**（1809 / 1903 / 1909 / 2004 / 20H1 / 20H2 / 21H1 / 21H2 / 22H2）。其中主驱动（UsbAudio、UsbAudioks）要求 1809+；VirtUsbAudioEmu 子驱动可下探至 1803。
- **无 Win11 硬锁**：4 个 INF 的装饰中**均无 22000+（Win11）专属门槛**，对 Windows 10 全系开放。
- **不支持**：Windows 10 1709（16299）及更早版本（低于 17134）。
- **前提**：Wave Link 3.x 应用本体仍需先绕过 MSIX 的 Win11 门槛（不在本仓库范围）；本仓库只解决「驱动不下发」这一步。

> 说明：1809+ 为基于 INF OS 装饰的**理论支持**，目前仅 22H2 经过实测。若你在其他 Win10 版本安装成功，欢迎在仓库提交反馈以扩展实测矩阵。

## 流程总览

| 步骤 | 内容 | 产物 |
|---|---|---|
| 1. 定位驱动源 | 逆向 `Elgato.BaseClasses.Core.dll` 找到官方端点，伪装 Win11 参数抓包 | `driver/WaveLinkDriver_3.0.0.466_x64.msi` |
| 2. 兼容性判定 | 读取 MSI 内嵌 CAB 的 4 个 INF，确认无 Win11 硬锁 | `evidence/inf/*.inf` |
| 3. 安装 | `msiexec /i` 安装（关键：用 Windows 原生宿主，避免 MSYS 路径转换坑） | 退出码 0，无需重启 |
| 4. 核验 | 启动已打补丁的 Wave Link 3.x，抓日志确认 17/17 虚拟路由端点就绪 | `reports/wavelink_verify_report.md` |

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
│   ├── reinstall_wavelink_driver.bat      # 一键重装（管理员提权 + msiexec /qn）
│   ├── fetch_driver.bat                   # 从官方 CDN 重新下载 MSI（替代二进制）
│   ├── extract_cab.py                     # 用 olefile 抽取 MSI 内嵌 CAB
│   ├── find_cab.py                        # 定位 MSI 内的 Media.cab 流
│   ├── list_streams.py                    # 列出 OLE 复合文件流
│   ├── patch_manifest.py                  # 改 MSIX AppxManifest MinVersion（绕过 Win11 门槛）
│   ├── envcheck.ps1                       # 环境检查（管理员 / MSI 路径）
│   └── install.ps1                        # 原生宿主安装入口
├── reports/
│   ├── wavelink_driver_install_report.md  # 安装与 INF 兼容性报告
│   └── wavelink_verify_report.md          # 启动实测核验报告（17/17 端点）
├── certs/
│   └── WaveLinkPatch.cer                  # MSIX 改包用的自签证书（仅示意，非必需）
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

## 使用教程（保姆级）

> 本教程假设你已经**绕过 MSIX 的 Win11 门槛**、在 Windows 10 上装好了 Wave Link 3.x 应用本身（这一步不在本仓库范围，本仓库只解决「驱动不下发」）。

### 前置条件

- **Windows 10 1809（17763）或更高**（已实测 22H2 / 19045；1809+ 理论支持，详见「兼容性范围」）。查看方法：按 `Win + R` → 输入 `winver` → 回车。
- 已安装 Wave Link 3.x（应用能打开，但卡在 "Install driver" / 音频路由端点缺失）。
- 管理员权限（安装内核驱动必须）。
- 本仓库已下载到本地（见步骤 1）。

### 步骤 1：获取本仓库

**方式 A — 下载 ZIP（最简单）**
1. 在 GitHub 页面点击绿色的 **Code → Download ZIP**。
2. 解压到任意目录，例如 `C:\wavelink-win10-driver\`。

**方式 B — 用 git 克隆**
```bat
git clone https://github.com/<你的用户名>/wavelink-win10-driver.git
cd wavelink-win10-driver
```

### 步骤 2：安装驱动

#### 方式 A：一键安装（推荐小白）
1. 进入 `scripts\` 文件夹。
2. **右键** `reinstall_wavelink_driver.bat` → **以管理员身份运行**。
3. 脚本会自动检测管理员权限；若未提权会弹出 UAC 请求。安装过程无界面（静默 `/qn`），约几十秒。
4. 看到命令窗口回到提示符即完成。无需重启。

#### 方式 B：手动安装（复制粘贴即可）
1. 以**管理员**打开命令提示符（CMD）：按 `Win`，输入 `cmd`，右键 → 以管理员身份运行。
2. 逐行复制执行（把路径改成你解压的实际位置）：
```bat
cd /d "C:\wavelink-win10-driver\driver"
msiexec /i WaveLinkDriver_3.0.0.466_x64.msi /qn /norestart /l*v msi_install.log
```
3. 等待命令返回（无进度条），查看 `msi_install.log` 末尾应有 `Return Value 0`。

> ⚠️ **关键坑（必读）**：务必用 **Windows 原生宿主（cmd.exe / PowerShell）** 启动 `msiexec`。
> 在 **Git Bash / MSYS** 下，它会把 `C:\Users\...` 转成 `/Users\...` 传给 msiexec，导致服务端报 `Note 1314`、退出码 **83** 失败。若你用 Git Bash，请改用上面的 CMD 方式或 `scripts/install.ps1`。

### 步骤 3：验证

1. 启动 Wave Link 3.x（需先完成 MSIX 改包，不在本仓库范围）。
2. 打开 **Wave Link 设置 → 音频路由（Output Routing）**，确认以下端点全部可见且可选：
   `Wave Mic 1–4、Game、Music、Chat Mix、Voice Chat、Browser、SFX、System、Aux 1/2、Aux Mix、Personal Mix、Stream Mix、Recording Mix`（共 17 个）。
3. 若全部出现，驱动即安装成功。详细核验证据见 `reports/wavelink_verify_report.md`。

> 启动初期约 2 分钟内可能出现 `hr=0x88890004`（pipeline 初始化抖动），属正常现象，稍候即稳。

### 步骤 4：缺驱动包时重新下载

若你删除了 `driver/*.msi`，或想确保拿到官方原版：
1. 进入 `scripts\`。
2. 双击 `fetch_driver.bat`（或右键以管理员运行）。
3. 脚本会从官方公共 CDN `https://edge.elgato.com/egc/windows/ewlw/drivers/WaveLinkDriver_3.0.0.466_x64.msi` 重新下载到 `driver\`。
   - 需联网；若你的网络无法直连该 CDN，请自行配置代理后运行。

### 常见问题 FAQ

**Q1：安装脚本一闪而过 / 没反应？**
A：必须在 `scripts\` 目录内运行，且以管理员身份。建议在 CMD 里手动执行 `scripts\reinstall_wavelink_driver.bat` 看完整输出。

**Q2：msiexec 退出码 83？**
A：几乎都是 Git Bash/MSYS 路径转换导致。改用原生 CMD 或 `install.ps1`，路径保持 `C:\...` 形式。详见步骤 2 的警告。

**Q3：日志里 `Found 0 drivers` 是不是没装上？**
A：不是。这是运行时向服务端查更新（Win10 服务端不下发），非本地缺失。同一日志会显示 `Found current driver version: 3.0.0.466`，证明本地驱动已就位。

**Q4：能不能完全不含二进制上传？**
A：可以。删除 `driver/*.msi`，改用 `fetch_driver.bat` 下载即可（见步骤 4）。

**Q5：我的 Windows 10 是 1809 / 1909 / 21H1 等更老版本，能用吗？**
A：基于 INF OS 装饰，驱动安装层**理论支持 1809（17763）及以上全系**（VirtUsbAudioEmu 子驱动可下至 1803）。目前仅 22H2 实测通过；更老版本请按本教程自行验证，成功后可反馈以扩展实测矩阵。1709（16299）及更早不支持。

## 合规与排除项

本仓库**刻意排除**以下 Elgato 专有 / 大体积内容，仅保留研究产物与官方公开驱动：

| 排除项 | 体量 | 原因 |
|---|---|---|
| 两个 MSIX 应用包 | ~191MB ×2 | Elgato 专有应用，版权 / 体积 |
| `src\` 解包应用 DLL | ~390MB | Elgato 专有应用代码 |
| 驱动二进制 `.sys`/`.dll`/`.cat` | — | 由 MSI 自身提供，单独散布有版权风险 |
| `win11_update.xml` | 52MB | 服务端响应缓存 |

**纳入**：报告、脚本、证据日志、官方公开 CDN 驱动 MSI（3MB）、自签证书、4 个 INF 文本。

> 若你希望仓库完全不含任何二进制，可删除 `driver/*.msi` 并改用 `scripts/fetch_driver.bat` 下载。

## 已知限制

- 启动初期约 2 分钟内可能出现 `hr=0x88890004`（Thesycon pipeline 初始化抖动），非致命。
- 日志中 `Found 0 drivers` 是运行时向服务端查更新（Win10 服务端不下发），非本地缺失——同一日志明确 `Found current driver version: 3.0.0.466`，证明本地驱动已就位。
- 如需 GUI 交互级最终验证（播放音频确认电平走动），需你侧手动操作。
- 本仓库不提供 MSIX 改包后的 Wave Link 应用本体（版权与体积原因），仅提供驱动安装闭环。
- 兼容性仅 Windows 10 22H2 经过实测；1809+ 为基于 INF 的理论支持，未经逐版本实测。

## 参考资料

- 安装报告：`reports/wavelink_driver_install_report.md`
- 核验报告：`reports/wavelink_verify_report.md`
- 官方驱动 CDN：`https://edge.elgato.com/egc/windows/ewlw/drivers/WaveLinkDriver_3.0.0.466_x64.msi`

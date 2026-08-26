# Wave Link 3.x 驱动强制安装于 Windows 10（研究 / 复现包）
# Force-install Elgato Wave Link 3.x Driver on Windows 10 — Research / Reproduction Kit

> ⚠️ **免责声明 / Disclaimer**：本项目与 Elgato / Corsair 无任何关联，仅用于技术研究、兼容性验证与本地复现。
> This project is not affiliated with Elgato / Corsair. It is for technical research, compatibility verification, and local reproduction only.
> 所有 Elgato 专有资产（驱动 MSI、INF）的版权归 Elgato 所有，使用须遵守其许可协议。
> All Elgato proprietary assets (driver MSI, INF) are copyrighted by Elgato and must be used in accordance with their license.
> 不建议用于商业用途或规避正常的产品授权机制。详见 [`NOTICE`](./NOTICE)。
> Not intended for commercial use or for circumventing normal product licensing. See [`NOTICE`](./NOTICE).

---

## 项目简介 / Abstract

**中文**：Elgato Wave Link 3.x 官方仅支持 Windows 11。本仓库提供在 **Windows 10 22H2（19045）** 上绕过「服务端不下发驱动」限制、把官方音频驱动装好并验证可用的完整流程产物——含逆向取证、兼容性判定、安装日志与一键重装脚本。**不含任何 Elgato 专有应用二进制**，仅保留研究产物与官方公开 CDN 驱动（约 3 MB）。

**English**: Elgato Wave Link 3.x is officially Windows 11 only. This repository provides the full set of artifacts needed to bypass the "server does not push the driver" block and install + verify the official audio driver on **Windows 10 22H2 (19045)** — including reverse-engineering evidence, compatibility analysis, install logs, and a one-click reinstall script. **No Elgato proprietary app binaries are included**; only research artifacts and the official public-CDN driver (~3 MB).

> GitHub 仓库简介（Description）建议填写 / Suggested repo description:
> `Force-install Elgato Wave Link 3.x audio driver on Windows 10 — research & repro kit (bypasses MSIX Win11 gate + server-side driver block)`

---

## 背景 / Background

Elgato Wave Link 3.x 官方仅支持 Windows 11（MSIX 包 `MinVersion=22000`）。
在 **Windows 10 22H2（19045）** 上运行时，应用卡在 "Install driver" —— 根因是
Wave Link 运行时向服务端 `device-update-check.php` 请求驱动，服务端对
`osVersion=10.0.19045.0` 返回空列表（`appDevices:[]`），不下发音频驱动。

经逆向与实测确认：**系统级封锁只存在于两层（MSIX 版本门槛 + 服务端不下发驱动），
驱动二进制本身对 Windows 10 开放。** 本仓库提供绕过封锁、在 Win10 上装好驱动并验证
的完整流程产物。

**English**: Elgato Wave Link 3.x targets Windows 11 only (MSIX `MinVersion=22000`). On **Windows 10 22H2 (19045)** the app hangs at "Install driver". Root cause: at runtime Wave Link queries the server `device-update-check.php`, and for `osVersion=10.0.19045.0` the server returns an empty list (`appDevices:[]`), so the audio driver is never delivered. Reverse-engineering and live testing confirm the block exists in only two layers (MSIX version gate + server-side driver delivery), while **the driver binaries themselves are open to Windows 10**. This repo ships the full artifact set to bypass the block, install the driver, and verify it works.

---

## 流程总览 / Overview

| 步骤 | 内容 | 产物 |
|---|---|---|
| 1. 定位驱动源 | 逆向 `Elgato.BaseClasses.Core.dll` 找到官方端点，伪装 Win11 参数抓包 | `driver/WaveLinkDriver_3.0.0.466_x64.msi` |
| 2. 兼容性判定 | 读取 MSI 内嵌 CAB 的 4 个 INF，确认无 Win11 硬锁 | `evidence/inf/*.inf` |
| 3. 安装 | `msiexec /i` 安装（关键：用 Windows 原生宿主，避免 MSYS 路径转换坑） | 退出码 0，无需重启 |
| 4. 核验 | 启动已打补丁的 Wave Link 3.x，抓日志确认 17/17 虚拟路由端点就绪 | `reports/wavelink_verify_report.md` |

核心结论：驱动二进制对 Win10 开放（`LaunchConditions` 通过，4 个 INF 均无 Win11 装饰），
原"卡在 Install driver"的根因（服务端不下发）已被绕过。

**English**: (1) Locate the driver by reverse-engineering `Elgato.BaseClasses.Core.dll` to find the official endpoint and capturing the MSI with spoofed Win11 params. (2) Confirm Win10 compatibility by reading the 4 INF files inside the MSI's embedded CAB (no Win11-only decoration). (3) Install via `msiexec /i` using a native Windows host (avoids the MSYS path-translation trap). (4) Verify by launching the patched Wave Link 3.x and confirming 17/17 virtual routing endpoints are ready. Conclusion: the driver binaries are Win10-compatible; the original "stuck at Install driver" block (server-side non-delivery) is bypassed.

---

## 目录结构 / Repository Layout

```
wavelink_win10_driver/
├── README.md                      # 本文件（中英双语）/ This file (bilingual)
├── NOTICE                         # 合规 / 版权说明 / Compliance & copyright
├── LICENSE                        # MIT（仅覆盖自创脚本与文档）/ MIT (self-authored only)
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

---

## 使用教程（保姆级）/ Step-by-Step Guide

> 本教程假设你已经**绕过 MSIX 的 Win11 门槛**、在 Windows 10 上装好了 Wave Link 3.x 应用本身（这一步不在本仓库范围，本仓库只解决「驱动不下发」）。
> This guide assumes you have already bypassed the MSIX Win11 gate and installed the Wave Link 3.x app on Windows 10. This repo solves only the "driver not delivered" step.

### 前置条件 / Prerequisites

- Windows 10 22H2（版本号 19045）。查看方法：按 `Win + R` → 输入 `winver` → 回车。
- 已安装 Wave Link 3.x（应用能打开，但卡在 "Install driver" / 音频路由端点缺失）。
- 管理员权限（安装内核驱动必须）。
- 本仓库已下载到本地（见步骤 1）。

### 步骤 1：获取本仓库 / Get the repository

**方式 A — 下载 ZIP（最简单）**
1. 在 GitHub 页面点击绿色的 **Code → Download ZIP**。
2. 解压到任意目录，例如 `C:\wavelink-win10-driver\`。

**方式 B — 用 git 克隆**
```bat
git clone https://github.com/<你的用户名>/wavelink-win10-driver.git
cd wavelink-win10-driver
```

### 步骤 2：安装驱动 / Install the driver

#### 方式 A：一键安装（推荐小白）/ One-click (recommended)
1. 进入 `scripts\` 文件夹。
2. **右键** `reinstall_wavelink_driver.bat` → **以管理员身份运行**。
3. 脚本会自动检测管理员权限；若未提权会弹出 UAC 请求。安装过程无界面（静默 `/qn`），约几十秒。
4. 看到命令窗口回到提示符即完成。无需重启。

#### 方式 B：手动安装（复制粘贴即可）/ Manual (copy-paste)
1. 以**管理员**打开命令提示符（CMD）：按 `Win`，输入 `cmd`，右键 → 以管理员身份运行。
2. 逐行复制执行（把路径改成你解压的实际位置）：
```bat
cd /d "C:\wavelink-win10-driver\driver"
msiexec /i WaveLinkDriver_3.0.0.466_x64.msi /qn /norestart /l*v msi_install.log
```
3. 等待命令返回（无进度条），查看 `msi_install.log` 末尾应有 `Return Value 0`。

> ⚠️ **关键坑（必读）**：务必用 **Windows 原生宿主（cmd.exe / PowerShell）** 启动 `msiexec`。
> 在 **Git Bash / MSYS** 下，它会把 `C:\Users\...` 转成 `/Users\...` 传给 msiexec，导致服务端报 `Note 1314`、退出码 **83** 失败。若你用 Git Bash，请改用上面的 CMD 方式或 `scripts/install.ps1`。
> **Critical**: Always launch `msiexec` from a native Windows host (cmd.exe / PowerShell). Under Git Bash/MSYS the path `C:\Users\...` is rewritten to `/Users\...`, causing server error `Note 1314` and exit code **83**. Use CMD or `scripts/install.ps1`.

### 步骤 3：验证 / Verify

1. 启动 Wave Link 3.x（需先完成 MSIX 改包，不在本仓库范围）。
2. 打开 **Wave Link 设置 → 音频路由（Output Routing）**，确认以下端点全部可见且可选：
   `Wave Mic 1–4、Game、Music、Chat Mix、Voice Chat、Browser、SFX、System、Aux 1/2、Aux Mix、Personal Mix、Stream Mix、Recording Mix`（共 17 个）。
3. 若全部出现，驱动即安装成功。详细核验证据见 `reports/wavelink_verify_report.md`。

> 启动初期约 2 分钟内可能出现 `hr=0x88890004`（pipeline 初始化抖动），属正常现象，稍候即稳。
> In the first ~2 minutes you may see `hr=0x88890004` (pipeline init jitter) — benign, settles shortly.

### 步骤 4：缺驱动包时重新下载 / Re-fetch the driver if missing

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

---

## 合规与排除项 / Compliance & Exclusions

本仓库**刻意排除**以下 Elgato 专有 / 大体积内容，仅保留研究产物与官方公开驱动：

| 排除项 | 体量 | 原因 |
|---|---|---|
| 两个 MSIX 应用包 | ~191MB ×2 | Elgato 专有应用，版权 / 体积 |
| `src\` 解包应用 DLL | ~390MB | Elgato 专有应用代码 |
| 驱动二进制 `.sys`/`.dll`/`.cat` | — | 由 MSI 自身提供，单独散布有版权风险 |
| `win11_update.xml` | 52MB | 服务端响应缓存 |

**纳入**：报告、脚本、证据日志、官方公开 CDN 驱动 MSI（3MB）、自签证书、4 个 INF 文本。

> 若你希望仓库完全不含任何二进制，可删除 `driver/*.msi` 并改用 `scripts/fetch_driver.bat` 下载。
> If you want a binary-free repo, delete `driver/*.msi` and use `scripts/fetch_driver.bat` instead.

---

## 已知限制 / Known Limitations

- 启动初期约 2 分钟内可能出现 `hr=0x88890004`（Thesycon pipeline 初始化抖动），非致命。
- 日志中 `Found 0 drivers` 是运行时向服务端查更新（Win10 服务端不下发），非本地缺失——同一日志明确 `Found current driver version: 3.0.0.466`，证明本地驱动已就位。
- 如需 GUI 交互级最终验证（播放音频确认电平走动），需你侧手动操作。
- 本仓库不提供 MSIX 改包后的 Wave Link 应用本体（版权与体积原因），仅提供驱动安装闭环。

---

## 参考资料 / References

- 安装报告：`reports/wavelink_driver_install_report.md`
- 核验报告：`reports/wavelink_verify_report.md`
- 官方驱动 CDN：`https://edge.elgato.com/egc/windows/ewlw/drivers/WaveLinkDriver_3.0.0.466_x64.msi`

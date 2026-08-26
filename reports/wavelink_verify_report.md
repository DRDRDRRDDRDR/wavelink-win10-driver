# Wave Link 3.x 在 Windows 10 的实测核验报告

## 核验方式
- 通过 AUMID `Elgato.WaveLink_6q5ec962p4s3e!App`（Windows 原生宿主启动，避免 MSYS 路径坑）启动 Wave Link 3.x。
- 等待初始化后抓取最新运行日志：`%LOCALAPPDATA%\Packages\Elgato.WaveLink_6q5ec962p4s3e\LocalState\Logs\ElgatoWaveLink_2026-08-25-23-18-28.log`（UTF-8，5645 行）。

## 结论：驱动已被 Wave Link 正确识别并加载，17/17 虚拟路由端点就绪

### ✅ 成功信号
| 信号 | 日志原文 |
|---|---|
| 进程存活 | PID 22908，常驻内存约 584 MB，未崩溃 |
| 驱动被识别 | `Elgato driver Elgato Virtual Audio (6.0.0.30116)` |
| 安装包被识别 | `Elgato installer Elgato Wave Link Driver (3.0.0.466)` |
| 本地驱动版本 | `Found current driver version: 3.0.0.466`（与已装 MSI 一致） |
| 虚拟设备枚举 | `New Wave Software device(s) detected (17/17)`，`All Wave Software devices started` |
| 声卡可见 | `Soundcard name Elgato Virtual Audio`；`Elgato device VID:0x0FD9 PID:0x0096 Elgato Virtual Audio` |

### ⚠️ 警告（均非致命）
1. **`hr=0x88890004`（Thesycon Activate/Prepare 失败）**：共 8 次，全部集中在启动后前约 2 分钟（23:18:54–23:20:42），发生在 pipeline 移除/重建（reload）期间。上下文为 `Removed Pipeline ...` → `AudioDeviceRouterImpl::Stop` → `Activate failed`。由于设备均 `started` 且存在 42 条 pipeline-OK 事件，判定为**初始化抖动/竞态**，非致命。
2. **`Found 0 drivers` / `Found 0 potential driver updates`**：来自 `DriverUpdateService` 运行时向服务端查更新（Win10 服务端不下发），并非本地驱动缺失——同一日志明确 `Found current driver version: 3.0.0.466`，证明本地驱动已就位。
3. **大量 `FileNotFoundException` / 图标提取失败**：来自"按应用路由"功能扫描已卸载游戏/程序的 exe 路径（如 I:\、E:\ 等已不可达盘符），与音频驱动无关，良性噪声。

## 建议的下一步（需你侧 GUI 交互）
- 在 Wave Link 中播放一路音频（如浏览器音乐路由到某 Wave 输出），确认电平走动且可听，以最终确认 `0x88890004` 不影响实际路由。
- 若某一路在 GUI 播放时复现 `0x88890004` 且无声，多半是音频会话/设备被占用，再针对性排查。

## 重装脚本
- 见同目录 `reinstall_wavelink_driver.bat`：双击即以管理员身份重装 `WaveLinkDriver_3.0.0.466_x64.msi`（/qn /norestart，日志写 `msi_reinstall.log`）。该 bat 由 cmd.exe 原生执行，不受 MSYS 路径转换影响。

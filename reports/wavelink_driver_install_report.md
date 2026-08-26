# Wave Link 3.x 驱动强制安装结果报告（Windows 10 22H2 / 19045）

## 背景与原卡点
- 目标：在 Windows 10 上运行本只支持 Windows 11 的 Elgato Wave Link 3.x。
- 此前卡在 "Install driver" 界面：Wave Link 运行时向服务端 `device-update-check.php` 请求驱动，服务端对 `osVersion=10.0.19045.0` 返回 `appDevices:[]`（日志中的 `Found 0 drivers for Elgato Wave Link Driver`），因此不下发音频驱动。
- 系统级 OS 封锁只存在于两层：MSIX 包 `MinVersion=22000`（已改包绕过）+ 服务端不下发驱动。**驱动二进制本身对 Win10 开放。**

## 驱动来源
- 通过逆向 `Elgato.BaseClasses.Core.dll` 定位官方端点，以 Win11 参数伪装请求，抓到官方驱动 MSI：
  `https://edge.elgato.com/egc/windows/ewlw/drivers/WaveLinkDriver_3.0.0.466_x64.msi`（版本 3.0.0.466）。
- 本地已保存：`wavelink_patched\WaveLinkDriver_3.0.0.466_x64.msi`。

## INF OS 兼容性判定（只读，安装前）
| INF | OS 装饰 | 含义 |
|---|---|---|
| ElgatoVirtUsbAudioEmu | `ntamd64.10.0...17134` | Win10 1803+ |
| ElgatoUsbAudio | `ntamd64.10.0...17763` | Win10 1809+ |
| ElgatoUsbAudioks | `ntamd64.10.0...17763` | Win10 1809+ |
| ElgatoUsbAudio_dfu | `ntamd64`（无装饰） | 全 NTamd64 |

四个 INF 均无 Windows 11 硬锁；Win10 22H2（19045）全部满足。

## 安装方式（关键坑）
- 失败初因：在 Git Bash 下 `msiexec /i "C:\Users\..."` 时，MSYS 把路径转成 `/Users/...`，msiexec 服务端报 `Note 1314` + `MainEngineThread returning 2` + **退出码 83**。
- 修正：改用 Windows 原生宿主（PowerShell 运行环境）直接启动 msiexec，路径保持 `C:\Users\...`：
  `msiexec /i WaveLinkDriver_3.0.0.466_x64.msi /qn /norestart /l*v msi_install3.log`
- **结果：退出码 0，无需重启。** LaunchConditions 通过（`DetectVersionLaunchCondition already passes`），证明包内无 OS 启动条件拦截。

## 安装后验证
| 项 | 结果 |
|---|---|
| 服务 `ElgatoVirtUsbAudioEmu` | STATE 4 RUNNING |
| 服务 `ElgatoUsbAudio` | STATE 4 RUNNING |
| 服务 `ElgatoUsbAudioks` | STATE 4 RUNNING |
| 驱动商店 INF | 4 个 Elgato INF（usbaudio / usbaudioks / usbaudio_dfu / virtusbaudioemu），Provider=Elgato |
| PnP 虚拟设备 | `Elgato Root Device`（ROOT\ELGATO_ROOT_DEVICE\0000）状态 OK |
| 音频路由端点 | 全套 `X (Elgato Virtual Audio)`：Wave Mic 1–4、Game、Music、Chat Mix、Voice Chat、Browser、SFX、System、Aux 1/2、Aux Mix、Personal Mix、Stream Mix、Recording Mix |
| 其他 | `Elgato Virtual Audio`(MEDIA)、`Elgato APO` 软件组件均已就绪 |

## 结论
Wave Link 3.x 应用内音频路由所需的**内核驱动与虚拟音频端点已在 Windows 10 全部就位**。原先"卡在 Install driver"的根因（服务端对 Win10 不下发驱动）已绕过，驱动本身兼容 Win10。

## 后续与注意事项
1. 启动已打补丁并安装的 Wave Link 3.x GUI，实测麦克风/输出路由、推流混音等是否正常工作（交互验证，需你侧操作）。
2. 若某端点异常，优先在设备管理器查看 `Elgato Root Device` / `Elgato Virtual Audio` 是否带黄色感叹号（Code 52 表示驱动签名未被 DSE 接受，概率低，因 .cat 为 Elgato 正式签名）。
3. 如后续重装/升级 Wave Link，驱动通常保留；若被清理，可重新 `msiexec /i` 本 MSI（注意用 Windows 原生宿主启动，避免 MSYS 路径转换坑）。

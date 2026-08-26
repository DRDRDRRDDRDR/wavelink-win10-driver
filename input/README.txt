把官方 Wave Link 3.x 的 MSIX 安装包放在本目录。
文件名任意，扩展名须为 .msix（或 .appx）。
自动化脚本 scripts/setup_wavelink_win10.ps1 会自动使用本目录中第一个 .msix。

本目录内的 *.msix / *.appx 已被 .gitignore 忽略，不会进入 git 仓库（符合 NOTICE 合规决策）。

获取途径（任选其一）：
- Elgato 官方 Wave Link 下载器导出的 MSIX
- 已安装实例的 Package 文件夹复制 / 导出
- 你此前已下载的备份（例如 Elgato.WaveLink_3.2.10.4073_x64_Win10.msix）

注意：此文件为 Elgato 专有资产，请勿随本仓库分发。

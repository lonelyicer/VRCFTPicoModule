# VRCFTPicoModule

[![GitHub Release](https://img.shields.io/github/v/release/lonelyicer/VRCFTPicoModule)](https://github.com/lonelyicer/VRCFTPicoModule/releases/)
[![GitHub Downloads (all assets, all releases)](https://img.shields.io/github/downloads/lonelyicer/VRCFTPicoModule/total)](https://github.com/lonelyicer/VRCFTPicoModule/releases/latest)
[![GitHub Actions Workflow Status](https://img.shields.io/github/actions/workflow/status/lonelyicer/VRCFTPicoModule/ci.yml)](https://github.com/lonelyicer/VRCFTPicoModule/actions/workflows/ci.yml)


| [English](./README.md) | **简体中文** |

VRCFTPicoModule 是一个为 VRCFaceTracking 添加了对 PICO 4 Pro / Enterprise 支持的模块。

## 从这开始
### 1.下载  
从 [这里](https://github.com/lonelyicer/VRCFTPicoModule/releases/latest) 下载最新稳定版模块 (VRCFTPicoModule.zip) 和一键设置脚本 (SetupPICOConnect.ps1)。  
从 [这里](https://github.com/lonelyicer/VRCFTPicoModule/actions/workflows/ci.yml) 下载最新构建版模块 (VRCFTPicoModule.zip)。

### 2.运行一键设置脚本 (可选)  
右键 `SetupPICOConnect.ps1` 并点击 `使用 PowerShell 运行`。

> [!NOTE]  
> 你可能需要修改 PowerShell 的脚本执行策略  
> 以管理员权限运行 PowerShell 并执行以下命令：  
> ``` 
> Set-ExecutionPolicy RemoteSigned 
> ```

### 3.安装模块
启动 `VRCFaceTracking` 并点击 `官方模块库` 选项卡。  
然后点击 `Install Module from .zip` 按钮。  
选择名为 `VRCFTPicoModule.zip` 的文件。  

完成！你已经成功安装好了模块。

> [!IMPORTANT]  
> 如果你使用的是 `PICO Connect`。  
> 你需要手动更改协议版本或运行一键设置脚本。

> [!NOTE]  
> 要手动更改协议版本，
> 你需要修改位于 `%AppData%/PICO Connect/` 目录下的 `settings.json` 文件内的 `faceTrackingTransferProtocol` 键对应的值为 `2` 或者 `1`。

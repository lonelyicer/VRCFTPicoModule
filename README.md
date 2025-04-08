# VRCFTPicoModule

[![GitHub Release](https://img.shields.io/github/v/release/lonelyicer/VRCFTPicoModule)](https://github.com/lonelyicer/VRCFTPicoModule/releases/)
[![GitHub Downloads (all assets, all releases)](https://img.shields.io/github/downloads/lonelyicer/VRCFTPicoModule/total)](https://github.com/lonelyicer/VRCFTPicoModule/releases/latest)
[![GitHub Actions Workflow Status](https://img.shields.io/github/actions/workflow/status/lonelyicer/VRCFTPicoModule/ci.yml)](https://github.com/lonelyicer/VRCFTPicoModule/actions/workflows/ci.yml)


| **English** | [简体中文](./README.zh.md) |

VRCFTPicoModule is an extension module that adds support for PICO 4 Pro / Enterprise to [VRCFaceTracking](https://github.com/benaclejames/VRCFaceTracking).

## Getting Started
### 1.Download  
Download the latest module (VRCFTPicoModule.zip) and one step setup script (SetupPICOConnect.ps1) from [here](https://github.com/lonelyicer/VRCFTPicoModule/releases/latest).  
Download the CI build from [here](https://github.com/lonelyicer/VRCFTPicoModule/actions/workflows/ci.yml).

### 2.Run one step setup script (optional)  
Right Click the `SetupPICOConnect.ps1`, and select `Run with Powershell`.

> [!NOTE]  
> You may need change the execution policy in PowerShell  
> Start PowerShell with administrator privilege and run the command below:  
> ``` 
> Set-ExecutionPolicy RemoteSigned 
> ```

### 3.Install module
Start `VRCFaceTracking` and Click the `Module Registry` tab.  
Then click the `Install Module from .zip` button.  
Select the file named `VRCFTPicoModule.zip`.  

Done! You have successfully installed the module.

> [!IMPORTANT]  
> If you are using `PICO Connect`.  
> You will need to manually change the protocol version or run a one-step setup script.

> [!NOTE]  
> To manual change protocol version,
> you will need change the value of `faceTrackingTransferProtocol` in the `settings.json` file located in the `%AppData%/PICO Connect/` directory to `2` or `1`.

## Configuration  

To selectively disable eye or facial expression tracking, please follow these steps:  

### 1.Navigate to the module's configuration directory (path can be found in module logs)
### 2.Create corresponding files:
   - Create `.disable_eye` to disable eye tracking
   - Create `.disable_expression` to disable facial expression tracking
### 3.Restart the VRCFaceTracking for changes to take effect
# GSYNC

Windows 游戏存档同步工具，支持本地文件夹和 WebDAV。GSYNC 使用 WinUI 3，运行于 Windows x64。

[下载发行版](https://github.com/bennie-asi/gsync/releases) · [English](#english) · [项目文档](docs/README.md)

## 功能

- 管理游戏及其存档、配置等内容项，支持从 Steam、Epic 导入或手动添加。
- 在本地目录或 WebDAV 目标之间同步；比较差异后可逐文件选择本地版本、远端版本或暂不处理。
- 使用路径变量适配不同电脑；查看同步队列、历史记录和本地快照。
- 支持 Windows 登录时启动、关闭窗口后在后台运行，以及从系统托盘打开或退出。
- 自动检查 GitHub 正式版，验证发行签名并在同步任务结束后安装；启动检查失败时恢复旧版本。

## 安装和运行

从 [Releases](https://github.com/bennie-asi/gsync/releases) 下载 Windows x64 ZIP，解压到独立目录并运行 `GSYNC.App.exe`。应用数据默认保存在 `%APPDATA%\GSYNC`；可通过绝对路径环境变量 `GSYNC_DATA_ROOT` 指定其他目录。

关闭窗口默认会保留后台进程。单击系统托盘图标可重新打开窗口，右键可退出；也可在设置中关闭后台运行。登录自启是独立选项，默认关闭。

运行期间最多每天联网检查一次更新。自动检查和自动安装可在设置中分别关闭。安装目录必须可写，且不能与数据目录重叠。没有更新器或受管文件清单的旧版本，需要先手动解压新版。

## 同步和数据

- 上传使用不可变版本及条件写入发布清单。使用该格式前，请先升级所有设备上的 GSYNC。
- WebDAV 服务器需要支持强 ETag 和条件 PUT；远程认证要求 HTTPS，本机开发地址除外。
- Windows 凭据使用当前账户的 DPAPI 加密。将数据复制到其他账户后，需要重新输入凭据。
- 下载前可创建本地快照。恢复、冲突处理和更新失败时保留的备份不会替代用户自行备份。

## 开发

需要 Windows 10 1904x 或更新版本、x64、.NET 9.0.3xx SDK 和 .NET 8 运行时。

```powershell
dotnet restore GSYNC.sln
dotnet build src/GSYNC.App/GSYNC.App.csproj -p:Platform=x64
dotnet test GSYNC.sln -p:Platform=x64
```

使用 `scripts/build.ps1 -Version 1.0.0` 执行还原、测试、发布和打包。发布工作流还会使用仓库密钥签名更新清单。

主要项目：`GSYNC.App` 为桌面界面，`GSYNC.Core` 为同步逻辑，`GSYNC.Data` 为 SQLite 持久化，`GSYNC.Manifest`、`GSYNC.Storage` 和 `GSYNC.Providers` 分别负责游戏定义、存储和游戏来源。`GSYNC.Updater` 负责更新与恢复。

## English

GSYNC is a Windows x64 game-save synchronization app with local-folder and WebDAV targets. It supports game discovery, path variables, file comparison, sync history, snapshots, sign-in startup, and a notification-area icon.

Download the Windows x64 ZIP from [Releases](https://github.com/bennie-asi/gsync/releases), extract it to a separate folder, and run `GSYNC.App.exe`. Data is stored in `%APPDATA%\GSYNC`, unless an absolute `GSYNC_DATA_ROOT` is provided. Closing the window keeps the app running by default; use the tray icon to reopen or exit it.

The app checks for stable releases while running and can install signed updates after sync tasks finish. Checking, installation, background operation, and sign-in startup can be configured in Settings. Older installations without the update helper require one manual upgrade.

For development, install the .NET 9.0.3xx SDK and .NET 8 runtime, then run the build and test commands above. See [the documentation index](docs/README.md) for implementation details.

## License

No license has been declared. All rights are reserved by the author.

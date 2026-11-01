# GSYNC

Windows 游戏存档同步工具，支持本地文件夹和 WebDAV。GSYNC 使用 WinUI 3，运行于 Windows x64。

[下载发行版](https://github.com/bennie-asi/gsync/releases) · [English](#english) · [项目文档](docs/README.md)

## 功能

- 管理游戏及其存档、配置等内容项，支持从 Steam、Epic 导入或手动添加。
- 在本地目录或 WebDAV 目标之间同步；比较差异后可逐文件选择本地版本、远端版本或暂不处理。
- 使用路径变量适配不同电脑；查看同步队列、历史记录和本地快照。
- 支持 Windows 登录时在后台启动、关闭窗口后继续运行，以及从系统托盘打开或退出。
- 自动检查 GitHub 正式版，验证发行签名并在同步任务结束后安装；启动检查失败时恢复旧版本。

## 安装和运行

从 [Releases](https://github.com/bennie-asi/gsync/releases) 下载 Windows x64 ZIP，解压到独立目录并运行 `GSYNC.App.exe`。应用数据默认保存在 `%APPDATA%\GSYNC`；可通过绝对路径环境变量 `GSYNC_DATA_ROOT` 指定其他目录。

关闭窗口默认会保留后台进程。单击系统托盘图标可重新打开窗口，右键可退出；也可在设置中关闭后台运行。登录自启是独立选项，默认关闭；启用后默认在后台启动，可在设置中改为显示主窗口。手动启动仍会打开主窗口。

运行期间最多每天联网检查一次更新。自动检查和自动安装可在设置中分别关闭。安装目录必须可写，且不能与数据目录重叠。没有更新器或受管文件清单的旧版本，需要先手动解压新版。

## 同步和数据

- 上传使用不可变版本及条件写入发布清单。使用该格式前，请先升级所有设备上的 GSYNC。
- WebDAV 服务器需要支持强 ETag 和条件 PUT；远程认证要求 HTTPS，本机开发地址除外。
- Windows 凭据使用当前账户的 DPAPI 加密。将数据复制到其他账户后，需要重新输入凭据。
- 下载前可创建本地快照。恢复、冲突处理和更新失败时保留的备份不会替代用户自行备份。
- 在游戏详情页选择「更多 → 从游戏库移除」，可移除游戏和同步配置；本机、远端存档及历史记录会保留。快照文件也会保留，但移除后不能在 GSYNC 中恢复这些旧快照。

### 在两台电脑之间同步

1. 在第一台电脑配置 WebDAV 目标，添加游戏并上传存档。
2. 在第二台电脑配置同一个 WebDAV 目标。添加游戏时，选择目标后程序会扫描已有存档，并根据游戏标识或旧目录标识列出可能的匹配；选择候选项并确认本机路径与远端内容的对应关系。也可选择「关联已有远端存档」手动浏览；已有游戏可在详情页的同步位置编辑区执行手动关联。
3. 如果两台电脑选择的存档目录相差一层，可在关联时填写远端子目录，或在路径检查中选择对应的本机文件夹。保存配置后选择「比较存档」，确认差异并选择保留的版本，再开始同步。关联不会移动或删除原有远端文件；其他旧目录需自行确认后再处理。

上传时，GSYNC 会在远端存档元数据中记录可用的游戏目录标识、名称和内容根，不记录本机路径或凭据。旧版存档可能没有这些标识；扫描受目录深度、数量和时间限制，未找到时可以手动选择。

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

The app checks for stable releases while running and can install signed updates after sync tasks finish. Checking, installation, background operation, and sign-in startup can be configured in Settings. Sign-in startup is off by default; when enabled, it starts in the background unless configured otherwise. Manual launches still open the window. Older installations without the update helper require one manual upgrade.

To sync between computers, configure the same WebDAV target on both. Upload the game from the first computer; when adding it on the second, select the target and review the suggested matches, or choose “Link existing remote saves” to browse manually. Existing games can be linked from their sync location editor. Match each local save to its remote content, adjust the remote subdirectory if the folder levels differ, and compare the files before syncing. Uploads record available game identifiers and content roots in remote metadata without local paths or credentials. Older saves may lack identifiers, and scans have depth, count, and time limits.

For development, install the .NET 9.0.3xx SDK and .NET 8 runtime, then run the build and test commands above. See [the documentation index](docs/README.md) for implementation details.

## License

No license has been declared. All rights are reserved by the author.

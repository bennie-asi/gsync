using GSYNC.App.Infrastructure.Localization;
using CommunityToolkit.Mvvm.ComponentModel;

namespace GSYNC.App.ViewModels;

public partial class ConflictResolutionViewModel : ObservableObject
{
    private readonly bool _isChinese;

    public ConflictResolutionViewModel(ILocalizationService localizationService)
    {
        _isChinese = localizationService.CurrentLanguageTag == "zh-CN";

        Title = Pick("解决冲突", "Resolve Conflict");
        WarningMessage = Pick("本地与远端版本都在上次同步后发生了变化。请选择要保留的版本以避免数据丢失。", "Both local and remote versions have changed since the last synchronization. Select which version to keep to avoid data loss.");
        LocalTitle = Pick("本地版本", "Local Version");
        RemoteTitle = Pick("远端版本", "Remote Version");
        BaselineTitle = Pick("同步基线", "Sync baseline");
        TableTitle = Pick("冲突文件", "Conflicting files");
        TableSubtitle = Pick("在选择解决路径前，先检查文件名、本地状态与远端状态。", "Review filename, local status, and remote status before choosing a resolution path.");
        TableFooterText = Pick("记住此选择的能力会在正式实现后开放。", "Remember this choice is available after final implementation.");
        UseLocalText = Pick("使用本地并上传", "Use Local and Upload");
        UseRemoteText = Pick("使用远端并下载", "Use Remote and Download");
        KeepBothText = Pick("保留两份副本", "Keep Both Copies");
        BackupRestoreText = Pick("先备份本地再恢复远端", "Backup Local then Restore Remote");
        CancelText = Pick("取消", "Cancel");

        LocalVersion =
        [
            new(Pick("设备", "Device"), Pick("Desktop-A · 当前设备", "Desktop-A · current device")),
            new(Pick("修改时间", "Modified"), Pick("今天 · 09:42", "Today · 09:42")),
            new(Pick("文件数", "Files"), Pick("3 个文件", "3 files")),
            new(Pick("大小", "Size"), "320 MB"),
            new(Pick("摘要", "Summary"), Pick("主存档已推进，并更新了本地模组元数据", "Progressed main save and updated local mod metadata")),
        ];

        RemoteVersion =
        [
            new(Pick("来源", "Source"), Pick("Steam Deck · 远端", "Steam Deck · remote")),
            new(Pick("修改时间", "Modified"), Pick("今天 · 08:58", "Today · 08:58")),
            new(Pick("文件数", "Files"), Pick("2 个文件", "2 files")),
            new(Pick("大小", "Size"), "318 MB"),
            new(Pick("摘要", "Summary"), Pick("自动存档会话已完成，并包含掌机本地设置", "Auto-save session completed with deck-local settings")),
        ];

        ConflictFiles =
        [
            new("ER0000.sl2", Pick("进度较新", "Progress newer"), Pick("掌机存档较新", "Deck save newer")),
            new("GraphicsConfig.xml", Pick("本地已更新", "Updated locally"), Pick("远端未变化", "Unchanged remotely")),
            new("mods/config.json", Pick("校验和不同", "Different checksum"), Pick("校验和不同", "Different checksum")),
        ];

        SyncBaseline =
        [
            new(Pick("最近成功同步", "Last Successful Sync"), Pick("昨天 · 21:10", "Yesterday · 21:10")),
            new(Pick("基线大小", "Baseline Size"), "314 MB"),
            new(Pick("记住此选择", "Remember this choice"), Pick("当前内容项未勾选", "Unchecked for this content item")),
        ];
    }

    public string Title { get; }
    public string WarningMessage { get; }
    public string LocalTitle { get; }
    public string RemoteTitle { get; }
    public string BaselineTitle { get; }
    public string TableTitle { get; }
    public string TableSubtitle { get; }
    public string TableFooterText { get; }
    public string UseLocalText { get; }
    public string UseRemoteText { get; }
    public string KeepBothText { get; }
    public string BackupRestoreText { get; }
    public string CancelText { get; }
    public bool IsChinese => _isChinese;
    public IReadOnlyList<UiPropertyRow> LocalVersion { get; }
    public IReadOnlyList<UiPropertyRow> RemoteVersion { get; }
    public IReadOnlyList<ConflictFileRow> ConflictFiles { get; }
    public IReadOnlyList<UiPropertyRow> SyncBaseline { get; }

    private string Pick(string zh, string en) => _isChinese ? zh : en;
}

public sealed record ConflictFileRow(string Filename, string LocalStatus, string RemoteStatus);

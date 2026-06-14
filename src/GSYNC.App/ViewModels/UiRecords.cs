namespace GSYNC.App.ViewModels;

public sealed record UiPropertyRow(string Label, string Value);

public sealed record UiGameMetaRow(
    Guid InstanceId,
    string DisplayName,
    string GameId,
    string SourceLabel,
    string InstallDirectory,
    int VariableCount,
    string EditButtonText)
{
    public string InstallDirectoryDisplay =>
        string.IsNullOrWhiteSpace(InstallDirectory) ? "—" : InstallDirectory;
}
public sealed record UiActivityItem(string PrimaryText, string SecondaryText, string Timestamp, string StatusVariant = "ready", bool IsTerminal = false);
public sealed record UiBindingUsage(string Name, string Detail);
public sealed record UiSettingOption(string Title, string Summary, string KeyLabel = "", string Status = "", string Variant = "ready", string SectionId = "");
public sealed record UiPreviewRow(string Name, string Path, string Status, string Variant = "ready", string Summary = "", string PrimaryActionText = "", string SecondaryActionText = "");
public sealed record UiOption(string Key, string Label)
{
    public override string ToString() => Label;
}

public sealed record UiTargetOption(Guid Id, string Label)
{
    public override string ToString() => Label;
}


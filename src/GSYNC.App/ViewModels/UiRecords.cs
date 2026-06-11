namespace GSYNC.App.ViewModels;

public sealed record UiPropertyRow(string Label, string Value);
public sealed record UiActivityItem(string PrimaryText, string SecondaryText, string Timestamp, bool IsTerminal = false);
public sealed record UiBindingUsage(string Name, string Detail);
public sealed record UiSettingOption(string Title, string Summary, string KeyLabel = "", string Status = "", string Variant = "ready");
public sealed record UiPreviewRow(string Name, string Path, string Status, string Variant = "ready", string Summary = "", string PrimaryActionText = "", string SecondaryActionText = "");

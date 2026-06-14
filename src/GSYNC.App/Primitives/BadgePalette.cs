namespace GSYNC.App.Primitives;

/// <summary>
/// Maps sync status variants to the shared badge accent brush keys so that
/// every surface (library status badges, history result badges, activity
/// timeline dots) renders the same colour for the same state.
/// </summary>
public static class BadgePalette
{
    public static string ResolveBrushKey(string? variant)
    {
        var normalized = variant?.Trim().ToLowerInvariant();
        return normalized switch
        {
            "synced" or "success" => "BadgeSyncedBrush",
            "local newer" => "BadgeLocalNewerBrush",
            "remote newer" => "BadgeRemoteNewerBrush",
            "conflict" => "BadgeConflictBrush",
            "pending" => "BadgePendingBrush",
            "missing" => "BadgeMissingBrush",
            "disabled" => "BadgeDisabledBrush",
            _ => "BadgeReadyBrush",
        };
    }
}

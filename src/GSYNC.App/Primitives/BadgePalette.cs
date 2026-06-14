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

    /// <summary>
    /// Maps the same status variants to their Material Symbols codepoint so badges show the
    /// leading status icon used in the Stitch design. Returns the glyph string for
    /// <c>FontIcon.Glyph</c> (font family is the bundled Material Symbols).
    /// </summary>
    public static string ResolveGlyph(string? variant)
    {
        var normalized = variant?.Trim().ToLowerInvariant();
        var codepoint = normalized switch
        {
            "synced" or "success" => 0xF0BE, // check_circle
            "local newer" => 0xF09B,         // upload
            "remote newer" => 0xF090,        // download
            "conflict" => 0xE629,            // sync_problem
            "pending" => 0xE88B,             // hourglass_empty
            "missing" => 0xE2C1,             // cloud_off
            "disabled" => 0xE15B,            // remove
            _ => 0xEA0B,                     // bolt
        };

        return char.ConvertFromUtf32(codepoint);
    }
}

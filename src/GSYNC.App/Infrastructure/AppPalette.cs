using Microsoft.UI;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Media;

namespace GSYNC.App.Infrastructure;

/// <summary>Updates the shared brushes used by both XAML and dynamically created controls.</summary>
internal static class AppPalette
{
    private static readonly (string Key, uint Dark, uint Light)[] Colors =
    [
        ("AppBackgroundBrush", 0xFF0B0E17, 0xFFF3F6FB),
        ("AppSurfaceBrush", 0xFF131929, 0xFFFFFFFF),
        ("AppSurfaceRaisedBrush", 0xFF151C2D, 0xFFE8EEF9),
        ("AppSurfaceAltBrush", 0xFF101624, 0xFFF1F5FA),
        ("AppInputBrush", 0xFF0F1522, 0xFFFFFFFF),
        ("AppDividerBrush", 0x886B7592, 0xFFC8D0E0),
        ("AppPrimaryBrush", 0xFF5B8CFF, 0xFF739CFF),
        ("AppSuccessBrush", 0xFFA5D6A7, 0xFF257A47),
        ("AppWarningBrush", 0xFFFFB74D, 0xFF986000),
        ("AppDangerBrush", 0xFFEE7D77, 0xFFB3261E),
        ("AppMutedBrush", 0xFF95A0BE, 0xFF64748B),
        ("AppTextPrimaryBrush", 0xFFE8EDF8, 0xFF182338),
        ("AppTextSecondaryBrush", 0xFFB3BDD6, 0xFF46566E),
        ("AppTextDimBrush", 0xFF8994AE, 0xFF60718A),
        ("BadgeSyncedBrush", 0xFFA5D6A7, 0xFF257A47),
        ("BadgeLocalNewerBrush", 0xFFFFB74D, 0xFF986000),
        ("BadgeRemoteNewerBrush", 0xFF8FB3FF, 0xFF315FAF),
        ("BadgeConflictBrush", 0xFFEE7D77, 0xFFB3261E),
        ("BadgePendingBrush", 0xFFFFB74D, 0xFF986000),
        ("BadgeMissingBrush", 0xFF8D97B3, 0xFF5C6A80),
        ("BadgeDisabledBrush", 0xFF6B7592, 0xFF68758A),
        ("BadgeReadyBrush", 0xFF5B8CFF, 0xFF315FAF),
    ];

    public static void Apply(ResourceDictionary resources, bool light)
    {
        foreach (var (key, dark, lightColor) in Colors)
        {
            if (resources[key] is not SolidColorBrush brush) continue;
            var argb = light ? lightColor : dark;
            brush.Color = ColorHelper.FromArgb((byte)(argb >> 24), (byte)(argb >> 16), (byte)(argb >> 8), (byte)argb);
        }
    }
}

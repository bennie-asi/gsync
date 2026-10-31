using Microsoft.UI;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Media;

namespace GSYNC.App.Infrastructure;

/// <summary>Updates the shared brushes used by both XAML and dynamically created controls.</summary>
internal static class AppPalette
{
    private static readonly (string Key, uint Dark, uint Light)[] Colors =
    [
        ("AppBackgroundBrush", 0xFF101419, 0xFFF3F6F5),
        ("AppSurfaceBrush", 0xFF1A2028, 0xFFFFFFFF),
        ("AppSurfaceRaisedBrush", 0xFF253039, 0xFFE2F2ED),
        ("AppSurfaceAltBrush", 0xFF151B21, 0xFFF0F5F3),
        ("AppInputBrush", 0xFF111820, 0xFFFFFFFF),
        ("AppDividerBrush", 0xFF34414B, 0xFFC8D5D1),
        ("AppPrimaryBrush", 0xFF00C9A7, 0xFF00A88D),
        ("AppSuccessBrush", 0xFF6EDDB8, 0xFF217A56),
        ("AppWarningBrush", 0xFFFFBF58, 0xFF926000),
        ("AppDangerBrush", 0xFFEE7D77, 0xFFB3261E),
        ("AppMutedBrush", 0xFF9BAAB8, 0xFF647870),
        ("AppTextPrimaryBrush", 0xFFE8EDF2, 0xFF1B2A2A),
        ("AppTextSecondaryBrush", 0xFFB1BEC9, 0xFF465C58),
        ("AppTextDimBrush", 0xFF9DAFBC, 0xFF536A64),
        ("BadgeSyncedBrush", 0xFF6EDDB8, 0xFF217A56),
        ("BadgeLocalNewerBrush", 0xFFFFBF58, 0xFF926000),
        ("BadgeRemoteNewerBrush", 0xFF8FB3FF, 0xFF315FAF),
        ("BadgeConflictBrush", 0xFFFFBF58, 0xFF926000),
        ("BadgePendingBrush", 0xFFFFBF58, 0xFF926000),
        ("BadgeMissingBrush", 0xFF8D97B3, 0xFF5C6A80),
        ("BadgeDisabledBrush", 0xFF6B7592, 0xFF68758A),
        ("BadgeReadyBrush", 0xFF00C9A7, 0xFF007B68),
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

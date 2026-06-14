using GSYNC.App.Primitives;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Data;
using Microsoft.UI.Xaml.Media;

namespace GSYNC.App.Infrastructure.Converters;

/// <summary>
/// Resolves a sync status variant string (for example "synced" or "conflict")
/// to its shared badge accent brush, keeping timeline dots in step with the
/// status badges used across the library and history screens.
/// </summary>
public sealed class BadgeVariantToBrushConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, string language)
    {
        var key = BadgePalette.ResolveBrushKey(value as string);
        return (Brush)Application.Current.Resources[key];
    }

    public object ConvertBack(object value, Type targetType, object parameter, string language)
        => throw new NotSupportedException();
}

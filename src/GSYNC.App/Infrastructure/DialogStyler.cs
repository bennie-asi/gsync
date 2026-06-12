using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace GSYNC.App.Infrastructure;

public static class DialogStyler
{
    public static ContentDialog Apply(ContentDialog dialog)
    {
        dialog.RequestedTheme = ElementTheme.Dark;

        if (Application.Current.Resources.TryGetValue("PrimaryToolbarButtonStyle", out var primaryStyle) && primaryStyle is Style primary)
        {
            dialog.PrimaryButtonStyle = primary;
        }

        if (Application.Current.Resources.TryGetValue("SecondaryToolbarButtonStyle", out var secondaryStyle) && secondaryStyle is Style secondary)
        {
            dialog.SecondaryButtonStyle = secondary;
            dialog.CloseButtonStyle = secondary;
        }

        return dialog;
    }
}

using GSYNC.App.Infrastructure.Localization;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace GSYNC.App.Infrastructure;

public static class DialogStyler
{
    public static ContentDialog Apply(ContentDialog dialog)
    {
        var settings = App.GetService<UiSettingsStore>().Load();
        dialog.RequestedTheme = settings.ThemeMode == AppUiSettings.ThemeLight
            ? ElementTheme.Light
            : ElementTheme.Dark;

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

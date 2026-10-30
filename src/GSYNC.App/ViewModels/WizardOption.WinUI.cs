namespace GSYNC.App.ViewModels;

public sealed partial record WizardOption
{
    public Microsoft.UI.Xaml.Media.Brush BorderBrush =>
        (Microsoft.UI.Xaml.Media.Brush)Microsoft.UI.Xaml.Application.Current.Resources[IsSelected ? "AppPrimaryBrush" : "AppDividerBrush"];
}

using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace GSYNC.App.Primitives;

public sealed partial class WizardStepRail : UserControl
{
    public static readonly DependencyProperty RailContentProperty = DependencyProperty.Register(
        nameof(RailContent),
        typeof(object),
        typeof(WizardStepRail),
        new PropertyMetadata(null));

    public WizardStepRail()
    {
        InitializeComponent();
    }

    public object RailContent
    {
        get => GetValue(RailContentProperty);
        set => SetValue(RailContentProperty, value);
    }
}

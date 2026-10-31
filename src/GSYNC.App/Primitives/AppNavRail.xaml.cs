using GSYNC.App.Infrastructure.Localization;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Automation;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;

namespace GSYNC.App.Primitives;

public sealed partial class AppNavRail : UserControl
{
    public static readonly DependencyProperty SelectedKeyProperty = DependencyProperty.Register(
        nameof(SelectedKey),
        typeof(string),
        typeof(AppNavRail),
        new PropertyMetadata("library", OnSelectedKeyChanged));

    public static readonly DependencyProperty TargetStatusTextProperty = DependencyProperty.Register(
        nameof(TargetStatusText),
        typeof(string),
        typeof(AppNavRail),
        new PropertyMetadata(string.Empty));

    private bool _isExpanded = true;
    private bool _isChinese = true;

    public event EventHandler<NavigationRequestedEventArgs>? NavigationRequested;

    public AppNavRail()
    {
        InitializeComponent();
        Loaded += (_, _) =>
        {
            ApplyLayout();
            ApplySelectionState();
        };
    }

    public string SelectedKey
    {
        get => (string)GetValue(SelectedKeyProperty);
        set => SetValue(SelectedKeyProperty, value);
    }

    public string TargetStatusText
    {
        get => (string)GetValue(TargetStatusTextProperty);
        set => SetValue(TargetStatusTextProperty, value);
    }

    public void ApplyLocalization(ILocalizationService localizationService)
    {
        _isChinese = localizationService.CurrentLanguageTag == "zh-CN";
        LibraryTextBlock.Text = localizationService.GetString("MainWindow.Nav.Library");
        TargetsTextBlock.Text = localizationService.GetString("MainWindow.Nav.Targets");
        VariablesTextBlock.Text = localizationService.GetString("MainWindow.Nav.Variables");
        HistoryTextBlock.Text = localizationService.GetString("MainWindow.Nav.History");
        QueueTextBlock.Text = localizationService.GetString("MainWindow.Nav.Queue");
        SettingsTextBlock.Text = localizationService.GetString("MainWindow.Nav.Settings");
        BrandSubtitleTextBlock.Text = _isChinese ? "游戏存档同步" : "Game save sync";
        foreach (var (button, label) in new[]
        {
            (LibraryButton, LibraryTextBlock), (TargetsButton, TargetsTextBlock),
            (VariablesButton, VariablesTextBlock), (HistoryButton, HistoryTextBlock),
            (QueueButton, QueueTextBlock), (SettingsButton, SettingsTextBlock),
        })
        {
            AutomationProperties.SetName(button, label.Text);
            ToolTipService.SetToolTip(button, label.Text);
        }
        ApplyLayout();
    }

    private static void OnSelectedKeyChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is AppNavRail rail)
        {
            rail.ApplySelectionState();
        }
    }

    private void NavButton_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button button && button.Tag is string key)
        {
            NavigationRequested?.Invoke(this, new NavigationRequestedEventArgs(key));
        }
    }

    public void SetExpanded(bool expanded)
    {
        _isExpanded = expanded;
        ApplyLayout();
    }

    private void ToggleRail_Click(object sender, RoutedEventArgs e) => SetExpanded(!_isExpanded);

    private void ApplyLayout()
    {
        if (RootGrid is null) return;
        Width = _isExpanded ? 220 : 72;
        RootGrid.Padding = _isExpanded ? new Thickness(10, 18, 10, 12) : new Thickness(8, 18, 8, 12);
        BrandPanel.HorizontalAlignment = _isExpanded ? HorizontalAlignment.Left : HorizontalAlignment.Center;
        BrandPanel.Margin = _isExpanded ? new Thickness(3, 0, 0, 0) : new Thickness(0);
        QueueIconGrid.ColumnSpacing = _isExpanded ? 12 : 0;
        foreach (var button in new[] { LibraryButton, TargetsButton, VariablesButton, HistoryButton, QueueButton, SettingsButton, ToggleRailButton })
        {
            button.MinWidth = 0;
            button.Padding = _isExpanded ? new Thickness(12, 8, 12, 8) : new Thickness(0);
            button.HorizontalContentAlignment = _isExpanded ? HorizontalAlignment.Stretch : HorizontalAlignment.Center;
        }
        var visibility = _isExpanded ? Visibility.Visible : Visibility.Collapsed;
        BrandTextPanel.Visibility = visibility;
        TargetStatusCard.Visibility = visibility;
        LibraryTextBlock.Visibility = visibility;
        TargetsTextBlock.Visibility = visibility;
        VariablesTextBlock.Visibility = visibility;
        HistoryTextBlock.Visibility = visibility;
        QueueTextBlock.Visibility = visibility;
        SettingsTextBlock.Visibility = visibility;
        ToggleRailTextBlock.Visibility = visibility;
        ToggleRailTextBlock.Text = _isChinese ? "收起导航" : "Collapse navigation";
        ToggleRailIcon.Glyph = _isExpanded ? "\uE5CB" : "\uE5CC";
        var toggleName = _isExpanded ? (_isChinese ? "收起导航" : "Collapse navigation") : (_isChinese ? "展开导航" : "Expand navigation");
        AutomationProperties.SetName(ToggleRailButton, toggleName);
        ToolTipService.SetToolTip(ToggleRailButton, toggleName);
    }

    private void ApplySelectionState()
    {
        ApplyButtonState(LibraryButton, LibraryTextBlock, LibraryIcon, SelectedKey is "library" or "wizard" or "conflict");
        ApplyButtonState(TargetsButton, TargetsTextBlock, TargetsIcon, SelectedKey == "targets");
        ApplyButtonState(VariablesButton, VariablesTextBlock, VariablesIcon, SelectedKey == "variables");
        ApplyButtonState(HistoryButton, HistoryTextBlock, HistoryIcon, SelectedKey == "history");
        ApplyButtonState(QueueButton, QueueTextBlock, QueueIcon, SelectedKey == "queue");
        ApplyButtonState(SettingsButton, SettingsTextBlock, SettingsIcon, SelectedKey == "settings");
    }

    public void SetQueueBadge(int count)
    {
        if (QueueBadgeBorder is null || QueueBadgeTextBlock is null)
        {
            return;
        }

        QueueBadgeBorder.Visibility = count > 0 ? Visibility.Visible : Visibility.Collapsed;
        QueueBadgeTextBlock.Text = count.ToString();
    }

    private void ApplyButtonState(Button button, TextBlock label, FontIcon icon, bool isSelected)
    {
        var resources = Application.Current.Resources;
        button.Background = (Brush)resources[isSelected ? "AppSurfaceRaisedBrush" : "AppSurfaceAltBrush"];
        var foreground = (Brush)resources[isSelected ? "AppPrimaryBrush" : "AppTextSecondaryBrush"];
        button.Foreground = foreground;
        label.Foreground = foreground;
        icon.Foreground = foreground;
        button.BorderBrush = (Brush)resources[isSelected ? "AppPrimaryBrush" : "AppSurfaceAltBrush"];
        button.BorderThickness = new Thickness(1);
    }
}

public sealed class NavigationRequestedEventArgs(string key) : EventArgs
{
    public string Key { get; } = key;
}

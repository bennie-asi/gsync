using GSYNC.App.ViewModels;
using GSYNC.App.Infrastructure;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Serilog;

namespace GSYNC.App.Pages;

public sealed partial class VariablesPage : Page
{
    private VariablesPageViewModel? _viewModel;
    private bool _isEditingVariable;

    public VariablesPage()
    {
        InitializeComponent();
        ResponsiveSplitLayout.Attach(VariablePanes, VariableDetailPane, 900, new GridLength(320), new GridLength(1, GridUnitType.Star));
        TryInitializePage();
    }

    private void VariableHeader_SizeChanged(object sender, SizeChangedEventArgs e)
    {
        var narrow = e.NewSize.Width < 760;
        Grid.SetRow(VariableActions, narrow ? 1 : 0);
        Grid.SetColumn(VariableActions, narrow ? 0 : 1);
        Grid.SetColumnSpan(VariableActions, narrow ? 2 : 1);
    }

    private void TryInitializePage()
    {
        try
        {
            ThrowIfInitializationForcedToFail("variables");
            Log.Information("Initializing VariablesPage.");
            _viewModel = App.GetService<VariablesPageViewModel>();
            DataContext = _viewModel;

            MainContentRoot.Visibility = Visibility.Visible;
            InitializationErrorPanel.Visibility = Visibility.Collapsed;
            _viewModel.Load();
            Log.Information("VariablesPage initialized successfully.");
        }
        catch (Exception exception)
        {
            Log.Error(exception, "VariablesPage initialization failed.");
            MainContentRoot.Visibility = Visibility.Collapsed;
            InitializationErrorPanel.Visibility = Visibility.Visible;
            InitializationErrorMessage.Text = exception.Message;
        }
    }

    private static void ThrowIfInitializationForcedToFail(string pageKey)
    {
        var configured = Environment.GetEnvironmentVariable("GSYNC_FAIL_PAGE_INIT")?.Trim().ToLowerInvariant();
        if (string.Equals(configured, pageKey, StringComparison.Ordinal))
        {
            throw new InvalidOperationException($"Forced {pageKey} page initialization failure for diagnostics.");
        }
    }

    private void RetryInitialization_Click(object sender, RoutedEventArgs e)
    {
        TryInitializePage();
    }

    private void VariableRow_Tapped(object sender, TappedRoutedEventArgs e)
    {
        if (!_isEditingVariable && _viewModel is not null && sender is FrameworkElement { DataContext: VariableRow row })
        {
            _viewModel.SelectVariable(row);
        }
    }

    private void RefreshButton_Click(object sender, RoutedEventArgs e)
    {
        _viewModel?.Load();
    }

    private void ClearTesterButton_Click(object sender, RoutedEventArgs e)
    {
        _viewModel?.ClearTester();
    }

    private void TestTemplateInlineButton_Click(object sender, RoutedEventArgs e)
    {
        _viewModel?.TestTemplate(_viewModel.TemplateInput);
    }

    private void AddVariableButton_Click(object sender, RoutedEventArgs e)
    {
        if (!_isEditingVariable) ShowVariableEditor(isEdit: false);
    }

    private async void EditVariableButton_Click(object sender, RoutedEventArgs e)
    {
        if (_viewModel is null)
        {
            return;
        }

        if (!_viewModel.IsSelectedVariableUserDefined())
        {
            await ShowInfoAsync(
                _viewModel.IsChinese ? "无法编辑内置变量" : "Built-in variables cannot be edited",
                _viewModel.IsChinese
                    ? "系统和来源变量由运行环境解析。你可以添加自定义变量来覆盖路径模板中的取值。"
                    : "System and source variables are resolved from the runtime environment. Add a custom variable to override values in path templates.");
            return;
        }

        if (!_isEditingVariable) ShowVariableEditor(isEdit: true);
    }

    private async void DeleteVariableButton_Click(object sender, RoutedEventArgs e)
    {
        if (_viewModel is null || _isEditingVariable)
        {
            return;
        }

        if (!_viewModel.IsSelectedVariableUserDefined())
        {
            await ShowInfoAsync(
                _viewModel.IsChinese ? "无法删除内置变量" : "Built-in variables cannot be deleted",
                _viewModel.IsChinese
                    ? "只有自定义变量可以删除。"
                    : "Only custom variables can be deleted.");
            return;
        }

        var isChinese = _viewModel.IsChinese;
        var dialog = DialogStyler.Apply(new ContentDialog
        {
            XamlRoot = XamlRoot,
            Title = isChinese ? "删除变量" : "Delete Variable",
            Content = isChinese
                ? $"确定删除自定义变量 {_viewModel.SelectedVariableName} 吗？"
                : $"Delete custom variable {_viewModel.SelectedVariableName}?",
            PrimaryButtonText = isChinese ? "删除" : "Delete",
            CloseButtonText = isChinese ? "取消" : "Cancel",
            DefaultButton = ContentDialogButton.Close,
        });

        if (await dialog.ShowAsync() == ContentDialogResult.Primary)
        {
            _viewModel.DeleteSelectedUserVariable();
        }
    }

    private void TestTemplateButton_Click(object sender, RoutedEventArgs e)
    {
        TemplateTesterSheet.StartBringIntoView();
    }

    private void ShowVariableEditor(bool isEdit)
    {
        if (_viewModel is null)
        {
            return;
        }

        var isChinese = _viewModel.IsChinese;
        var existing = isEdit ? _viewModel.GetSelectedUserVariable() : null;

        var nameBox = new TextBox
        {
            Header = isChinese ? "变量名" : "Variable Name",
            PlaceholderText = "%CUSTOM_SAVE_ROOT%",
            Text = existing?.Name ?? string.Empty,
            IsEnabled = existing is null,
        };
        var valueBox = new TextBox
        {
            Header = isChinese ? "变量值（路径）" : "Value (path)",
            PlaceholderText = @"D:\CloudSync\GameData",
            Text = existing?.Value ?? string.Empty,
        };
        var browsePathButton = new Button
        {
            Content = isChinese ? "选择文件夹" : "Browse Folder",
            Style = (Style)Application.Current.Resources["SecondaryToolbarButtonStyle"],
        };
        browsePathButton.Click += async (_, _) =>
        {
            var selected = await PathPickerService.PickFolderAsync(App.Current.MainWindow);
            if (!string.IsNullOrWhiteSpace(selected))
            {
                valueBox.Text = selected;
            }
        };
        var descriptionBox = new TextBox
        {
            Header = isChinese ? "描述（可选）" : "Description (optional)",
            Text = existing?.Description ?? string.Empty,
        };

        var panel = new StackPanel { Spacing = 12, MaxWidth = 680, HorizontalAlignment = HorizontalAlignment.Stretch };
        panel.Children.Add(nameBox);
        panel.Children.Add(valueBox);
        panel.Children.Add(browsePathButton);
        panel.Children.Add(descriptionBox);

        var validation = new TextBlock { Foreground = (Microsoft.UI.Xaml.Media.Brush)Application.Current.Resources["AppWarningBrush"], TextWrapping = TextWrapping.Wrap, Visibility = Visibility.Collapsed };
        panel.Children.Add(validation);
        var buttons = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8 };
        var save = new Button
        {
            Content = isChinese ? "保存变量" : "Save variable",
            Style = (Style)Application.Current.Resources["PrimaryToolbarButtonStyle"],
        };
        var cancel = new Button { Content = isChinese ? "取消" : "Cancel", Style = (Style)Application.Current.Resources["SecondaryToolbarButtonStyle"] };
        buttons.Children.Add(save);
        buttons.Children.Add(cancel);
        panel.Children.Add(buttons);
        cancel.Click += (_, _) => CloseVariableEditor();
        save.Click += (_, _) =>
        {
            var name = nameBox.Text.Trim().Trim('%').Trim();
            if (string.IsNullOrWhiteSpace(name) || string.IsNullOrWhiteSpace(valueBox.Text))
            {
                validation.Text = isChinese ? "请填写变量名和变量值。" : "Enter a variable name and value.";
                validation.Visibility = Visibility.Visible;
                return;
            }
            if (!isEdit && _viewModel.UserVariableExists(name))
            {
                validation.Text = isChinese ? "该自定义变量已存在，请选择后编辑。" : "This custom variable already exists; select it to edit.";
                validation.Visibility = Visibility.Visible;
                return;
            }
            try
            {
                _viewModel.SaveUserVariable(nameBox.Text, valueBox.Text.Trim(), descriptionBox.Text);
                CloseVariableEditor();
            }
            catch (Exception exception)
            {
                Log.Error(exception, "Failed to save variable.");
                validation.Text = (isChinese ? "保存失败：" : "Could not save: ") + exception.Message;
                validation.Visibility = Visibility.Visible;
            }
        };
        VariableEditorHeading.Text = isEdit ? (isChinese ? "编辑自定义变量" : "Edit custom variable") : (isChinese ? "添加自定义变量" : "Add custom variable");
        VariableEditorContent.Content = panel;
        VariableDetailHeading.Visibility = Visibility.Collapsed;
        VariableEditorHeading.Visibility = Visibility.Visible;
        VariableDetailsScroll.Visibility = Visibility.Collapsed;
        VariableActionsFooter.Visibility = Visibility.Collapsed;
        VariableEditorScroll.Visibility = Visibility.Visible;
        _isEditingVariable = true;
        VariableHeader.IsHitTestVisible = false;
        VariableListPane.IsHitTestVisible = false;
        VariableDetailPane.StartBringIntoView();
    }

    private void CloseVariableEditor()
    {
        VariableEditorContent.Content = null;
        VariableDetailHeading.Visibility = Visibility.Visible;
        VariableEditorHeading.Visibility = Visibility.Collapsed;
        VariableDetailsScroll.Visibility = Visibility.Visible;
        VariableActionsFooter.Visibility = Visibility.Visible;
        VariableEditorScroll.Visibility = Visibility.Collapsed;
        _isEditingVariable = false;
        VariableHeader.IsHitTestVisible = true;
        VariableListPane.IsHitTestVisible = true;
    }

    private async Task ShowInfoAsync(string title, string message)
    {
        var dialog = DialogStyler.Apply(new ContentDialog
        {
            XamlRoot = XamlRoot,
            Title = title,
            Content = message,
            CloseButtonText = _viewModel?.IsChinese == true ? "关闭" : "Close",
        });
        await dialog.ShowAsync();
    }
}

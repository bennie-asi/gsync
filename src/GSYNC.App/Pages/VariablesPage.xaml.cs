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

    public VariablesPage()
    {
        InitializeComponent();
        TryInitializePage();
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
        if (_viewModel is not null && sender is FrameworkElement { DataContext: VariableRow row })
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

    private async void AddVariableButton_Click(object sender, RoutedEventArgs e)
    {
        await ShowVariableEditorAsync(isEdit: false);
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

        await ShowVariableEditorAsync(isEdit: true);
    }

    private async void DeleteVariableButton_Click(object sender, RoutedEventArgs e)
    {
        if (_viewModel is null)
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

    private async void TestTemplateButton_Click(object sender, RoutedEventArgs e)
    {
        if (_viewModel is null)
        {
            return;
        }

        var isChinese = _viewModel.IsChinese;
        var templateBox = new TextBox
        {
            Header = isChinese ? "模板字符串" : "Template String",
            PlaceholderText = "%APPDATA%/MyGame/Saves",
            Text = _viewModel.TemplateInput,
            MinWidth = 380,
        };

        var dialog = DialogStyler.Apply(new ContentDialog
        {
            XamlRoot = XamlRoot,
            Title = isChinese ? "测试路径模板" : "Test Path Template",
            Content = templateBox,
            PrimaryButtonText = isChinese ? "解析" : "Resolve",
            CloseButtonText = isChinese ? "取消" : "Cancel",
            DefaultButton = ContentDialogButton.Primary,
        });

        if (await dialog.ShowAsync() == ContentDialogResult.Primary)
        {
            _viewModel.TestTemplate(templateBox.Text);
        }
    }

    private async Task ShowVariableEditorAsync(bool isEdit)
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

        var panel = new StackPanel { Spacing = 12, MinWidth = 380 };
        panel.Children.Add(nameBox);
        panel.Children.Add(valueBox);
        panel.Children.Add(browsePathButton);
        panel.Children.Add(descriptionBox);

        var dialog = DialogStyler.Apply(new ContentDialog
        {
            XamlRoot = XamlRoot,
            Title = isEdit
                ? (isChinese ? "编辑自定义变量" : "Edit Custom Variable")
                : (isChinese ? "添加自定义变量" : "Add Custom Variable"),
            Content = panel,
            PrimaryButtonText = isChinese ? "保存" : "Save",
            CloseButtonText = isChinese ? "取消" : "Cancel",
            DefaultButton = ContentDialogButton.Primary,
        });

        if (await dialog.ShowAsync() != ContentDialogResult.Primary)
        {
            return;
        }

        if (string.IsNullOrWhiteSpace(nameBox.Text) || string.IsNullOrWhiteSpace(valueBox.Text))
        {
            return;
        }

        _viewModel.SaveUserVariable(nameBox.Text, valueBox.Text.Trim(), descriptionBox.Text);
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

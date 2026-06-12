using GSYNC.App.ViewModels;
using GSYNC.App.Infrastructure;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;

namespace GSYNC.App.Pages;

public sealed partial class AddGameWizardPage : Page
{
    private readonly AddGameWizardViewModel _viewModel;

    public AddGameWizardPage()
    {
        InitializeComponent();
        _viewModel = App.GetService<AddGameWizardViewModel>();
        DataContext = _viewModel;
    }

    private void CancelButton_Click(object sender, RoutedEventArgs e)
    {
        Frame?.Navigate(typeof(HomePage));
    }

    private void BackButton_Click(object sender, RoutedEventArgs e)
    {
        _viewModel.GoBack();
    }

    private async void NextButton_Click(object sender, RoutedEventArgs e)
    {
        var validation = _viewModel.ValidateCurrentStep();
        if (validation is not null)
        {
            await ShowInfoAsync(_viewModel.IsChinese ? "还不能继续" : "Cannot continue yet", validation);
            return;
        }

        switch (_viewModel.CurrentStep)
        {
            case 1:
                if (_viewModel.IsManualSourceSelected)
                {
                    await ShowManualGameDialogAsync();
                    if (_viewModel.ValidateCurrentStep() is not null)
                    {
                        return;
                    }
                }
                else
                {
                    await _viewModel.ScanSelectedSourceAsync();
                }

                _viewModel.GoNext();
                break;
            case 6:
            {
                var error = await _viewModel.CreateProfileAsync();
                if (error is not null)
                {
                    await ShowInfoAsync(_viewModel.IsChinese ? "创建失败" : "Creation failed", error);
                    return;
                }

                if (_viewModel.LastCreatedInstanceId is { } instanceId)
                {
                    Frame?.Navigate(typeof(GameDetailsPage), instanceId.ToString("D"));
                }
                else
                {
                    Frame?.Navigate(typeof(HomePage));
                }

                break;
            }
            default:
                _viewModel.GoNext();
                break;
        }
    }

    private void SwitchToManualSetupButton_Click(object sender, RoutedEventArgs e)
    {
        _viewModel.SwitchToManualSetup();
    }

    private void BackToSourceSelectionButton_Click(object sender, RoutedEventArgs e)
    {
        _viewModel.BackToSourceSelection();
    }

    private void WizardOption_Tapped(object sender, TappedRoutedEventArgs e)
    {
        if (sender is FrameworkElement { DataContext: WizardOption option })
        {
            _viewModel.SelectOption(option);
        }
    }

    private async Task ShowManualGameDialogAsync()
    {
        var isChinese = _viewModel.IsChinese;
        var nameBox = new TextBox
        {
            Header = isChinese ? "游戏名称" : "Game name",
            PlaceholderText = isChinese ? "例如：Elden Ring" : "For example: Elden Ring",
        };
        var pathBox = new TextBox
        {
            Header = isChinese ? "存档目录或文件" : "Save folder or file",
            PlaceholderText = @"C:\Users\You\Documents\My Game\Saves",
        };
        var browseFolderButton = new Button
        {
            Content = isChinese ? "选择文件夹" : "Browse Folder",
            Style = (Style)Application.Current.Resources["SecondaryToolbarButtonStyle"],
        };
        browseFolderButton.Click += async (_, _) =>
        {
            var selected = await PathPickerService.PickFolderAsync(App.Current.MainWindow);
            if (!string.IsNullOrWhiteSpace(selected))
            {
                pathBox.Text = selected;
            }
        };
        var browseFileButton = new Button
        {
            Content = isChinese ? "选择文件" : "Browse File",
            Style = (Style)Application.Current.Resources["SecondaryToolbarButtonStyle"],
        };
        browseFileButton.Click += async (_, _) =>
        {
            var selected = await PathPickerService.PickFileAsync(App.Current.MainWindow);
            if (!string.IsNullOrWhiteSpace(selected))
            {
                pathBox.Text = selected;
            }
        };

        var panel = new StackPanel { Spacing = 12, MinWidth = 420 };
        panel.Children.Add(nameBox);
        panel.Children.Add(pathBox);
        panel.Children.Add(new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = 8,
            Children = { browseFolderButton, browseFileButton },
        });

        var dialog = DialogStyler.Apply(new ContentDialog
        {
            XamlRoot = XamlRoot,
            Title = isChinese ? "手动添加游戏" : "Add game manually",
            Content = panel,
            PrimaryButtonText = isChinese ? "确认" : "Confirm",
            CloseButtonText = isChinese ? "取消" : "Cancel",
            DefaultButton = ContentDialogButton.Primary,
        });

        if (await dialog.ShowAsync() != ContentDialogResult.Primary)
        {
            return;
        }

        if (string.IsNullOrWhiteSpace(nameBox.Text) || string.IsNullOrWhiteSpace(pathBox.Text))
        {
            return;
        }

        _viewModel.SetManualGame(nameBox.Text, pathBox.Text);
    }

    private async Task ShowInfoAsync(string title, string message)
    {
        var dialog = DialogStyler.Apply(new ContentDialog
        {
            XamlRoot = XamlRoot,
            Title = title,
            Content = message,
            CloseButtonText = _viewModel.IsChinese ? "关闭" : "Close",
        });
        await dialog.ShowAsync();
    }
}

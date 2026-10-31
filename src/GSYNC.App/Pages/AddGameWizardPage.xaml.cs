using GSYNC.App.ViewModels;
using GSYNC.App.Infrastructure;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Navigation;

namespace GSYNC.App.Pages;

public sealed partial class AddGameWizardPage : Page
{
    private AddGameWizardViewModel _viewModel;

    public AddGameWizardPage()
    {
        InitializeComponent();
        _viewModel = App.GetService<AddGameWizardViewModel>();
        DataContext = _viewModel;
    }

    protected override void OnNavigatedTo(NavigationEventArgs e)
    {
        base.OnNavigatedTo(e);
        if (e.Parameter is WizardTargetReturn context)
        {
            _viewModel = context.Wizard;
            _viewModel.RefreshTargets(context.SelectedTargetId);
            DataContext = _viewModel;
        }
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
        if (_viewModel.CurrentStep == 4) _viewModel.ApplyPathChanges();
        var validation = _viewModel.ValidateCurrentStep();
        if (validation is not null)
        {
            if (_viewModel.CurrentStep == 4)
            {
                PathValidationText.Text = validation;
                PathValidationText.Visibility = Visibility.Visible;
                PathValidationText.StartBringIntoView();
                return;
            }
            await ShowInfoAsync(_viewModel.IsChinese ? "还不能继续" : "Cannot continue yet", validation);
            return;
        }
        PathValidationText.Visibility = Visibility.Collapsed;

        switch (_viewModel.CurrentStep)
        {
            case 1:
                if (_viewModel.IsManualSourceSelected)
                {
                    var error = await _viewModel.PrepareManualCandidatesAsync(App.Current.LifetimeToken);
                    if (error is not null)
                    {
                        ManualValidationText.Text = error;
                        ManualValidationText.Visibility = Visibility.Visible;
                        ManualValidationText.StartBringIntoView();
                        return;
                    }
                    ManualValidationText.Visibility = Visibility.Collapsed;
                }
                else
                {
                    await _viewModel.ScanSelectedSourceAsync();
                }

                _viewModel.GoNext();
                break;
            case 4 when _viewModel.PathWarningCount > 0:
            {
                var isChinese = _viewModel.IsChinese;
                var warning = DialogStyler.Apply(new ContentDialog
                {
                    XamlRoot = XamlRoot,
                    Title = isChinese ? "路径检查有警告" : "Path check has warnings",
                    Content = isChinese
                        ? $"有 {_viewModel.PathWarningCount} 项路径检查需要注意。可以继续创建，但同步前请确认存档路径和文件是否存在。"
                        : $"There are {_viewModel.PathWarningCount} path warnings. You can continue, but check that the save paths and files exist before syncing.",
                    PrimaryButtonText = isChinese ? "继续" : "Continue",
                    CloseButtonText = isChinese ? "返回检查" : "Review paths",
                    DefaultButton = ContentDialogButton.Close,
                });
                if (await warning.ShowAsync() == ContentDialogResult.Primary) _viewModel.GoNext();
                break;
            }
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
                    if (_viewModel.StartFirstSync && await PrepareFirstSyncAsync()) return;
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

    private void RecheckPath_Click(object sender, RoutedEventArgs e)
    {
        _viewModel.ApplyPathChanges();
        PathValidationText.Visibility = Visibility.Collapsed;
    }

    private void ResetPath_Click(object sender, RoutedEventArgs e)
    {
        if (sender is FrameworkElement { DataContext: WizardPathReviewRow row }) row.OverridePath = string.Empty;
        _viewModel.ApplyPathChanges();
        PathValidationText.Visibility = Visibility.Collapsed;
    }

    private async void BrowseReviewFolder_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not FrameworkElement { DataContext: WizardPathReviewRow row }) return;
        var path = await PathPickerService.PickFolderAsync(App.Current.MainWindow);
        if (path is not null) { row.OverridePath = path; _viewModel.ApplyPathChanges(); PathValidationText.Visibility = Visibility.Collapsed; }
    }

    private async void BrowseReviewFile_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not FrameworkElement { DataContext: WizardPathReviewRow row }) return;
        var path = await PathPickerService.PickFileAsync(App.Current.MainWindow);
        if (path is not null) { row.OverridePath = path; _viewModel.ApplyPathChanges(); PathValidationText.Visibility = Visibility.Collapsed; }
    }

    private async Task<bool> PrepareFirstSyncAsync()
    {
        var (context, error) = await _viewModel.CompareCreatedProfileAsync();
        if (error is not null || context is null)
        {
            await ShowInfoAsync(_viewModel.IsChinese ? "游戏已添加，比较失败" : "Game added; comparison failed", error ?? string.Empty);
            return false;
        }

        var remoteCount = context.Comparison.Entries.Count(entry => entry.RemoteHash is not null);
        var localCount = context.Comparison.Entries.Count(entry => entry.LocalHash is not null);
        var isChinese = _viewModel.IsChinese;
        if (remoteCount > 0)
        {
            var review = DialogStyler.Apply(new ContentDialog
            {
                XamlRoot = XamlRoot,
                Title = isChinese ? "目标上已有存档" : "Saves already exist on the target",
                Content = isChinese
                    ? $"本地发现 {localCount} 个文件，目标上发现 {remoteCount} 个文件。请先比较并选择如何处理。"
                    : $"Found {localCount} local files and {remoteCount} remote files. Review the comparison before choosing what to do.",
                PrimaryButtonText = isChinese ? "查看比较" : "Review comparison",
                CloseButtonText = isChinese ? "稍后处理" : "Decide later",
                DefaultButton = ContentDialogButton.Primary,
            });
            if (await review.ShowAsync() == ContentDialogResult.Primary)
            {
                Frame?.Navigate(typeof(ConflictResolutionPage), context);
                return true;
            }
            return false;
        }

        if (localCount == 0)
        {
            await ShowInfoAsync(isChinese ? "游戏已添加" : "Game added", isChinese
                ? "本地和目标中都没有找到存档文件。请确认路径或先运行游戏生成存档。"
                : "No save files were found locally or on the target. Check the paths or run the game to create a save.");
            return false;
        }

        var upload = DialogStyler.Apply(new ContentDialog
        {
            XamlRoot = XamlRoot,
            Title = isChinese ? "准备首次上传" : "Prepare the first upload",
            Content = isChinese ? $"目标中没有现有存档。本地发现 {localCount} 个文件，是否加入上传队列？" : $"No existing remote saves were found. Queue the {localCount} local files for upload?",
            PrimaryButtonText = isChinese ? "加入队列" : "Queue upload",
            CloseButtonText = isChinese ? "稍后处理" : "Decide later",
            DefaultButton = ContentDialogButton.Close,
        });
        if (await upload.ShowAsync() != ContentDialogResult.Primary) return false;
        var queueError = await _viewModel.QueueFirstUploadAsync();
        if (queueError is not null)
        {
            await ShowInfoAsync(isChinese ? "提交失败" : "Submission failed", queueError);
            return false;
        }
        Frame?.Navigate(typeof(QueuePage));
        return true;
    }

    private void SwitchToManualSetupButton_Click(object sender, RoutedEventArgs e)
    {
        _viewModel.SwitchToManualSetup();
        _viewModel.CurrentStep = 1;
    }

    private void BackToSourceSelectionButton_Click(object sender, RoutedEventArgs e)
    {
        _viewModel.BackToSourceSelection();
    }

    private void WizardOption_Click(object sender, RoutedEventArgs e)
    {
        if (sender is FrameworkElement { DataContext: WizardOption option })
        {
            _viewModel.SelectOption(option);
        }
    }

    private void ManageTargetsButton_Click(object sender, RoutedEventArgs e)
    {
        Frame?.Navigate(typeof(SyncTargetsPage), new WizardTargetSetup(_viewModel));
    }

    private async void BrowseManualFolder_Click(object sender, RoutedEventArgs e)
    {
        var selected = await PathPickerService.PickFolderAsync(App.Current.MainWindow);
        if (!string.IsNullOrWhiteSpace(selected)) _viewModel.ManualSavePath = selected;
    }

    private async void RefreshCatalog_Click(object sender, RoutedEventArgs e)
    {
        RefreshCatalogButton.IsEnabled = false;
        ManualValidationText.Visibility = Visibility.Visible;
        ManualValidationText.Text = _viewModel.IsChinese ? "正在更新游戏资料库，请稍候…" : "Updating the game catalog…";
        try
        {
            var error = await _viewModel.RefreshCatalogAsync(App.Current.LifetimeToken);
            ManualValidationText.Text = error ?? (_viewModel.IsChinese ? "资料库更新已完成，可以按名称查找。" : "Catalog update complete. You can search by name.");
        }
        catch (OperationCanceledException) when (App.Current.LifetimeToken.IsCancellationRequested) { }
        finally { RefreshCatalogButton.IsEnabled = true; }
    }

    private async void BrowseGameProgram_Click(object sender, RoutedEventArgs e)
    {
        var selected = await PathPickerService.PickGameProgramAsync(App.Current.MainWindow);
        if (string.IsNullOrWhiteSpace(selected)) return;
        try
        {
            _viewModel.SetProgramSelection(GSYNC.App.Infrastructure.Wizard.GameProgramSelectionReader.Read(selected));
            ManualValidationText.Visibility = Visibility.Collapsed;
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or ArgumentException)
        {
            ManualValidationText.Text = exception.Message;
            ManualValidationText.Visibility = Visibility.Visible;
        }
    }

    private async void BrowseManualFile_Click(object sender, RoutedEventArgs e)
    {
        var selected = await PathPickerService.PickFileAsync(App.Current.MainWindow);
        if (!string.IsNullOrWhiteSpace(selected)) _viewModel.ManualSavePath = selected;
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

public sealed record WizardTargetSetup(AddGameWizardViewModel Wizard);
public sealed record WizardTargetReturn(AddGameWizardViewModel Wizard, Guid? SelectedTargetId);

using System.Diagnostics;
using GSYNC.App.Primitives;
using GSYNC.App.ViewModels;
using GSYNC.App.Infrastructure;
using GSYNC.App.Infrastructure.Localization;
using GSYNC.Core.Abstractions.Sync;
using GSYNC.Core.Services.Sync;
using GSYNC.Core.Models;
using GSYNC.Core.Models.Manifest;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Navigation;
using Serilog;

namespace GSYNC.App.Pages;

public sealed partial class GameDetailsPage : Page
{
    private readonly GameDetailsViewModel _viewModel;
    private readonly ISyncQueue _syncQueue;
    private readonly AutoPullCoordinator _autoPull;
    private bool _isCurrentPage;
    private RemoteLinkResult? _pendingRemoteLink;

    public GameDetailsPage()
    {
        InitializeComponent();
        _viewModel = App.GetService<GameDetailsViewModel>();
        _syncQueue = App.GetService<ISyncQueue>();
        _autoPull = App.GetService<AutoPullCoordinator>();
        DataContext = _viewModel;
        RemoveGameMenuItem.Text = _viewModel.RemoveGameText;

    }

    protected override void OnNavigatedTo(NavigationEventArgs e)
    {
        base.OnNavigatedTo(e);

        _isCurrentPage = true;
        _syncQueue.QueueChanged += SyncQueue_QueueChanged;
        _autoPull.StatusChanged += AutoPull_StatusChanged;

        if (e.Parameter is GameDetailsEditContext editContext)
        {
            _ = LoadAndEditAsync(editContext.InstanceId);
        }
        else if (e.Parameter is GameTargetReturn targetReturn)
        {
            _ = LoadAndEditDestinationAsync(targetReturn);
        }
        else if (e.Parameter is string parameter && Guid.TryParse(parameter, out var instanceId))
        {
            _ = LoadDetailsAsync(instanceId, false);
        }
    }

    private async Task LoadAndEditAsync(Guid instanceId)
    {
        await LoadDetailsAsync(instanceId, true);
    }

    private async Task LoadAndEditDestinationAsync(GameTargetReturn targetReturn)
    {
        await LoadDetailsAsync(targetReturn.InstanceId, false);
        if (_isCurrentPage && _viewModel.InstanceId == targetReturn.InstanceId)
            ShowDestinationEditor(targetReturn.SelectedTargetId, targetReturn.RemoteNamespace);
    }

    private async Task LoadDetailsAsync(Guid instanceId, bool edit)
    {
        await _viewModel.LoadAsync(instanceId);
        if (!_isCurrentPage || _viewModel.InstanceId != instanceId) return;
        RefreshAutoPullStatus();
        if (edit) ShowGameEditor();
    }

    private void DetailsHeader_SizeChanged(object sender, SizeChangedEventArgs e)
    {
        var narrow = e.NewSize.Width < 850;
        Grid.SetColumn(DetailsHeaderActions, narrow ? 0 : 1);
        Grid.SetRow(DetailsHeaderActions, narrow ? 1 : 0);
        Grid.SetColumnSpan(DetailsHeaderActions, narrow ? 2 : 1);
        DetailsHeaderActions.HorizontalAlignment = narrow ? HorizontalAlignment.Left : HorizontalAlignment.Right;
    }

    private void HistoryFeedsGrid_SizeChanged(object sender, SizeChangedEventArgs e)
    {
        var narrow = e.NewSize.Width < 800;
        SnapshotColumn.Width = narrow ? new GridLength(0) : new GridLength(1, GridUnitType.Star);
        Grid.SetColumn(SnapshotPanel, narrow ? 0 : 1);
        Grid.SetRow(SnapshotPanel, narrow ? 1 : 0);
    }

    private void DestinationOverview_SizeChanged(object sender, SizeChangedEventArgs e)
    {
        var narrow = e.NewSize.Width < 700;
        DestinationArrowColumn.Width = narrow ? new GridLength(0) : GridLength.Auto;
        DestinationTargetColumn.Width = narrow ? new GridLength(0) : new GridLength(1, GridUnitType.Star);
        DestinationArrow.Visibility = narrow ? Visibility.Collapsed : Visibility.Visible;
        Grid.SetColumn(DestinationTargetCard, narrow ? 0 : 2);
        Grid.SetRow(DestinationTargetCard, narrow ? 1 : 0);
    }

    protected override void OnNavigatedFrom(NavigationEventArgs e)
    {
        _isCurrentPage = false;
        _syncQueue.QueueChanged -= SyncQueue_QueueChanged;
        _autoPull.StatusChanged -= AutoPull_StatusChanged;
        _viewModel.StopStatusCheck();
        base.OnNavigatedFrom(e);
    }

    private void AutoPull_StatusChanged(object? sender, EventArgs e) => DispatcherQueue.TryEnqueue(RefreshAutoPullStatus);

    private void RefreshAutoPullStatus()
    {
        if (!_isCurrentPage) return;
        var zh = _viewModel.IsChinese;
        if (!_autoPull.Enabled) { AutoPullStatusText.Text = zh ? "自动拉取已关闭。" : "Automatic pull is off."; return; }
        var status = _autoPull.Statuses.FirstOrDefault(item => item.GameInstanceId == _viewModel.InstanceId);
        AutoPullStatusText.Text = status is null ? (zh ? "等待检查。" : "Waiting to check.") : AutoPullStatusFormatter.Format(status, zh);
    }

    private async void ConfirmBaseline_Click(object sender, RoutedEventArgs e)
    {
        if (_viewModel.InstanceId is not { } id) return;
        ConfirmBaselineButton.IsEnabled = false;
        try
        {
            await _autoPull.ConfirmBaselineAsync(id, App.Current.LifetimeToken);
            AutoPullStatusText.Text = _viewModel.IsChinese ? "共同版本已确认；开启自动拉取后将检查远端变化。" : "Common version confirmed; remote changes will be checked when automatic pull is enabled.";
        }
        catch (OperationCanceledException) when (App.Current.LifetimeToken.IsCancellationRequested) { }
        catch (AutoPullBaselineMismatchException)
        {
            AutoPullStatusText.Text = _viewModel.IsChinese ? "无法确认：本地和远端必须都有文件且内容完全一致。请先比较并处理差异。" : "Cannot confirm: local and remote saves must be non-empty and identical. Compare and resolve the differences first.";
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            AutoPullStatusText.Text = (_viewModel.IsChinese ? "无法确认共同版本：" : "Cannot confirm the common version: ") + exception.Message;
        }
        finally { ConfirmBaselineButton.IsEnabled = true; }
    }

    private void SyncQueue_QueueChanged(object? sender, EventArgs e)
    {
        if (_syncQueue.ActiveJob is not null || _syncQueue.QueueDepth != 0) return;
        DispatcherQueue.TryEnqueue(async () =>
        {
            if (_isCurrentPage && _viewModel.InstanceId is { } id) await _viewModel.LoadAsync(id);
        });
    }

    private void SelectItemDetails_Click(object sender, RoutedEventArgs e)
    {
        if (sender is FrameworkElement { DataContext: ContentItemRow row })
        {
            _viewModel.SelectContentItem(row);
            DetailsInspector.StartBringIntoView();
        }
    }

    private async void RemoveGame_Click(object sender, RoutedEventArgs e)
    {
        if (_viewModel.InstanceId is null) return;
        var zh = _viewModel.IsChinese;
        var dialog = DialogStyler.Apply(new ContentDialog
        {
            XamlRoot = XamlRoot,
            Title = zh ? "从游戏库移除？" : "Remove from library?",
            Content = zh
                ? $"将移除“{_viewModel.Title}”及其同步配置。本机和远端存档、历史记录与快照文件会保留。移除后，旧快照无法在 GSYNC 中恢复。"
                : $"Remove “{_viewModel.Title}” and its sync configuration. Local and remote saves, history, and snapshot files will remain. Old snapshots cannot be restored in GSYNC after removal.",
            PrimaryButtonText = zh ? "移除游戏" : "Remove game",
            CloseButtonText = zh ? "取消" : "Cancel",
            DefaultButton = ContentDialogButton.Close,
        });
        if (await dialog.ShowAsync() != ContentDialogResult.Primary) return;
        var error = await _viewModel.RemoveGameAsync(App.Current.LifetimeToken);
        if (error is null) Frame?.Navigate(typeof(HomePage));
        else await ShowResultAsync(error, string.Empty, string.Empty);
    }

    private async void UploadButton_Click(object sender, RoutedEventArgs e)
    {
        var targets = _viewModel.GetTargetOptions();
        IReadOnlyList<Guid>? selected = null;
        if (targets.Count > 1)
        {
            var panel = new StackPanel { Spacing = 8 };
            panel.Children.Add(new TextBlock { Text = _viewModel.IsChinese ? "选择要上传的目标。每个目标独立检查远端差异。" : "Choose upload targets. Each target is checked independently for remote changes.", TextWrapping = TextWrapping.Wrap });
            var checks = targets.Select(target => new CheckBox { Content = target.Label, Tag = target.Id, IsChecked = target.Id == _viewModel.CurrentTargetId }).ToArray();
            foreach (var check in checks) panel.Children.Add(check);
            var dialog = DialogStyler.Apply(new ContentDialog { XamlRoot = XamlRoot,
                Title = _viewModel.IsChinese ? "选择同步目标" : "Choose sync targets", Content = new ScrollViewer { MaxHeight = 360, Content = panel },
                PrimaryButtonText = _viewModel.IsChinese ? "上传" : "Upload", CloseButtonText = _viewModel.IsChinese ? "取消" : "Cancel",
                DefaultButton = ContentDialogButton.Close });
            if (await dialog.ShowAsync() != ContentDialogResult.Primary) return;
            selected = checks.Where(check => check.IsChecked == true).Select(check => (Guid)check.Tag).ToArray();
            if (selected.Count == 0) { await ShowResultAsync(_viewModel.IsChinese ? "请选择至少一个目标。" : "Choose at least one target.", string.Empty, string.Empty); return; }
        }
        var error = await _viewModel.QueueSyncAsync(SyncDirection.Upload, targetIds: selected);
        await ShowQueueResultAsync(
            error,
            _viewModel.IsChinese ? "上传任务已加入队列" : "Upload queued",
            _viewModel.IsChinese
                ? "每个目标的任务独立执行；如发现远端差异，该目标的上传会停止。"
                : "Each target is processed separately. An upload stops if that target contains remote differences.");
    }

    private async void DownloadButton_Click(object sender, RoutedEventArgs e)
    {
        var (proceed, targetId) = await SelectSingleTargetAsync();
        if (!proceed) return;
        var autoSnapshot = App.GetService<UiSettingsStore>().Load().AutoSnapshotBeforeDownload;
        if (!await ConfirmAsync(
            _viewModel.IsChinese ? "下载远端存档" : "Download remote saves",
            UiFormat.DownloadConfirmationText(autoSnapshot, _viewModel.IsChinese)))
        {
            return;
        }

        var error = await _viewModel.QueueSyncAsync(SyncDirection.Download, targetIds: targetId is { } selected ? new[] { selected } : null);
        await ShowQueueResultAsync(
            error,
            _viewModel.IsChinese ? "下载任务已加入队列" : "Download queued",
            _viewModel.IsChinese
                ? "任务完成后可在历史记录中查看结果。"
                : "Review the result in the history after the job finishes.");
    }

    private async void OpenConflictResolution_Click(object sender, RoutedEventArgs e)
    {
        var (proceed, targetId) = await SelectSingleTargetAsync();
        if (!proceed) return;
        var (context, error) = await _viewModel.CompareAsync(targetId: targetId);
        if (error is not null)
        {
            await ShowResultAsync(error, string.Empty, string.Empty);
            return;
        }

        if (context is not null)
        {
            Frame?.Navigate(typeof(ConflictResolutionPage), context);
        }
    }

    private async Task<(bool Proceed, Guid? TargetId)> SelectSingleTargetAsync()
    {
        var targets = _viewModel.GetTargetOptions();
        if (targets.Count <= 1) return (true, null);
        var choices = new ComboBox { ItemsSource = targets, DisplayMemberPath = nameof(GameTargetOption.Label), HorizontalAlignment = HorizontalAlignment.Stretch,
            SelectedItem = targets.FirstOrDefault(target => target.Id == _viewModel.CurrentTargetId) };
        var dialog = DialogStyler.Apply(new ContentDialog { XamlRoot = XamlRoot,
            Title = _viewModel.IsChinese ? "选择同步目标" : "Choose a sync target", Content = choices,
            PrimaryButtonText = _viewModel.IsChinese ? "继续" : "Continue", CloseButtonText = _viewModel.IsChinese ? "取消" : "Cancel",
            DefaultButton = ContentDialogButton.Close });
        if (await dialog.ShowAsync() != ContentDialogResult.Primary) return (false, null);
        if (choices.SelectedItem is GameTargetOption selected) return (true, selected.Id);
        await ShowResultAsync(_viewModel.IsChinese ? "请选择一个目标。" : "Choose a target.", string.Empty, string.Empty);
        return (false, null);
    }

    private async void RestoreButton_Click(object sender, RoutedEventArgs e)
    {
        var snapshot = _viewModel.Snapshots.FirstOrDefault();
        if (snapshot is null)
        {
            await ShowResultAsync(_viewModel.IsChinese ? "还没有可恢复的快照。" : "There are no snapshots to restore.", string.Empty, string.Empty);
            return;
        }
        await RestoreSnapshotWithConfirmationAsync(snapshot);
    }

    private async void SnapshotFeed_ActionInvoked(object? sender, object item)
    {
        if (item is SnapshotRow snapshot) await RestoreSnapshotWithConfirmationAsync(snapshot);
    }

    private async Task RestoreSnapshotWithConfirmationAsync(SnapshotRow snapshot)
    {
        if (!await ConfirmAsync(
            _viewModel.IsChinese ? "恢复快照" : "Restore snapshot",
            UiFormat.RestoreConfirmationText(_viewModel.Title, snapshot.Name, snapshot.Timestamp, snapshot.FileCount, _viewModel.IsChinese)))
        {
            return;
        }

        var error = await _viewModel.RestoreSnapshotAsync(snapshot);
        await ShowResultAsync(
            error,
            _viewModel.IsChinese ? "快照恢复完成" : "Snapshot restored",
            _viewModel.IsChinese ? "本地文件已恢复到快照内容。" : "Local files were restored from the snapshot.");
    }

    private async void OpenFolderButton_Click(object sender, RoutedEventArgs e)
    {
        SelectItemForAction(sender);
        var path = _viewModel.SelectedContentItem?.ResolvedPath;
        if (string.IsNullOrWhiteSpace(path))
        {
            await ShowResultAsync(_viewModel.IsChinese ? "尚未选择有效的存档路径。" : "No valid save path is selected.", string.Empty, string.Empty);
            return;
        }

        try
        {
            var directory = Directory.Exists(path) ? path : Path.GetDirectoryName(path);
            if (!string.IsNullOrWhiteSpace(directory) && Directory.Exists(directory))
            {
                Process.Start(new ProcessStartInfo("explorer.exe", $"\"{directory}\""));
                return;
            }
            await ShowResultAsync(_viewModel.IsChinese ? $"目录不存在：{path}" : $"Directory does not exist: {path}", string.Empty, string.Empty);
        }
        catch (Exception exception)
        {
            Log.Warning(exception, "Failed to open folder for {Path}.", path);
            await ShowResultAsync((_viewModel.IsChinese ? "无法打开目录：" : "Cannot open directory: ") + exception.Message, string.Empty, string.Empty);
        }
    }

    private async void TestPathButton_Click(object sender, RoutedEventArgs e)
    {
        SelectItemForAction(sender);
        var (exists, message) = _viewModel.TestSelectedPath();
        var dialog = DialogStyler.Apply(new ContentDialog
        {
            XamlRoot = XamlRoot,
            Title = exists
                ? (_viewModel.IsChinese ? "路径检查通过" : "Path check passed")
                : (_viewModel.IsChinese ? "路径检查未通过" : "Path check failed"),
            Content = message,
            CloseButtonText = _viewModel.IsChinese ? "关闭" : "Close",
        });
        await dialog.ShowAsync();
    }

    private async Task<bool> ConfirmAsync(string title, string message)
    {
        var dialog = DialogStyler.Apply(new ContentDialog
        {
            XamlRoot = XamlRoot,
            Title = title,
            Content = message,
            PrimaryButtonText = _viewModel.IsChinese ? "继续" : "Continue",
            CloseButtonText = _viewModel.IsChinese ? "取消" : "Cancel",
            DefaultButton = ContentDialogButton.Close,
        });

        return await dialog.ShowAsync() == ContentDialogResult.Primary;
    }

    private async Task ShowResultAsync(string? error, string successTitle, string successMessage)
    {
        var dialog = DialogStyler.Apply(new ContentDialog
        {
            XamlRoot = XamlRoot,
            Title = error is null ? successTitle : (_viewModel.IsChinese ? "操作失败" : "Operation failed"),
            Content = error ?? successMessage,
            CloseButtonText = _viewModel.IsChinese ? "关闭" : "Close",
        });
        await dialog.ShowAsync();
    }

    private async Task ShowQueueResultAsync(string? error, string title, string message)
    {
        if (error is not null)
        {
            await ShowResultAsync(error, string.Empty, string.Empty);
            return;
        }

        var dialog = DialogStyler.Apply(new ContentDialog
        {
            XamlRoot = XamlRoot,
            Title = title,
            Content = message,
            PrimaryButtonText = _viewModel.IsChinese ? "查看队列" : "View queue",
            CloseButtonText = _viewModel.IsChinese ? "关闭" : "Close",
        });
        if (await dialog.ShowAsync() == ContentDialogResult.Primary)
            Frame?.Navigate(typeof(QueuePage));
    }

    private void SelectItemForAction(object sender)
    {
        if (sender is not FrameworkElement { DataContext: ContentItemRow row }) return;
        var inspectorWasVisible = _viewModel.IsInspectorVisible;
        _viewModel.SelectContentItem(row);
        if (!inspectorWasVisible) _viewModel.HideInspector();
    }

    private void HideInspector_Click(object sender, RoutedEventArgs e) => _viewModel.HideInspector();

    private void ChangeDestination_Click(object sender, RoutedEventArgs e) => ShowDestinationEditor(null, null);

    private void ShowDestinationEditor(Guid? preferredTargetId, string? draftNamespace)
    {
        if (_viewModel.InstanceId is null || GameEditor.Visibility == Visibility.Visible || ContentEditor.Visibility == Visibility.Visible || DestinationEditor.Visibility == Visibility.Visible) return;
        var options = _viewModel.GetTargetOptions();
        _pendingRemoteLink = null;
        DestinationTargetBox.ItemsSource = options;
        DestinationTargetBox.SelectedItem = options.FirstOrDefault(option => option.Id == (preferredTargetId ?? _viewModel.CurrentTargetId));
        DestinationNamespaceBox.Text = draftNamespace ?? _viewModel.CurrentRemoteNamespace;
        DestinationValidation.Text = options.Count == 0
            ? (_viewModel.IsChinese ? "尚无可用目标，请先进入目标管理添加。" : "No targets are available. Add one in target management first.") : string.Empty;
        DestinationMessage.Text = string.Empty;
        DestinationEditor.Visibility = Visibility.Visible;
        DestinationPanel.StartBringIntoView();
    }

    private void CancelDestination_Click(object sender, RoutedEventArgs e)
    {
        _pendingRemoteLink = null;
        DestinationEditor.Visibility = Visibility.Collapsed;
        DestinationValidation.Text = string.Empty;
    }

    private async void SaveDestination_Click(object sender, RoutedEventArgs e)
    {
        if (DestinationTargetBox.SelectedItem is not GameTargetOption selected)
        {
            DestinationValidation.Text = _viewModel.IsChinese ? "请选择一个同步目标。" : "Select a sync target.";
            return;
        }
        var normalizedDraft = DestinationNamespaceBox.Text.Trim().Replace('\\', '/').TrimEnd('/');
        if (_pendingRemoteLink is { } pending && (pending.TargetId != selected.Id || !string.Equals(pending.RemoteNamespace, normalizedDraft, StringComparison.OrdinalIgnoreCase)))
        {
            DestinationValidation.Text = _viewModel.IsChinese ? "目标或目录已更改，请重新关联存档，或取消并重新编辑。" : "The target or folder changed. Link the saves again, or cancel and edit the destination again.";
            return;
        }
        var changed = _pendingRemoteLink is not null || selected.Id != _viewModel.CurrentTargetId || !string.Equals(normalizedDraft, _viewModel.CurrentRemoteNamespace, StringComparison.Ordinal);
        SaveDestinationButton.IsEnabled = false;
        CancelDestinationButton.IsEnabled = false;
        ManageDestinationButton.IsEnabled = false;
        try
        {
            var error = await _viewModel.UpdateDestinationAsync(selected.Id, DestinationNamespaceBox.Text, App.Current.LifetimeToken, _pendingRemoteLink?.Links);
            if (error is not null) { DestinationValidation.Text = error; return; }
            DestinationEditor.Visibility = Visibility.Collapsed;
            if (changed && _viewModel.InstanceId is { } id)
            {
                _autoPull.NotifyDestinationChanged(id, _viewModel.Title);
                DestinationMessage.Text = _viewModel.IsChinese ? "同步位置已更新。请先比较新位置；原有远端存档未移动或删除。" : "Destination updated. Compare the new location first; the old remote saves were not moved or deleted.";
            }
            else DestinationMessage.Text = _viewModel.IsChinese ? "同步位置未改变。" : "The destination is unchanged.";
        }
        finally
        {
            SaveDestinationButton.IsEnabled = true;
            CancelDestinationButton.IsEnabled = true;
            ManageDestinationButton.IsEnabled = true;
        }
    }

    private async void LinkRemote_Click(object sender, RoutedEventArgs e)
    {
        if (DestinationTargetBox.SelectedItem is not GameTargetOption target)
        {
            DestinationValidation.Text = _viewModel.IsChinese ? "请先选择同步目标。" : "Choose a sync target first.";
            return;
        }
        LinkRemoteButton.IsEnabled = false;
        try
        {
            var result = await RemoteLinkDialog.ShowAsync(XamlRoot, target.Id, _viewModel.GetLinkableRoots(), _viewModel.IsChinese, App.Current.LifetimeToken);
            if (result is null) return;
            _pendingRemoteLink = result;
            DestinationNamespaceBox.Text = result.RemoteNamespace;
            DestinationValidation.Text = _viewModel.IsChinese ? "已选择远端存档。保存后请先比较；现有文件不会移动或删除。" : "Remote saves selected. Save and compare; existing files will not be moved or deleted.";
        }
        catch (OperationCanceledException) when (App.Current.LifetimeToken.IsCancellationRequested) { }
        catch (Exception exception) { DestinationValidation.Text = (_viewModel.IsChinese ? "无法关联存档：" : "Cannot link saves: ") + exception.Message; }
        finally { LinkRemoteButton.IsEnabled = true; }
    }

    private void ManageDestination_Click(object sender, RoutedEventArgs e)
    {
        if (_viewModel.InstanceId is { } id)
            Frame?.Navigate(typeof(SyncTargetsPage), new GameTargetSetup(id, DestinationNamespaceBox.Text));
    }

    private void AddContentItem_Click(object sender, RoutedEventArgs e) => ShowContentEditor(null);

    private void EditContentPath_Click(object sender, RoutedEventArgs e)
    {
        if (sender is FrameworkElement { DataContext: ContentItemRow row }) ShowContentEditor(row);
    }

    private async void ToggleContentItem_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not FrameworkElement { DataContext: ContentItemRow row }) return;
        var error = await _viewModel.UpdateContentSelectionAsync(row, !row.Enabled, App.Current.LifetimeToken);
        if (error is not null) await ShowResultAsync(error, _viewModel.IsChinese ? "无法修改内容项" : "Cannot change content item", string.Empty);
        else if (_viewModel.InstanceId is { } id) _autoPull.NotifyDestinationChanged(id, _viewModel.Title);
    }

    private async void RemoveContentItem_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not FrameworkElement { DataContext: ContentItemRow row }) return;
        if (!await ConfirmAsync(_viewModel.IsChinese ? "移除内容项？" : "Remove content item?",
            _viewModel.IsChinese ? $"将从此游戏的同步配置中移除 {row.Name}。本机和远端文件不会删除。" : $"Remove {row.Name} from this game's sync configuration. Local and remote files will remain.")) return;
        var error = await _viewModel.UpdateContentSelectionAsync(row, null, App.Current.LifetimeToken);
        if (error is not null) await ShowResultAsync(error, _viewModel.IsChinese ? "无法移除内容项" : "Cannot remove content item", string.Empty);
        else if (_viewModel.InstanceId is { } id) _autoPull.NotifyDestinationChanged(id, _viewModel.Title);
    }

    private void ShowContentEditor(ContentItemRow? existing)
    {
        if (GameEditor.Visibility == Visibility.Visible || ContentEditor.Visibility == Visibility.Visible || DestinationEditor.Visibility == Visibility.Visible) return;
        var isChinese = _viewModel.IsChinese;
        var panel = ContentEditorContent;
        panel.Children.Clear();
        panel.Children.Add(new TextBlock { Text = existing is null ? (isChinese ? "添加内容项" : "Add content item") : (isChinese ? "编辑存档路径" : "Edit save path"), FontSize = 18 });
        var nameBox = new TextBox { Header = isChinese ? "内容项名称" : "Content item name", PlaceholderText = isChinese ? "例如：附加截图" : "For example: Extra Screenshots", Text = existing?.Name ?? "" };
        if (existing is null) panel.Children.Add(nameBox);
        else panel.Children.Add(new TextBlock { Text = isChinese ? "修改的是此电脑的路径，不会移动文件。保存后请先比较。" : "This changes the path on this computer without moving files. Compare after saving.", TextWrapping = TextWrapping.Wrap });
        var pathBox = new TextBox { Header = isChinese ? "路径模板" : "Path template", PlaceholderText = "%DOCUMENTS%/MyGame/Screenshots", Text = existing?.EffectivePathTemplate ?? "" };
        panel.Children.Add(pathBox);
        var browse = new Button { Content = isChinese ? "选择文件夹" : "Browse folder", Style = (Style)Application.Current.Resources["SecondaryToolbarButtonStyle"] };
        browse.Click += async (_, _) => { var selected = await PathPickerService.PickFolderAsync(App.Current.MainWindow); if (!string.IsNullOrWhiteSpace(selected)) pathBox.Text = selected; };
        panel.Children.Add(browse);
        var message = new TextBlock { TextWrapping = TextWrapping.Wrap, Foreground = (Microsoft.UI.Xaml.Media.Brush)Application.Current.Resources["AppTextSecondaryBrush"] };
        panel.Children.Add(message);
        var buttons = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8 };
        var save = new Button { Content = isChinese ? "保存" : "Save", Style = (Style)Application.Current.Resources["PrimaryToolbarButtonStyle"] };
        var cancel = new Button { Content = isChinese ? "取消" : "Cancel", Style = (Style)Application.Current.Resources["SecondaryToolbarButtonStyle"] };
        cancel.Click += (_, _) => { ContentEditor.Visibility = Visibility.Collapsed; panel.Children.Clear(); };
        save.Click += async (_, _) =>
        {
            save.IsEnabled = false;
            cancel.IsEnabled = false;
            var error = existing is null ? await _viewModel.AddCustomContentItemAsync(nameBox.Text, pathBox.Text) : await _viewModel.UpdateContentPathAsync(existing, pathBox.Text);
            if (error is null) { ContentEditor.Visibility = Visibility.Collapsed; panel.Children.Clear(); }
            else { message.Text = error; save.IsEnabled = true; cancel.IsEnabled = true; }
        };
        buttons.Children.Add(save); buttons.Children.Add(cancel); panel.Children.Add(buttons);
        ContentEditor.Visibility = Visibility.Visible;
        ContentEditor.StartBringIntoView();
        (existing is null ? nameBox : pathBox).Focus(FocusState.Programmatic);
    }

    private void EditGame_Click(object sender, RoutedEventArgs e) => ShowGameEditor();

    private void ShowGameEditor()
    {
        var instance = _viewModel.EditableInstance;
        if (instance is null || GameEditor.Visibility == Visibility.Visible || ContentEditor.Visibility == Visibility.Visible || DestinationEditor.Visibility == Visibility.Visible) return;
        var zh = _viewModel.IsChinese;
        var panel = GameEditorContent;
        panel.Children.Clear();
        panel.Children.Add(new TextBlock { Text = zh ? "编辑游戏" : "Edit game", FontSize = 18 });
        panel.Children.Add(new TextBlock { Text = $"Game ID: {instance.GameId}", TextWrapping = TextWrapping.Wrap });
        var name = new TextBox { Header = zh ? "显示名称" : "Display name", Text = instance.DisplayName };
        var directory = new TextBox { Header = zh ? "安装目录（可选）" : "Install directory (optional)", Text = instance.InstallDirectory ?? "" };
        panel.Children.Add(name); panel.Children.Add(directory);
        var browse = new Button { Content = zh ? "选择文件夹" : "Browse folder", Style = (Style)Application.Current.Resources["SecondaryToolbarButtonStyle"] };
        browse.Click += async (_, _) => { var selected = await PathPickerService.PickFolderAsync(App.Current.MainWindow); if (selected is not null) directory.Text = selected; };
        panel.Children.Add(browse);
        var program = new TextBox { Header = zh ? "游戏程序或快捷方式（可选）" : "Game executable or shortcut (optional)", Text = instance.Variables.GetValueOrDefault(GSYNC.App.Infrastructure.Wizard.GameProgramSelectionReader.ProgramPathVariable, "") };
        var cover = new TextBox { Header = zh ? "本地封面图片（可选）" : "Local cover image (optional)", Text = instance.Variables.GetValueOrDefault(GSYNC.App.Infrastructure.Wizard.GameProgramSelectionReader.ArtworkPathVariable, "") };
        panel.Children.Add(program);
        var browseProgram = new Button { Content = zh ? "选择程序或快捷方式" : "Choose executable or shortcut", Style = (Style)Application.Current.Resources["SecondaryToolbarButtonStyle"] };
        browseProgram.Click += async (_, _) =>
        {
            var selected = await PathPickerService.PickGameProgramAsync(App.Current.MainWindow);
            if (selected is null) return;
            try
            {
                var selection = GSYNC.App.Infrastructure.Wizard.GameProgramSelectionReader.Read(selected);
                program.Text = selection.ExecutablePath ?? selection.SourcePath;
                if (string.IsNullOrWhiteSpace(directory.Text) && selection.InstallDirectory is not null) directory.Text = selection.InstallDirectory;
            }
            catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or ArgumentException) { program.Text = selected; }
        };
        panel.Children.Add(browseProgram);
        panel.Children.Add(cover);
        var browseCover = new Button { Content = zh ? "选择封面图片" : "Choose cover image", Style = (Style)Application.Current.Resources["SecondaryToolbarButtonStyle"] };
        browseCover.Click += async (_, _) => { var selected = await PathPickerService.PickArtworkAsync(App.Current.MainWindow); if (selected is not null) cover.Text = selected; };
        panel.Children.Add(browseCover);
        panel.Children.Add(new TextBlock { Text = zh ? "游戏专属变量（覆盖同名全局变量）" : "Game-specific variables override global variables", TextWrapping = TextWrapping.Wrap });
        var pairs = new List<(TextBox Key, TextBox Value)>();
        var rows = new StackPanel { Spacing = 6 };
        void AddRow(string key, string value)
        {
            var keyBox = new TextBox { Text = key, PlaceholderText = "VARIABLE_NAME", Width = 170 };
            var valueBox = new TextBox { Text = value, PlaceholderText = zh ? "值" : "Value" };
            var pair = (keyBox, valueBox); pairs.Add(pair);
            var row = new Grid { ColumnSpacing = 6 };
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            var remove = new Button { Content = "×", Style = (Style)Application.Current.Resources["SecondaryToolbarButtonStyle"] };
            Grid.SetColumn(valueBox, 1); Grid.SetColumn(remove, 2);
            row.Children.Add(keyBox); row.Children.Add(valueBox); row.Children.Add(remove);
            remove.Click += (_, _) => { pairs.Remove(pair); rows.Children.Remove(row); };
            rows.Children.Add(row);
        }
        foreach (var pair in instance.Variables.Where(p =>
            !p.Key.Equals(GSYNC.App.Infrastructure.Wizard.GameProgramSelectionReader.ProgramPathVariable, StringComparison.OrdinalIgnoreCase) &&
            !p.Key.Equals(GSYNC.App.Infrastructure.Wizard.GameProgramSelectionReader.ArtworkPathVariable, StringComparison.OrdinalIgnoreCase))
            .OrderBy(p => p.Key, StringComparer.OrdinalIgnoreCase)) AddRow(pair.Key, pair.Value);
        panel.Children.Add(rows);
        var add = new Button { Content = zh ? "+ 添加变量" : "+ Add variable", Style = (Style)Application.Current.Resources["SecondaryToolbarButtonStyle"] };
        add.Click += (_, _) => AddRow("", ""); panel.Children.Add(add);
        var message = new TextBlock { TextWrapping = TextWrapping.Wrap }; panel.Children.Add(message);
        var actions = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8 };
        var save = new Button { Content = zh ? "保存" : "Save", Style = (Style)Application.Current.Resources["PrimaryToolbarButtonStyle"] };
        var cancel = new Button { Content = zh ? "取消" : "Cancel", Style = (Style)Application.Current.Resources["SecondaryToolbarButtonStyle"] };
        cancel.Click += (_, _) => { GameEditor.Visibility = Visibility.Collapsed; panel.Children.Clear(); };
        save.Click += async (_, _) =>
        {
            var variables = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            foreach (var (key, value) in pairs)
            {
                var normalized = key.Text.Trim().Trim('%').ToUpperInvariant().Replace(' ', '_');
                if (normalized.Length == 0 && value.Text.Length == 0) continue;
                if (normalized is GSYNC.App.Infrastructure.Wizard.GameProgramSelectionReader.ProgramPathVariable or
                    GSYNC.App.Infrastructure.Wizard.GameProgramSelectionReader.ArtworkPathVariable)
                { message.Text = zh ? "请使用上方的程序或封面字段设置该路径。" : "Use the program or cover field above to set this path."; return; }
                if (normalized.Length == 0 || !variables.TryAdd(normalized, value.Text.Trim()))
                { message.Text = zh ? "变量名称不能为空或重复。" : "Variable names cannot be empty or duplicated."; return; }
            }
            if (!string.IsNullOrWhiteSpace(program.Text))
                variables[GSYNC.App.Infrastructure.Wizard.GameProgramSelectionReader.ProgramPathVariable] = program.Text.Trim();
            if (!string.IsNullOrWhiteSpace(cover.Text))
                variables[GSYNC.App.Infrastructure.Wizard.GameProgramSelectionReader.ArtworkPathVariable] = cover.Text.Trim();
            save.IsEnabled = false; cancel.IsEnabled = false;
            var error = await _viewModel.UpdateGameAsync(name.Text, directory.Text, variables);
            if (error is null) { GameEditor.Visibility = Visibility.Collapsed; panel.Children.Clear(); }
            else { message.Text = error; save.IsEnabled = true; cancel.IsEnabled = true; }
        };
        actions.Children.Add(save); actions.Children.Add(cancel); panel.Children.Add(actions);
        GameEditor.Visibility = Visibility.Visible; GameEditor.StartBringIntoView(); name.Focus(FocusState.Programmatic);
    }

    private void OpenActivityInfoButton_Click(object sender, RoutedEventArgs e)
    {
        if (_viewModel.InstanceId is { } id) Frame?.Navigate(typeof(HistoryPage), id);
    }
}

public sealed record GameDetailsEditContext(Guid InstanceId);
public sealed record GameTargetSetup(Guid InstanceId, string RemoteNamespace);
public sealed record GameTargetReturn(Guid InstanceId, Guid? SelectedTargetId, string RemoteNamespace);

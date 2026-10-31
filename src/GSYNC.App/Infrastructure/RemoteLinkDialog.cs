using GSYNC.App.Infrastructure.Configuration;
using GSYNC.Core.Abstractions;
using GSYNC.Core.Services.Sync;
using GSYNC.Core.Utilities;
using GSYNC.Storage.Services;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace GSYNC.App.Infrastructure;

public sealed record RemoteLinkResult(Guid TargetId, string RemoteNamespace, IReadOnlyList<ContentRootLink> Links);
public sealed record RemoteRootOption(string Key, string Label);

/// <summary>Links a local profile to existing logical saves without changing remote files.</summary>
public static class RemoteLinkDialog
{
    public static async Task<RemoteLinkResult?> ShowAsync(XamlRoot xamlRoot, Guid targetId, IReadOnlyList<LocalContentRoot> localRoots, bool chinese, CancellationToken cancellationToken)
    {
        if (localRoots.Count == 0) throw new InvalidOperationException(chinese ? "请先启用至少一个存档内容项。" : "Enable at least one content item first.");
        var targets = App.GetService<SyncTargetStore>();
        var target = targets.Get(targetId) ?? throw new InvalidOperationException(chinese ? "同步目标已不存在。" : "The sync target no longer exists.");
        var settings = targets.ResolveSettings(target.Settings);
        var provider = App.GetService<IEnumerable<IStorageProvider>>().FirstOrDefault(value =>
            string.Equals(value.ProviderId, target.ProviderId, StringComparison.OrdinalIgnoreCase))
            ?? throw new InvalidOperationException("The storage provider is unavailable.");
        var configured = provider.WithConfiguration(settings);
        var currentPath = string.Empty;
        var active = true;
        var pathText = new TextBlock { Text = "/", TextWrapping = TextWrapping.Wrap, IsTextSelectionEnabled = true };
        var status = new TextBlock { TextWrapping = TextWrapping.Wrap };
        var folders = new ListView { MaxHeight = 250, MinHeight = 100, IsItemClickEnabled = true, SelectionMode = ListViewSelectionMode.None };
        var up = new Button { Content = chinese ? "上一级" : "Up", IsEnabled = false };
        var panel = new StackPanel { Spacing = 10, MinWidth = 340 };
        panel.Children.Add(new TextBlock { Text = chinese ? "打开另一台电脑使用的游戏目录，然后选择该目录。" : "Open the game folder used by the other computer, then select it.", TextWrapping = TextWrapping.Wrap });
        panel.Children.Add(up);
        panel.Children.Add(pathText);
        panel.Children.Add(folders);
        panel.Children.Add(status);
        var browser = DialogStyler.Apply(new ContentDialog
        {
            XamlRoot = xamlRoot, Title = chinese ? "选择已有远端游戏" : "Choose an existing remote game",
            Content = panel, PrimaryButtonText = chinese ? "选择此目录" : "Select this folder",
            CloseButtonText = chinese ? "取消" : "Cancel", IsPrimaryButtonEnabled = false,
        });
        var loading = false;
        async Task LoadAsync(string path)
        {
            if (loading) return;
            loading = true;
            browser.IsPrimaryButtonEnabled = false;
            up.IsEnabled = false;
            folders.Items.Clear();
            status.Text = chinese ? "正在读取目录…" : "Loading folders…";
            try
            {
                IReadOnlyList<string> names;
                if (provider is WebDavStorageProvider webDav)
                    names = (await webDav.BrowseDirectoriesAsync(settings, path, cancellationToken)).Select(entry => entry.Path).ToArray();
                else if (configured.LocalRootPath is { } root)
                {
                    var folder = SafePath.ResolveUnderRoot(root, path, allowRoot: true);
                    names = Directory.EnumerateDirectories(folder)
                        .Where(value => (File.GetAttributes(value) & FileAttributes.ReparsePoint) == 0)
                        .Select(Path.GetFileName).OfType<string>().ToArray();
                }
                else throw new NotSupportedException(chinese ? "此目标不支持浏览目录。" : "This target does not support browsing folders.");
                if (!active) return;
                currentPath = path;
                pathText.Text = "/" + path;
                foreach (var name in names.Where(name => !name.Equals("__gsync_meta", StringComparison.OrdinalIgnoreCase) &&
                                                        !name.Equals("__gsync_versions", StringComparison.OrdinalIgnoreCase))
                             .OrderBy(name => name, StringComparer.OrdinalIgnoreCase))
                    folders.Items.Add(name);
                status.Text = folders.Items.Count == 0 ? (chinese ? "此目录没有子目录。" : "This folder has no subfolders.") : string.Empty;
                browser.IsPrimaryButtonEnabled = currentPath.Length > 0;
            }
            catch (Exception exception) when (exception is not OperationCanceledException)
            {
                if (active) status.Text = (chinese ? "无法读取目录：" : "Could not load folders: ") + exception.Message;
            }
            finally { loading = false; if (active) up.IsEnabled = currentPath.Length > 0; }
        }
        browser.Opened += async (_, _) => await LoadAsync(string.Empty);
        folders.ItemClick += async (_, args) =>
        {
            if (args.ClickedItem is string name)
                await LoadAsync(currentPath.Length == 0 ? name : currentPath + "/" + name);
        };
        up.Click += async (_, _) =>
        {
            var slash = currentPath.LastIndexOf('/');
            await LoadAsync(slash < 0 ? string.Empty : currentPath[..slash]);
        };
        var browserResult = await browser.ShowAsync();
        active = false;
        if (browserResult != ContentDialogResult.Primary) return null;

        var remoteRoots = await RemoteContentLayout.ListRootsAsync(configured, currentPath, cancellationToken);
        if (remoteRoots.Count == 0)
            throw new InvalidOperationException(chinese ? "此目录中没有可关联的存档。请选择另一台电脑上传时使用的游戏目录。" : "No saves were found in this folder. Choose the game folder used for the other computer's upload.");

        var choices = new List<(LocalContentRoot Root, ComboBox Box)>();
        var matching = new StackPanel { Spacing = 12, MinWidth = 340 };
        matching.Children.Add(new TextBlock
        {
            Text = chinese ? "确认每个本机存档对应的远端内容。关联只修改配置，之后请先比较存档。" : "Match each local save to remote content. Linking only changes the configuration; compare saves before syncing.",
            TextWrapping = TextWrapping.Wrap,
        });
        foreach (var local in localRoots)
        {
            var options = remoteRoots.Select(root => new RemoteRootOption(root, root)).ToList();
            if (!remoteRoots.Contains(local.DefaultRemoteRoot, StringComparer.OrdinalIgnoreCase))
                options.Add(new RemoteRootOption(local.DefaultRemoteRoot, (chinese ? "作为新内容：" : "New content: ") + local.DefaultRemoteRoot));
            var box = new ComboBox
            {
                Header = local.ContentId + "  ·  " + local.LocalPath,
                ItemsSource = options, DisplayMemberPath = nameof(RemoteRootOption.Label), HorizontalAlignment = HorizontalAlignment.Stretch,
            };
            box.SelectedItem = options.FirstOrDefault(option => string.Equals(option.Key, local.DefaultRemoteRoot, StringComparison.OrdinalIgnoreCase) &&
                remoteRoots.Contains(option.Key, StringComparer.OrdinalIgnoreCase))
                ?? (localRoots.Count == 1 && remoteRoots.Count == 1 ? options[0] : null);
            choices.Add((local, box));
            matching.Children.Add(box);
        }
        var validation = new TextBlock { TextWrapping = TextWrapping.Wrap };
        matching.Children.Add(validation);
        var matchDialog = DialogStyler.Apply(new ContentDialog
        {
            XamlRoot = xamlRoot, Title = chinese ? "对应存档内容" : "Match save content",
            Content = new ScrollViewer { Content = matching, MaxHeight = 440, HorizontalScrollMode = ScrollMode.Disabled },
            PrimaryButtonText = chinese ? "关联存档" : "Link saves", CloseButtonText = chinese ? "取消" : "Cancel",
        });
        matchDialog.PrimaryButtonClick += (_, args) =>
        {
            if (choices.Any(choice => choice.Box.SelectedItem is not RemoteRootOption))
            {
                validation.Text = chinese ? "请为每个本机内容项选择对应位置。" : "Choose a location for each local content item.";
                args.Cancel = true;
                return;
            }
            try { RemoteContentLayout.ValidateDistinctRoots(choices.Select(choice => ((RemoteRootOption)choice.Box.SelectedItem).Key)); }
            catch (InvalidDataException)
            {
                validation.Text = chinese ? "不同内容项不能使用相同或重叠的远端位置。" : "Content items cannot use the same or overlapping remote locations.";
                args.Cancel = true;
            }
        };
        if (await matchDialog.ShowAsync() != ContentDialogResult.Primary) return null;
        return new RemoteLinkResult(targetId, currentPath, choices.Select(choice =>
            new ContentRootLink(choice.Root.ContentId, choice.Root.Template, ((RemoteRootOption)choice.Box.SelectedItem).Key)).ToArray());
    }
}

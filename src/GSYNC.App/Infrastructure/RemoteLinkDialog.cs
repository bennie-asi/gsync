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

        return await MatchAsync(xamlRoot, targetId, currentPath, configured, localRoots, chinese, cancellationToken);
    }

    public static Task<RemoteLinkResult?> ShowCandidateAsync(XamlRoot xamlRoot, Guid targetId, string remoteNamespace,
        IReadOnlyList<LocalContentRoot> localRoots, bool chinese, CancellationToken cancellationToken)
    {
        var targets = App.GetService<SyncTargetStore>();
        var target = targets.Get(targetId) ?? throw new InvalidOperationException("The selected sync target no longer exists.");
        var provider = App.GetService<IEnumerable<IStorageProvider>>().First(value => string.Equals(value.ProviderId, target.ProviderId, StringComparison.OrdinalIgnoreCase));
        return MatchAsync(xamlRoot, targetId, remoteNamespace, provider.WithConfiguration(targets.ResolveSettings(target.Settings)), localRoots, chinese, cancellationToken);
    }

    private static async Task<RemoteLinkResult?> MatchAsync(XamlRoot xamlRoot, Guid targetId, string currentPath, IStorageProvider configured,
        IReadOnlyList<LocalContentRoot> localRoots, bool chinese, CancellationToken cancellationToken)
    {
        var remoteFiles = await RemoteContentLayout.ListFilesAsync(configured, currentPath, cancellationToken);
        var remoteRoots = remoteFiles.Keys.OrderBy(root => root, StringComparer.OrdinalIgnoreCase).ToArray();
        if (remoteRoots.Length == 0)
            throw new InvalidOperationException(chinese ? "此目录中没有可关联的存档。请选择另一台电脑上传时使用的游戏目录。" : "No saves were found in this folder. Choose the game folder used for the other computer's upload.");

        var descriptor = await RemoteGameCatalog.ReadAsync(configured, currentPath, cancellationToken);

        var choices = new List<(LocalContentRoot Root, ComboBox Box, TextBox Subdirectory)>();
        var matching = new StackPanel { Spacing = 12, MinWidth = 340 };
        matching.Children.Add(new TextBlock
        {
            Text = chinese ? "确认每个本机存档对应的远端内容。若远端多一层目录，可填写远端子目录；关联后请先比较。" : "Match each local save to remote content. If the remote save has an extra folder level, enter that subdirectory. Compare before syncing.",
            TextWrapping = TextWrapping.Wrap,
        });
        foreach (var local in localRoots)
        {
            var options = remoteRoots.Select(root => new RemoteRootOption(root, root + "  ·  " + string.Join(", ", remoteFiles[root].Take(2)))).ToList();
            if (!remoteRoots.Contains(local.DefaultRemoteRoot, StringComparer.OrdinalIgnoreCase))
                options.Add(new RemoteRootOption(local.DefaultRemoteRoot, (chinese ? "作为新内容：" : "New content: ") + local.DefaultRemoteRoot));
            var box = new ComboBox
            {
                Header = local.ContentId + "  ·  " + local.LocalPath,
                ItemsSource = options, DisplayMemberPath = nameof(RemoteRootOption.Label), HorizontalAlignment = HorizontalAlignment.Stretch,
            };
            var subdirectory = new TextBox { Header = chinese ? "远端子目录（可选）" : "Remote subdirectory (optional)",
                PlaceholderText = chinese ? "例如：win64_save；无额外层级时留空" : "For example: win64_save; leave empty if none" };
            var hint = new TextBlock { TextWrapping = TextWrapping.Wrap, Opacity = 0.8 };
            var localFiles = await Task.Run(() => GetLocalFiles(local.LocalPath), cancellationToken);
            box.SelectionChanged += (_, _) =>
            {
                if (box.SelectedItem is not RemoteRootOption option || !remoteFiles.TryGetValue(option.Key, out var files)) return;
                var suggestion = RemoteContentLayout.SuggestRemoteSubdirectory(localFiles, files);
                subdirectory.Text = suggestion ?? string.Empty;
                hint.Text = suggestion is { Length: > 0 }
                    ? (chinese ? "根据文件名建议使用此子目录，请确认层级。" : "Suggested from matching file names; confirm the folder level.")
                    : (chinese ? "远端示例：" : "Remote examples: ") + string.Join("、", files.Take(3));
            };
            var exactTemplate = descriptor?.Roots.Where(root => root.TemplateFingerprint == RemoteContentLayout.TemplateFingerprint(local.Template) &&
                remoteRoots.Contains(root.StorageRoot, StringComparer.OrdinalIgnoreCase)).ToArray() ?? [];
            var exactContent = descriptor?.Roots.Where(root => string.Equals(root.ContentId, local.ContentId, StringComparison.OrdinalIgnoreCase) &&
                remoteRoots.Contains(root.StorageRoot, StringComparer.OrdinalIgnoreCase)).ToArray() ?? [];
            var recommended = exactTemplate.Length == 1 ? exactTemplate[0].StorageRoot : exactContent.Length == 1 ? exactContent[0].StorageRoot : local.DefaultRemoteRoot;
            box.SelectedItem = options.FirstOrDefault(option => string.Equals(option.Key, recommended, StringComparison.OrdinalIgnoreCase) &&
                remoteRoots.Contains(option.Key, StringComparer.OrdinalIgnoreCase))
                ?? (localRoots.Count == 1 && remoteRoots.Length == 1 ? options[0] : null);
            choices.Add((local, box, subdirectory));
            matching.Children.Add(box);
            matching.Children.Add(subdirectory);
            matching.Children.Add(hint);
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
            try { RemoteContentLayout.ValidateDistinctStoragePaths(choices.Select(choice => RemoteContentLayout.MappedStoragePath(((RemoteRootOption)choice.Box.SelectedItem).Key, choice.Subdirectory.Text))); }
            catch (Exception exception) when (exception is InvalidDataException or ArgumentException)
            {
                validation.Text = chinese ? "请检查子目录：不能包含 .. 或无效字符，不同内容项也不能使用相同或重叠的位置。" : "Check subdirectories: .. and invalid characters are not allowed; content locations cannot overlap.";
                args.Cancel = true;
            }
        };
        if (await matchDialog.ShowAsync() != ContentDialogResult.Primary) return null;
        return new RemoteLinkResult(targetId, currentPath, choices.Select(choice =>
            new ContentRootLink(choice.Root.ContentId, choice.Root.Template, ((RemoteRootOption)choice.Box.SelectedItem).Key, choice.Subdirectory.Text)).ToArray());
    }

    private static IReadOnlyList<string> GetLocalFiles(string path)
    {
        try
        {
            if (File.Exists(path)) return [Path.GetFileName(path)];
            if (!Directory.Exists(path)) return [];
            return GSYNC.Core.Utilities.SafePath.EnumerateTree(path).Where(File.Exists)
                .Select(file => Path.GetRelativePath(path, file).Replace('\\', '/')).Take(513).ToArray();
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or ArgumentException) { return []; }
    }
}

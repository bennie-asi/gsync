using GSYNC.Core.Abstractions;
using GSYNC.Core.Models.Manifest;
using GSYNC.Core.Utilities;

namespace GSYNC.Core.Services.Sync;

public sealed record LocalContentRoot(string ContentId, string Template, string LocalPath, string DefaultRemoteRoot);
public sealed record ContentRootLink(string ContentId, string Template, string RemoteRoot, string RemoteSubdirectory = "");

/// <summary>Discovers logical content roots without exposing immutable storage generations.</summary>
public static class RemoteContentLayout
{
    public static IReadOnlyList<LocalContentRoot> LocalRoots(GameContentDefinition definition, IReadOnlyList<ContentSelection> selections) =>
        definition.ContentItems.SelectMany(item =>
        {
            var selection = selections.FirstOrDefault(value => string.Equals(value.ContentId, item.ContentId, StringComparison.OrdinalIgnoreCase));
            var enabled = selections.Count == 0 ? item.DefaultEnabled : selection?.IsEnabled == true;
            var templates = item.PathTemplates.Select(path => path.Replace('\\', '/')).Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
            return !enabled ? Array.Empty<LocalContentRoot>() : templates.Where(template => selection?.IsTemplateEnabled(template) != false)
                .Select(template => new LocalContentRoot(item.ContentId, template, selection?.EffectiveTemplate(template) ?? template,
                    DefaultRootKey(item.ContentId, template, templates.Length))).ToArray();
        }).ToArray();

    public static IReadOnlyList<ContentSelection> ApplyLinks(GameContentDefinition definition, IReadOnlyList<ContentSelection> selections, IReadOnlyList<ContentRootLink> links)
    {
        var roots = LocalRoots(definition, selections);
        if (links.Count != roots.Count || roots.Any(root => links.Count(link =>
            string.Equals(link.ContentId, root.ContentId, StringComparison.OrdinalIgnoreCase) &&
            string.Equals(link.Template.Replace('\\', '/'), root.Template, StringComparison.OrdinalIgnoreCase)) != 1))
            throw new InvalidDataException("Choose a remote content root for each enabled local content item.");
        ValidateDistinctStoragePaths(links.Select(link => MappedStoragePath(link.RemoteRoot, link.RemoteSubdirectory)));
        return definition.ContentItems.Select(item =>
        {
            var previous = selections.FirstOrDefault(value => string.Equals(value.ContentId, item.ContentId, StringComparison.OrdinalIgnoreCase));
            return new ContentSelection
            {
                ContentId = item.ContentId, IsEnabled = selections.Count == 0 ? item.DefaultEnabled : previous?.IsEnabled == true,
                PathOverrides = previous?.PathOverrides ?? new Dictionary<string, string>(),
                DisabledPathTemplates = previous?.DisabledPathTemplates ?? Array.Empty<string>(),
                RemovedPathTemplates = previous?.RemovedPathTemplates ?? Array.Empty<string>(),
                RemoteRootOverrides = links.Where(link => string.Equals(link.ContentId, item.ContentId, StringComparison.OrdinalIgnoreCase))
                    .ToDictionary(link => link.Template, link => ValidateRootKey(link.RemoteRoot), StringComparer.OrdinalIgnoreCase),
                RemoteSubdirectoryOverrides = links.Where(link => string.Equals(link.ContentId, item.ContentId, StringComparison.OrdinalIgnoreCase) && !string.IsNullOrWhiteSpace(link.RemoteSubdirectory))
                    .ToDictionary(link => link.Template, link => ValidateSubdirectory(link.RemoteSubdirectory), StringComparer.OrdinalIgnoreCase),
            };
        }).ToArray();
    }

    public static string DefaultRootKey(string contentId, string template, int templateCount) =>
        ValidateRootKey(templateCount == 1 ? contentId : contentId + "/__roots/" + SyncFileService.GetRootId(template));

    public static string TemplateFingerprint(string template) => SyncFileService.GetRootId(template);

    public static string ValidateRootKey(string key)
    {
        var normalized = SafePath.ValidateRelative(key);
        var parts = normalized.Split('/');
        if (parts.Any(string.IsNullOrWhiteSpace) || IsReserved(parts[0]) ||
            !(parts.Length == 1 || (parts.Length == 3 && parts[1].Equals("__roots", StringComparison.OrdinalIgnoreCase) &&
                                  parts[2].Length == 16 && parts[2].All(Uri.IsHexDigit))))
            throw new InvalidDataException("The remote content root is invalid or reserved.");
        return normalized;
    }

    public static void ValidateDistinctRoots(IEnumerable<string> roots)
    {
        ValidateDistinctStoragePaths(roots.Select(ValidateRootKey));
    }

    public static string ValidateSubdirectory(string path)
    {
        if (string.IsNullOrWhiteSpace(path)) return string.Empty;
        var normalized = SafePath.ValidateRelative(path.Trim()).TrimEnd('/');
        if (normalized.Split('/').Any(string.IsNullOrWhiteSpace)) throw new InvalidDataException("Invalid remote subdirectory.");
        return normalized;
    }

    public static string MappedStoragePath(string root, string subdirectory)
    {
        var normalizedRoot = ValidateRootKey(root);
        var normalizedSubdirectory = ValidateSubdirectory(subdirectory);
        return normalizedSubdirectory.Length == 0 ? normalizedRoot : normalizedRoot + "/" + normalizedSubdirectory;
    }

    public static void ValidateDistinctStoragePaths(IEnumerable<string> roots)
    {
        var keys = roots.Select(value => SafePath.ValidateRelative(value)).ToArray();
        for (var i = 0; i < keys.Length; i++)
            for (var j = i + 1; j < keys.Length; j++)
                if (keys[i].Equals(keys[j], StringComparison.OrdinalIgnoreCase) ||
                    keys[i].StartsWith(keys[j] + "/", StringComparison.OrdinalIgnoreCase) ||
                    keys[j].StartsWith(keys[i] + "/", StringComparison.OrdinalIgnoreCase))
                    throw new InvalidDataException("Two local content items cannot use the same or overlapping remote content roots.");
    }

    public static async Task<IReadOnlyList<string>> ListRootsAsync(IStorageProvider storage, string remoteNamespace, CancellationToken cancellationToken)
        => (await ListFilesAsync(storage, remoteNamespace, cancellationToken)).Keys.OrderBy(value => value, StringComparer.OrdinalIgnoreCase).ToArray();

    public static async Task<IReadOnlyDictionary<string, IReadOnlyList<string>>> ListFilesAsync(IStorageProvider storage, string remoteNamespace, CancellationToken cancellationToken)
    {
        var prefix = SafePath.ValidateRelative(remoteNamespace).TrimEnd('/') + "/";
        var session = await RemoteSaveSession.OpenAsync(storage, prefix.TrimEnd('/'), cancellationToken);
        var entries = await session.ListAsync(prefix.TrimEnd('/'), cancellationToken);
        var roots = new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase);
        foreach (var entry in entries.Where(entry => !entry.IsDirectory && entry.Path.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)))
        {
            var parts = entry.Path[prefix.Length..].Split('/');
            if (parts.Length < 2 || IsReserved(parts[0])) continue;
            var key = parts.Length >= 4 && parts[1].Equals("__roots", StringComparison.OrdinalIgnoreCase)
                ? string.Join('/', parts.Take(3)) : parts[0];
            key = ValidateRootKey(key);
            if (!roots.TryGetValue(key, out var paths)) roots[key] = paths = [];
            var relative = string.Join('/', parts.Skip(key.Contains('/') ? 3 : 1));
            if (relative.Length > 0 && paths.Count < 513) paths.Add(relative);
        }
        return roots.ToDictionary(pair => pair.Key, pair => (IReadOnlyList<string>)pair.Value, StringComparer.OrdinalIgnoreCase);
    }

    public static string? SuggestRemoteSubdirectory(IEnumerable<string> localFiles, IEnumerable<string> remoteFiles)
    {
        var local = localFiles.Select(path => SafePath.ValidateRelative(path)).Distinct(StringComparer.OrdinalIgnoreCase).Take(513).ToArray();
        var remote = remoteFiles.Select(path => SafePath.ValidateRelative(path)).Distinct(StringComparer.OrdinalIgnoreCase).Take(513).ToArray();
        if (local.Length == 0 || local.Length > 512 || remote.Length > 512) return null;
        var remoteSet = remote.ToHashSet(StringComparer.OrdinalIgnoreCase);
        if (local.All(remoteSet.Contains)) return string.Empty;
        var candidates = remote.Where(path => path.EndsWith("/" + local[0], StringComparison.OrdinalIgnoreCase))
            .Select(path => path[..^(local[0].Length + 1)])
            .Where(prefix => local.All(path => remoteSet.Contains(prefix + "/" + path)))
            .Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
        return candidates.Length == 1 ? ValidateSubdirectory(candidates[0]) : null;
    }

    private static bool IsReserved(string segment) => segment.Equals("__gsync_meta", StringComparison.OrdinalIgnoreCase) ||
        segment.Equals("__gsync_versions", StringComparison.OrdinalIgnoreCase);
}

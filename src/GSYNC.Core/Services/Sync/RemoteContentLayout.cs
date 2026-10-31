using GSYNC.Core.Abstractions;
using GSYNC.Core.Models.Manifest;
using GSYNC.Core.Utilities;

namespace GSYNC.Core.Services.Sync;

public sealed record LocalContentRoot(string ContentId, string Template, string LocalPath, string DefaultRemoteRoot);
public sealed record ContentRootLink(string ContentId, string Template, string RemoteRoot);

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
        ValidateDistinctRoots(links.Select(link => link.RemoteRoot));
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
            };
        }).ToArray();
    }

    public static string DefaultRootKey(string contentId, string template, int templateCount) =>
        ValidateRootKey(templateCount == 1 ? contentId : contentId + "/__roots/" + SyncFileService.GetRootId(template));

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
        var keys = roots.Select(ValidateRootKey).ToArray();
        for (var i = 0; i < keys.Length; i++)
            for (var j = i + 1; j < keys.Length; j++)
                if (keys[i].Equals(keys[j], StringComparison.OrdinalIgnoreCase) ||
                    keys[i].StartsWith(keys[j] + "/", StringComparison.OrdinalIgnoreCase) ||
                    keys[j].StartsWith(keys[i] + "/", StringComparison.OrdinalIgnoreCase))
                    throw new InvalidDataException("Two local content items cannot use the same or overlapping remote content roots.");
    }

    public static async Task<IReadOnlyList<string>> ListRootsAsync(IStorageProvider storage, string remoteNamespace, CancellationToken cancellationToken)
    {
        var prefix = SafePath.ValidateRelative(remoteNamespace).TrimEnd('/') + "/";
        var session = await RemoteSaveSession.OpenAsync(storage, prefix.TrimEnd('/'), cancellationToken);
        var entries = await session.ListAsync(prefix.TrimEnd('/'), cancellationToken);
        var roots = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var entry in entries.Where(entry => !entry.IsDirectory && entry.Path.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)))
        {
            var parts = entry.Path[prefix.Length..].Split('/');
            if (parts.Length < 2 || IsReserved(parts[0])) continue;
            var key = parts.Length >= 4 && parts[1].Equals("__roots", StringComparison.OrdinalIgnoreCase)
                ? string.Join('/', parts.Take(3)) : parts[0];
            roots.Add(ValidateRootKey(key));
        }
        return roots.OrderBy(value => value, StringComparer.OrdinalIgnoreCase).ToArray();
    }

    private static bool IsReserved(string segment) => segment.Equals("__gsync_meta", StringComparison.OrdinalIgnoreCase) ||
        segment.Equals("__gsync_versions", StringComparison.OrdinalIgnoreCase);
}

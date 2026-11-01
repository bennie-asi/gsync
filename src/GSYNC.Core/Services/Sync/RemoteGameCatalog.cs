using System.Net;
using System.Text.Json;
using GSYNC.Core.Abstractions;
using GSYNC.Core.Models;
using GSYNC.Core.Models.Manifest;
using GSYNC.Core.Utilities;

namespace GSYNC.Core.Services.Sync;

public sealed record RemoteGameRoot(string StorageRoot, string ContentId, string TemplateFingerprint, bool IsDirectory);
public sealed record RemoteGameDescriptor(int Version, string Name, string[] Identities, RemoteGameRoot[] Roots);

/// <summary>Portable game identifiers and content roots stored with a committed save generation.</summary>
public static class RemoteGameCatalog
{
    public const string MetadataName = "__gsync_meta/game.json";

    public static string[] Identities(GameInstance game, GameContentDefinition definition)
    {
        var ids = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        if (definition.GameId.StartsWith("ludusavi-", StringComparison.OrdinalIgnoreCase))
            ids.Add("catalog:" + definition.GameId.ToLowerInvariant());
        else if (definition.GameId.StartsWith("custom-", StringComparison.OrdinalIgnoreCase))
            ids.Add("name:" + definition.GameId.ToLowerInvariant());
        if (definition.SourceHints.TryGetValue("steam.id", out var steamId) && IsSteamId(steamId)) ids.Add("steam:" + steamId);
        if ((game.SourceProviderId.Equals("steam", StringComparison.OrdinalIgnoreCase) ||
             game.SourceProviderId.Equals("custom", StringComparison.OrdinalIgnoreCase)) && IsSteamId(game.PlatformInstanceId))
            ids.Add("steam:" + game.PlatformInstanceId);
        if (game.SourceProviderId.Equals("epic", StringComparison.OrdinalIgnoreCase) &&
            game.PlatformInstanceId is { Length: > 0 and <= 128 } epic && epic.All(character => char.IsAsciiLetterOrDigit(character) || character is '-' or '_'))
            ids.Add("epic:" + epic.ToLowerInvariant());
        return ids.OrderBy(value => value, StringComparer.Ordinal).ToArray();
    }

    public static bool IsStrongMatch(IEnumerable<string> first, IEnumerable<string> second)
    {
        var left = first.ToHashSet(StringComparer.OrdinalIgnoreCase);
        var right = second.ToHashSet(StringComparer.OrdinalIgnoreCase);
        if (HaveConflictingStrongIdentities(left, right)) return false;
        return left.Intersect(right, StringComparer.OrdinalIgnoreCase).Any(value => !value.StartsWith("name:", StringComparison.OrdinalIgnoreCase));
    }

    public static bool IsNameMatch(IEnumerable<string> first, IEnumerable<string> second) =>
        !HaveConflictingStrongIdentities(first, second) &&
        first.Intersect(second, StringComparer.OrdinalIgnoreCase).Any(value => value.StartsWith("name:", StringComparison.OrdinalIgnoreCase));

    private static bool HaveConflictingStrongIdentities(IEnumerable<string> first, IEnumerable<string> second)
    {
        foreach (var prefix in new[] { "catalog:", "steam:", "epic:" })
        {
            var left = first.Where(value => value.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)).ToArray();
            var right = second.Where(value => value.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)).ToArray();
            if (left.Length > 0 && right.Length > 0 && !left.Intersect(right, StringComparer.OrdinalIgnoreCase).Any()) return true;
        }
        return false;
    }

    public static async Task<RemoteGameDescriptor?> ReadAsync(IStorageProvider storage, string remoteNamespace, CancellationToken cancellationToken)
    {
        var session = await RemoteSaveSession.OpenAsync(storage, remoteNamespace, cancellationToken);
        return await ReadAsync(session, remoteNamespace, cancellationToken);
    }

    internal static async Task<RemoteGameDescriptor?> ReadAsync(RemoteSaveSession session, string remoteNamespace, CancellationToken cancellationToken)
    {
        try
        {
            var descriptor = await session.ReadMetadataAsync<RemoteGameDescriptor>(remoteNamespace.TrimEnd('/') + "/" + MetadataName, cancellationToken);
            if (descriptor is null || descriptor.Version != 1 || string.IsNullOrWhiteSpace(descriptor.Name) || descriptor.Name.Length > 240 ||
                descriptor.Identities is null || descriptor.Identities.Length > 32 || descriptor.Roots is null || descriptor.Roots.Length > 512 ||
                descriptor.Identities.Any(value => string.IsNullOrWhiteSpace(value) || value.Length > 160 || value.Any(char.IsControl)) ||
                descriptor.Roots.Any(root => root is null || string.IsNullOrWhiteSpace(root.ContentId) || root.ContentId.Length > 160 ||
                    root.TemplateFingerprint is not { Length: 16 } || !root.TemplateFingerprint.All(Uri.IsHexDigit)))
                throw new InvalidDataException("Invalid remote game identification metadata.");
            foreach (var root in descriptor.Roots) RemoteContentLayout.ValidateRootKey(root.StorageRoot);
            return descriptor;
        }
        catch (FileNotFoundException) { return null; }
        catch (DirectoryNotFoundException) { return null; }
        catch (HttpRequestException exception) when (exception.StatusCode == HttpStatusCode.NotFound) { return null; }
        catch (JsonException exception) { throw new InvalidDataException("Invalid remote game identification metadata.", exception); }
    }

    internal static async Task WriteAsync(SyncExecutionContext context, IReadOnlyList<ResolvedLocalFile> files, CancellationToken cancellationToken)
    {
        if (files.Count == 0) return;
        var session = (RemoteSaveSession)context.StorageProvider;
        var previous = await ReadAsync(session, context.Binding.RemoteNamespace, cancellationToken);
        var identities = Identities(context.GameInstance, context.Definition);
        var explicitlyLinked = context.TargetId is { } target && context.Binding.Settings.TryGetValue("__gsyncRootMappingTargetId", out var mapped) &&
                               Guid.TryParse(mapped, out var mappedTarget) && mappedTarget == target;
        if (previous is not null && previous.Identities.Length > 0 &&
            !IsStrongMatch(previous.Identities, identities) && !IsNameMatch(previous.Identities, identities) && !explicitlyLinked)
            throw new InvalidOperationException("This remote folder identifies a different game. Link its existing saves explicitly before uploading.");
        var allIdentities = (previous?.Identities ?? []).Union(identities, StringComparer.OrdinalIgnoreCase).Take(32).ToArray();
        var roots = (previous?.Roots ?? []).ToList();
        foreach (var root in context.ContentRoots.Where(root => files.Any(file => file.StorageKey == root.StorageKey)))
            if (!roots.Any(value => string.Equals(value.StorageRoot, root.BaseStorageRoot, StringComparison.OrdinalIgnoreCase)))
                roots.Add(new RemoteGameRoot(root.BaseStorageRoot, root.ContentItem.ContentId, root.TemplateFingerprint, root.TreatAsDirectory));
        if (roots.Count > 512) throw new InvalidDataException("Too many remote content roots.");
        var name = previous?.Name ?? context.Definition.DisplayName;
        if (string.IsNullOrWhiteSpace(name)) name = context.GameInstance.DisplayName;
        if (name.Length > 240) name = name[..240];
        using var bytes = new MemoryStream(JsonSerializer.SerializeToUtf8Bytes(new RemoteGameDescriptor(1, name, allIdentities, roots.ToArray())));
        if (bytes.Length > 64 * 1024) throw new InvalidDataException("Remote game metadata is too large.");
        await session.UploadAsync(context.Binding.RemoteNamespace.TrimEnd('/') + "/" + MetadataName, bytes, cancellationToken);
    }

    private static bool IsSteamId(string? value) => value is { Length: > 0 and <= 16 } && value.All(char.IsAsciiDigit);
}

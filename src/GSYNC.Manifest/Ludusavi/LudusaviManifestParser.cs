using GSYNC.Core.Abstractions.Manifest;
using GSYNC.Core.Models;
using GSYNC.Core.Models.Manifest;
using GSYNC.Core.Utilities;
using Microsoft.Extensions.Logging;
using GSYNC.Manifest.Services;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

namespace GSYNC.Manifest.Ludusavi;

public sealed class LudusaviManifestParser : ILudusaviManifestParser
{
    private readonly ILogger<LudusaviManifestParser> _logger;
    private readonly IDeserializer _deserializer;

    public LudusaviManifestParser(ILogger<LudusaviManifestParser> logger)
    {
        _logger = logger;
        _deserializer = new DeserializerBuilder()
            .WithNamingConvention(CamelCaseNamingConvention.Instance)
            .IgnoreUnmatchedProperties()
            .WithDuplicateKeyChecking()
            .Build();
    }

    public IReadOnlyList<GameContentDefinition> Parse(string yamlContent)
    {
        if (string.IsNullOrWhiteSpace(yamlContent))
        {
            return Array.Empty<GameContentDefinition>();
        }

        if (yamlContent.Length > 32 * 1024 * 1024) throw new InvalidDataException("The manifest is too large.");
        LudusaviManifestDocument document;
        lock (_deserializer) document = _deserializer.Deserialize<LudusaviManifestDocument>(new BoundedYamlParser(yamlContent)) ?? new LudusaviManifestDocument();
        var definitions = new List<GameContentDefinition>();

        foreach (var pair in document)
        {
            var gameId = pair.Key;
            var entry = pair.Value;
            var paths = new Dictionary<ContentCategory, List<string>>();

            foreach (var file in entry.Files)
            {
                var path = file.Key;
                if (path.StartsWith("./", StringComparison.Ordinal) || path.StartsWith("../", StringComparison.Ordinal))
                    throw new InvalidDataException($"Relative manifest path '{path}' is not supported.");
                var tags = file.Value?.Tags ?? [];
                var category = tags.Count == 0 || tags.Contains("save", StringComparer.OrdinalIgnoreCase) ? ContentCategory.Save
                    : tags.Contains("config", StringComparer.OrdinalIgnoreCase) ? ContentCategory.Config : ContentCategory.Extra;
                if (!paths.TryGetValue(category, out var pathTemplates)) paths[category] = pathTemplates = [];
                if (LudusaviVariableMapper.TryMapPath(path, out var mappedPath))
                {
                    pathTemplates.Add(NormalizePath(mappedPath));
                }
                else
                {
                    _logger.LogWarning("Skipping unsupported Ludusavi path template '{Path}' for game '{GameId}'.", path, gameId);
                    // Keep an applicable but unsupported save visible as an unresolved template;
                    // otherwise a supported config path could falsely imply a complete save backup.
                    var constraints = file.Value?.When ?? [];
                    if (category == ContentCategory.Save && !path.Contains("<xdg", StringComparison.OrdinalIgnoreCase) &&
                        (constraints.Count == 0 || constraints.Any(condition => condition.Os is null || condition.Os.Equals("windows", StringComparison.OrdinalIgnoreCase))))
                        pathTemplates.Add(path);
                }
            }

            var sourceHints = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            sourceHints["ludusavi.title"] = gameId;
            if (!string.IsNullOrWhiteSpace(entry.Steam?.Id))
            {
                sourceHints["steam.id"] = entry.Steam.Id;
            }

            definitions.Add(new GameContentDefinition
            {
                GameId = GameIdentity.ForLudusaviTitle(gameId),
                DisplayName = gameId,
                SourceHints = sourceHints,
                ContentItems = paths.Where(pair => pair.Value.Count > 0).Select(pair => new ContentItem
                        {
                            ContentId = pair.Key == ContentCategory.Save ? "default" : pair.Key.ToString().ToLowerInvariant(),
                            Category = pair.Key,
                            PathTemplates = pair.Value.Distinct(StringComparer.OrdinalIgnoreCase).ToArray(),
                            DefaultEnabled = pair.Key != ContentCategory.Extra,
                        }).ToArray(),
            });
        }

        return definitions;
    }

    private static string NormalizePath(string path)
    {
        return path.Replace('\\', '/');
    }
}

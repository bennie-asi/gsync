using GSYNC.Core.Abstractions.Manifest;
using GSYNC.Core.Models.Manifest;
using GSYNC.Core.Utilities;
using Microsoft.Extensions.Logging;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

namespace GSYNC.Manifest.Services;

public sealed class YamlUserDefinitionStore : IUserDefinitionStore
{
    private readonly string _definitionsDirectory;
    private readonly ILogger<YamlUserDefinitionStore> _logger;
    private readonly IDeserializer _deserializer;
    private readonly ISerializer _serializer;
    private readonly SemaphoreSlim _gate = new(1, 1);

    public YamlUserDefinitionStore(string definitionsDirectory, ILogger<YamlUserDefinitionStore> logger)
    {
        _definitionsDirectory = definitionsDirectory;
        _logger = logger;
        _deserializer = new DeserializerBuilder()
            .WithNamingConvention(CamelCaseNamingConvention.Instance)
            .WithTypeMapping<IReadOnlyList<ContentItem>, List<ContentItem>>()
            .WithTypeMapping<IReadOnlyList<string>, List<string>>()
            .WithTypeMapping<IReadOnlyDictionary<string, string>, Dictionary<string, string>>()
            .IgnoreUnmatchedProperties()
            .WithDuplicateKeyChecking()
            .Build();
        _serializer = new SerializerBuilder().WithNamingConvention(CamelCaseNamingConvention.Instance).DisableAliases().Build();
    }

    public async Task<IReadOnlyList<GameContentDefinition>> LoadDefinitionsAsync(CancellationToken cancellationToken)
    {
        await _gate.WaitAsync(cancellationToken);
        try { return (await ReadFilesAsync(cancellationToken)).Select(entry => entry.Definition).ToArray(); }
        finally { _gate.Release(); }
    }

    public async Task SaveDefinitionAsync(GameContentDefinition definition, CancellationToken cancellationToken, bool overwrite = true)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(definition.GameId);
        await _gate.WaitAsync(cancellationToken);
        try
        {
            var files = await ReadFilesAsync(cancellationToken);
            var existing = files.SingleOrDefault(entry => string.Equals(entry.Definition.GameId, definition.GameId, StringComparison.OrdinalIgnoreCase));
            if (existing.Path is not null && !overwrite) throw new InvalidOperationException("This game already has a user definition. Edit the existing profile instead of replacing it.");
            Directory.CreateDirectory(_definitionsDirectory);
            var path = existing.Path ?? Path.Combine(_definitionsDirectory, GameIdentity.StorageNamespace(definition.GameId) + ".yaml");
            cancellationToken.ThrowIfCancellationRequested();
            var yaml = _serializer.Serialize(definition);
            if (existing.Path is null)
            {
                using var prepared = await AtomicFile.PrepareAsync(new MemoryStream(System.Text.Encoding.UTF8.GetBytes(yaml)), path, cancellationToken);
                // Create-only move protects a definition created by another app process in the meantime.
                File.Move(prepared.TemporaryPath!, path);
            }
            else AtomicFile.WriteAllText(path, yaml);
        }
        finally { _gate.Release(); }
    }

    private async Task<IReadOnlyList<(string Path, GameContentDefinition Definition)>> ReadFilesAsync(CancellationToken cancellationToken)
    {
        if (!Directory.Exists(_definitionsDirectory))
        {
            return Array.Empty<(string, GameContentDefinition)>();
        }

        var definitions = new List<(string Path, GameContentDefinition Definition)>();
        foreach (var file in Directory.EnumerateFiles(_definitionsDirectory, "*.yaml", SearchOption.TopDirectoryOnly))
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (new FileInfo(file).Length > 32 * 1024 * 1024) throw new InvalidDataException($"User definition '{file}' is too large.");
            var yaml = await File.ReadAllTextAsync(file, cancellationToken);
            var definition = _deserializer.Deserialize<GameContentDefinition>(new BoundedYamlParser(yaml))
                ?? throw new InvalidDataException($"User definition '{file}' is empty. Remove the file explicitly if its override is no longer needed.");
            if (string.IsNullOrWhiteSpace(definition.GameId) || definitions.Any(entry => string.Equals(entry.Definition.GameId, definition.GameId, StringComparison.OrdinalIgnoreCase)))
                throw new InvalidDataException($"User definition '{file}' has an empty or duplicate game ID.");
            definitions.Add((file, definition));
        }

        _logger.LogInformation("Loaded {DefinitionCount} user manifest definitions from {Directory}.", definitions.Count, _definitionsDirectory);
        return definitions;
    }
}

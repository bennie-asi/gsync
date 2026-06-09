using GSYNC.Core.Abstractions.Manifest;
using GSYNC.Core.Models.Manifest;
using Microsoft.Extensions.Logging;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

namespace GSYNC.Manifest.Services;

public sealed class YamlUserDefinitionStore : IUserDefinitionStore
{
    private readonly string _definitionsDirectory;
    private readonly ILogger<YamlUserDefinitionStore> _logger;
    private readonly IDeserializer _deserializer;

    public YamlUserDefinitionStore(string definitionsDirectory, ILogger<YamlUserDefinitionStore> logger)
    {
        _definitionsDirectory = definitionsDirectory;
        _logger = logger;
        _deserializer = new DeserializerBuilder()
            .WithNamingConvention(CamelCaseNamingConvention.Instance)
            .IgnoreUnmatchedProperties()
            .Build();
    }

    public async Task<IReadOnlyList<GameContentDefinition>> LoadDefinitionsAsync(CancellationToken cancellationToken)
    {
        if (!Directory.Exists(_definitionsDirectory))
        {
            return Array.Empty<GameContentDefinition>();
        }

        var definitions = new List<GameContentDefinition>();
        foreach (var file in Directory.EnumerateFiles(_definitionsDirectory, "*.yaml", SearchOption.TopDirectoryOnly))
        {
            cancellationToken.ThrowIfCancellationRequested();
            var yaml = await File.ReadAllTextAsync(file, cancellationToken);
            var definition = _deserializer.Deserialize<GameContentDefinition>(yaml);
            if (definition is not null)
            {
                definitions.Add(definition);
            }
        }

        _logger.LogInformation("Loaded {DefinitionCount} user manifest definitions from {Directory}.", definitions.Count, _definitionsDirectory);
        return definitions;
    }
}

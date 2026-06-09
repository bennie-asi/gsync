using GSYNC.Core.Models.Manifest;

namespace GSYNC.Core.Abstractions.Manifest;

public interface ILudusaviManifestParser
{
    IReadOnlyList<GameContentDefinition> Parse(string yamlContent);
}

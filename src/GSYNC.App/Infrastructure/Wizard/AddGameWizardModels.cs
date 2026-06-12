using GSYNC.Core.Models;
using GSYNC.Core.Models.Manifest;

namespace GSYNC.App.Infrastructure.Wizard;

public enum MatchConfidence
{
    SourceHint,
    PlatformId,
    NormalizedName,
    Fallback,
    Manual,
}

public enum MatchOrigin
{
    ExistingDefinition,
    FallbackDefinition,
    ManualDefinition,
}

public sealed record GameDefinitionMatch(
    DiscoveredGame Game,
    GameContentDefinition Definition,
    MatchConfidence Confidence,
    MatchOrigin Origin,
    string Reason,
    bool IsFallback);

public enum PathValidationSeverity
{
    Ok,
    Warning,
    Blocking,
}

public sealed record PathValidationResult(
    string ContentId,
    string Template,
    string ResolvedPath,
    PathValidationSeverity Severity,
    string MessageCode,
    bool Exists,
    bool IsResolved);

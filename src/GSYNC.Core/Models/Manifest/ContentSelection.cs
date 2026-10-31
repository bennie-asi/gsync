namespace GSYNC.Core.Models.Manifest;

public sealed class ContentSelection
{
    public required string ContentId { get; init; }

    public bool IsEnabled { get; init; } = true;

    // Keys are the original portable templates so the remote root identity does not
    // change when two computers use different local save locations.
    public IReadOnlyDictionary<string, string> PathOverrides { get; init; } = new Dictionary<string, string>();

    public string EffectiveTemplate(string originalTemplate)
    {
        var normalized = originalTemplate.Replace('\\', '/');
        var match = PathOverrides?.FirstOrDefault(pair =>
            string.Equals(pair.Key.Replace('\\', '/'), normalized, StringComparison.OrdinalIgnoreCase));
        return match is { Value: { } path } && !string.IsNullOrWhiteSpace(path) ? path : originalTemplate;
    }
}

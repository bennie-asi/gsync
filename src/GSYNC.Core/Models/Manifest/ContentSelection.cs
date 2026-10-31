namespace GSYNC.Core.Models.Manifest;

public sealed class ContentSelection
{
    public required string ContentId { get; init; }

    public bool IsEnabled { get; init; } = true;

    // Keys are the original portable templates so the remote root identity does not
    // change when two computers use different local save locations.
    public IReadOnlyDictionary<string, string> PathOverrides { get; init; } = new Dictionary<string, string>();

    // Explicit links to content roots already stored on the selected target.
    public IReadOnlyDictionary<string, string> RemoteRootOverrides { get; init; } = new Dictionary<string, string>();

    // Path-level choices retain the original template identity and do not modify shared definitions.
    public IReadOnlyList<string> DisabledPathTemplates { get; init; } = Array.Empty<string>();
    public IReadOnlyList<string> RemovedPathTemplates { get; init; } = Array.Empty<string>();

    public bool IsTemplateRemoved(string template) => ContainsTemplate(RemovedPathTemplates, template);
    public bool IsTemplateEnabled(string template) => IsEnabled && !IsTemplateRemoved(template) && !ContainsTemplate(DisabledPathTemplates, template);

    private static bool ContainsTemplate(IReadOnlyList<string>? templates, string template) =>
        templates?.Any(value => string.Equals(value.Replace('\\', '/'), template.Replace('\\', '/'), StringComparison.OrdinalIgnoreCase)) == true;

    public string EffectiveTemplate(string originalTemplate)
    {
        var normalized = originalTemplate.Replace('\\', '/');
        var match = PathOverrides?.FirstOrDefault(pair =>
            string.Equals(pair.Key.Replace('\\', '/'), normalized, StringComparison.OrdinalIgnoreCase));
        return match is { Value: { } path } && !string.IsNullOrWhiteSpace(path) ? path : originalTemplate;
    }

    public string? RemoteRoot(string originalTemplate) => RemoteRootOverrides?.FirstOrDefault(pair =>
        string.Equals(pair.Key.Replace('\\', '/'), originalTemplate.Replace('\\', '/'), StringComparison.OrdinalIgnoreCase)).Value;
}

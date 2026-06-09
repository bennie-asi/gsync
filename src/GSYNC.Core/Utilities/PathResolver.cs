using System.Text.RegularExpressions;

namespace GSYNC.Core.Utilities;

public sealed partial class PathResolver
{
    private static readonly Regex VariablePattern = VariableRegex();

    public string Resolve(string? template, params IReadOnlyDictionary<string, string>[] scopes)
    {
        if (string.IsNullOrWhiteSpace(template))
        {
            return string.Empty;
        }

        var variables = new VariableScopeStack(scopes).Flatten();
        return Resolve(template, variables);
    }

    public string Resolve(string template, IReadOnlyDictionary<string, string> variables)
    {
        ArgumentNullException.ThrowIfNull(template);
        ArgumentNullException.ThrowIfNull(variables);

        return VariablePattern.Replace(template, match =>
        {
            var key = match.Groups[1].Value;
            return variables.TryGetValue(key, out var value)
                ? value
                : match.Value;
        });
    }

    [GeneratedRegex("%([A-Z0-9_]+)%", RegexOptions.Compiled | RegexOptions.IgnoreCase)]
    private static partial Regex VariableRegex();
}

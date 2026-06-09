namespace GSYNC.Core.Utilities;

public sealed class VariableScopeStack
{
    private readonly IReadOnlyList<IReadOnlyDictionary<string, string>> _scopes;

    public VariableScopeStack(params IReadOnlyDictionary<string, string>[] scopes)
        : this((IEnumerable<IReadOnlyDictionary<string, string>>)scopes)
    {
    }

    public VariableScopeStack(IEnumerable<IReadOnlyDictionary<string, string>> scopes)
    {
        _scopes = scopes.ToArray();
    }

    public IReadOnlyDictionary<string, string> Flatten()
    {
        var merged = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        foreach (var scope in _scopes)
        {
            foreach (var pair in scope)
            {
                merged[pair.Key] = pair.Value;
            }
        }

        return merged;
    }
}

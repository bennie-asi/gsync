using YamlDotNet.Core;
using YamlDotNet.Core.Events;

namespace GSYNC.Manifest.Services;

internal sealed class BoundedYamlParser(string yaml) : IParser
{
    private readonly IParser _inner = new Parser(new StringReader(yaml));
    private readonly Stack<AnchorName> _containers = new();
    private readonly HashSet<AnchorName> _open = new();
    private readonly HashSet<AnchorName> _seen = new();
    private int _aliases;
    public ParsingEvent? Current => _inner.Current;
    public bool MoveNext()
    {
        if (!_inner.MoveNext()) return false;
        if (Current is NodeEvent node && !node.Anchor.IsEmpty) _seen.Add(node.Anchor);
        if (Current is MappingStart or SequenceStart)
        {
            var anchor = ((NodeEvent)Current).Anchor;
            _containers.Push(anchor);
            if (!anchor.IsEmpty) _open.Add(anchor);
            if (_containers.Count > 64) throw new InvalidDataException("The YAML nesting limit was exceeded.");
        }
        else if (Current is MappingEnd or SequenceEnd)
        {
            if (_containers.TryPop(out var anchor) && !anchor.IsEmpty) _open.Remove(anchor);
        }
        else if (Current is AnchorAlias alias && (++_aliases > 100000 || _open.Contains(alias.Value) || !_seen.Contains(alias.Value)))
            throw new InvalidDataException("Recursive or forward YAML aliases are not supported.");
        return true;
    }
}

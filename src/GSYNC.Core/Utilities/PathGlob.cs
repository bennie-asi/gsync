using System.Text;
using System.Text.RegularExpressions;

namespace GSYNC.Core.Utilities;

public static class PathGlob
{
    public static (string RootPath, string? Pattern) SplitRoot(string path)
    {
        var normalized = path.Replace('\\', '/');
        var wildcard = normalized.IndexOfAny(['*', '?', '[']);
        if (wildcard < 0) return (path, null);

        var separator = normalized.LastIndexOf('/', wildcard);
        var root = separator < 0 ? "." : normalized[..(separator + 1)];
        return (root, normalized[(separator + 1)..].TrimEnd('/'));
    }

    public static bool IsMatch(string pattern, string relativePath, bool matchFileName = false, bool includeDescendants = false)
    {
        var normalized = pattern.Replace('\\', '/').Trim('/');
        var expression = new StringBuilder("^");
        if (matchFileName && !normalized.Contains('/')) expression.Append("(?:.*/)?");

        for (var i = 0; i < normalized.Length; i++)
        {
            switch (normalized[i])
            {
                case '*':
                    if (i + 1 < normalized.Length && normalized[i + 1] == '*')
                    {
                        i++;
                        if (i + 1 < normalized.Length && normalized[i + 1] == '/')
                        {
                            expression.Append("(?:.*/)?");
                            i++;
                        }
                        else expression.Append(".*");
                    }
                    else expression.Append("[^/]*");
                    break;
                case '?':
                    expression.Append("[^/]");
                    break;
                case '[':
                    var end = normalized.IndexOf(']', i + 1);
                    if (end <= i + 1) throw new ArgumentException($"Invalid glob pattern '{pattern}'.");
                    var characters = normalized[(i + 1)..end];
                    if (characters[0] == '!') characters = "^" + characters[1..];
                    if (characters.Contains('/') || characters.Contains('[')) throw new ArgumentException($"Invalid glob pattern '{pattern}'.");
                    expression.Append('[').Append(characters).Append(']');
                    i = end;
                    break;
                default:
                    expression.Append(Regex.Escape(normalized[i].ToString()));
                    break;
            }
        }

        if (includeDescendants) expression.Append("(?:/.*)?");
        expression.Append('$');
        return Regex.IsMatch(relativePath.Replace('\\', '/'), expression.ToString(),
            RegexOptions.IgnoreCase | RegexOptions.CultureInvariant | RegexOptions.NonBacktracking, TimeSpan.FromSeconds(1));
    }
}

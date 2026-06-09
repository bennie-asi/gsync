namespace GSYNC.Manifest.Ludusavi;

public static class LudusaviVariableMapper
{
    private static readonly IReadOnlyDictionary<string, string?> VariableMappings = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase)
    {
        ["<base>"] = "%GAME_INSTALL_DIR%",
        ["<home>"] = "%HOME%",
        ["<winAppData>"] = "%APPDATA%",
        ["<winLocalAppData>"] = "%LOCALAPPDATA%",
        ["<winDocuments>"] = "%DOCUMENTS%",
    };

    public static bool TryMapPath(string path, out string mappedPath)
    {
        mappedPath = path;

        foreach (var pair in VariableMappings)
        {
            if (mappedPath.Contains(pair.Key, StringComparison.OrdinalIgnoreCase))
            {
                mappedPath = mappedPath.Replace(pair.Key, pair.Value, StringComparison.OrdinalIgnoreCase);
            }
        }

        return !mappedPath.Contains("<", StringComparison.Ordinal) && !mappedPath.Contains(">", StringComparison.Ordinal);
    }
}

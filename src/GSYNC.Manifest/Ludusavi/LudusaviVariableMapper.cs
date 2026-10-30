namespace GSYNC.Manifest.Ludusavi;

public static class LudusaviVariableMapper
{
    private static readonly IReadOnlyDictionary<string, string?> VariableMappings = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase)
    {
        ["<base>"] = "%GAME_INSTALL_DIR%",
        ["<root>/<game>"] = "%GAME_INSTALL_DIR%",
        ["<root>/userdata"] = "%STEAM_ROOT%/userdata",
        ["<root>"] = "%STORE_ROOT%",
        ["<game>"] = "%GAME_DIR_NAME%",
        ["<storeGameId>"] = "%STORE_GAME_ID%",
        ["<storeUserId>"] = "%STORE_USER_ID%",
        ["<osUserName>"] = "%USERNAME%",
        ["<home>"] = "%HOME%",
        ["<winAppData>"] = "%APPDATA%",
        ["<winLocalAppData>"] = "%LOCALAPPDATA%",
        ["<winLocalAppDataLow>"] = "%HOME%/AppData/LocalLow",
        ["<winDocuments>"] = "%DOCUMENTS%",
        ["<winPublic>"] = "%PUBLIC%",
        ["<winProgramData>"] = "%PROGRAMDATA%",
        ["<winDir>"] = "%WINDIR%",
    };

    public static bool TryMapPath(string path, out string mappedPath)
    {
        mappedPath = path.Replace('\\', '/');

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

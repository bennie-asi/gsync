namespace GSYNC.Core.Utilities;

public sealed class SystemVariableProvider
{
    public IReadOnlyDictionary<string, string> GetVariables()
    {
        return new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["HOME"] = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
            ["APPDATA"] = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            ["LOCALAPPDATA"] = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            ["DOCUMENTS"] = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
        };
    }
}

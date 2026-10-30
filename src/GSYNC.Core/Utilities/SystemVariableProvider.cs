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
            ["USERNAME"] = Environment.UserName,
            ["PUBLIC"] = Environment.GetEnvironmentVariable("PUBLIC") ?? Path.GetDirectoryName(Environment.GetFolderPath(Environment.SpecialFolder.CommonDocuments)) ?? "",
            ["PROGRAMDATA"] = Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData),
            ["WINDIR"] = Environment.GetFolderPath(Environment.SpecialFolder.Windows),
        };
    }
}

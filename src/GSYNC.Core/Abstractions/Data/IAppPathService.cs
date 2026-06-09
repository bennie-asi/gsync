namespace GSYNC.Core.Abstractions.Data;

public interface IAppPathService
{
    string GetAppDataRoot();

    string GetDatabasePath();

    string GetDefinitionsDirectory();

    string GetLogsDirectory();

    string GetManifestCachePath();

    string GetSnapshotsDirectory();

    string GetUiSettingsPath();
}

using GSYNC.App.Infrastructure.Configuration;
using GSYNC.Core.Abstractions.Data;
using Xunit;

namespace GSYNC.AppLogic.Tests;

public sealed class ConfigurationSafetyTests : IDisposable
{
    private readonly TestPaths _paths = new();

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void CorruptConfiguration_IsReported_AndNeverReplacedWithDefaults(bool variables)
    {
        var path = Path.Combine(_paths.Root, variables ? "user-variables.json" : "sync-targets.json");
        File.WriteAllText(path, "{ corrupt configuration");
        if (variables)
        {
            var store = new UserVariablesStore(_paths);
            Assert.Throws<InvalidDataException>(() => store.List());
            Assert.Throws<InvalidDataException>(() => store.Upsert(new UserVariable { Name = "SAVE", Value = "new" }));
        }
        else
        {
            var store = new SyncTargetStore(_paths);
            Assert.Throws<InvalidDataException>(() => store.List());
            Assert.Throws<InvalidDataException>(() => store.Upsert(new SyncTargetConfig { Name = "new", ProviderId = "local-folder" }));
        }
        Assert.Equal("{ corrupt configuration", File.ReadAllText(path));
    }

    [Fact]
    public void FailedTargetSave_DoesNotPublishUnpersistedChanges()
    {
        var store = new SyncTargetStore(_paths);
        var target = Assert.Single(store.List());
        var original = target.Settings["rootPath"];
        target.Settings["rootPath"] = Path.Combine(_paths.Root, "changed");
        var path = Path.Combine(_paths.Root, "sync-targets.json");
        var backup = Path.Combine(_paths.Root, "original.json");
        File.Move(path, backup);
        Directory.CreateDirectory(path);
        try
        {
            Assert.Throws<IOException>(() => store.Upsert(target));
            Assert.Equal(original, store.Get(target.Id)!.Settings["rootPath"]);
        }
        finally { Directory.Delete(path); File.Move(backup, path); }
        Assert.Equal(original, new SyncTargetStore(_paths).Get(target.Id)!.Settings["rootPath"]);
    }

    [Fact]
    public void ReadAndInputObjects_CannotMutatePersistedStoreState()
    {
        var targets = new SyncTargetStore(_paths);
        var target = Assert.Single(targets.List());
        target.Name = "unsaved";
        target.Settings["rootPath"] = "unsaved";
        Assert.Equal("Local Storage", targets.Get(target.Id)!.Name);
        Assert.NotEqual("unsaved", targets.Get(target.Id)!.Settings["rootPath"]);
        var variables = new UserVariablesStore(_paths);
        var variable = new UserVariable { Name = "SAVE_ROOT", Value = "original" };
        variables.Upsert(variable);
        variable.Value = "unsaved";
        Assert.Single(variables.List()).Value = "also-unsaved";
        Assert.Equal("original", variables.AsDictionary()["SAVE_ROOT"]);
    }

    [Fact]
    public void EmptyTargetList_RemainsEmptyAfterRestart_AndVariableNamesAreCanonical()
    {
        var targets = new SyncTargetStore(_paths);
        targets.Remove(Assert.Single(targets.List()).Id);
        Assert.Empty(new SyncTargetStore(_paths).List());
        var variables = new UserVariablesStore(_paths);
        variables.Upsert(new UserVariable { Name = "SAVE_ROOT", Value = "one" });
        variables.Upsert(new UserVariable { Name = "%save_root%", Value = "two" });
        Assert.Single(new UserVariablesStore(_paths).List());
        Assert.Equal("two", variables.AsDictionary()["SAVE_ROOT"]);
    }

    [Fact]
    public void CorruptUiSettingsArePreserved_AndBootstrapDoesNotStartNetworkRefresh()
    {
        var path = _paths.GetUiSettingsPath();
        File.WriteAllText(path, "{broken-settings");
        var store = new GSYNC.App.Infrastructure.Localization.UiSettingsStore(_paths);
        Assert.Throws<InvalidDataException>(() => store.Load());
        Assert.Throws<InvalidDataException>(() => store.Save(new GSYNC.App.Infrastructure.Localization.AppUiSettings()));
        Assert.False(store.LoadForStartup().RefreshManifestOnStartup);
        Assert.Equal("{broken-settings", File.ReadAllText(path));
    }

    public void Dispose() => Directory.Delete(_paths.Root, recursive: true);
    private sealed class TestPaths : IAppPathService
    {
        public string Root { get; } = Path.Combine(Path.GetTempPath(), "gsync-config-tests", Guid.NewGuid().ToString("N"));
        public TestPaths() => Directory.CreateDirectory(Root);
        public string GetAppDataRoot() => Root;
        public string GetDatabasePath() => Path.Combine(Root, "data.db");
        public string GetDefinitionsDirectory() => Path.Combine(Root, "definitions");
        public string GetLogsDirectory() => Path.Combine(Root, "logs");
        public string GetManifestCachePath() => Path.Combine(Root, "manifest.yaml");
        public string GetSnapshotsDirectory() => Path.Combine(Root, "snapshots");
        public string GetUiSettingsPath() => Path.Combine(Root, "ui.json");
    }
}

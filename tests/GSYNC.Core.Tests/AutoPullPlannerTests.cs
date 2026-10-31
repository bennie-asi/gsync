using GSYNC.Core.Abstractions.Data;
using GSYNC.Core.Models;
using GSYNC.Core.Services.Sync;
using Xunit;

namespace GSYNC.Core.Tests;

public sealed class AutoPullPlannerTests
{
    private static readonly Guid Game = Guid.NewGuid();
    private static readonly string A = new('A', 64);
    private static readonly string B = new('B', 64);
    private static readonly string Config = new('C', 64);
    private static SyncDiffEntry Entry(string path, string? local, string? remote) => new() { RelativePath = path, LocalHash = local, RemoteHash = remote, ChangeKind = SyncChangeKind.Conflict };
    private static SyncComparison Compare(params SyncDiffEntry[] entries) => new() { ConfigurationFingerprint = Config, Entries = entries };
    private static AutoPullBaseline Baseline() => AutoPullPlanner.ConfirmBaseline(Game, Compare(Entry("game/save", A, A)));

    [Fact]
    public void FirstConnectionRequiresExplicitConfirmation()
    {
        var equal = Compare(Entry("game/save", A, A));
        Assert.Equal(AutoPullDisposition.Review, AutoPullPlanner.Evaluate(Game, equal, null).Disposition);
        Assert.Throws<AutoPullBaselineMismatchException>(() => AutoPullPlanner.ConfirmBaseline(Game, Compare(Entry("game/save", null, A))));
        Assert.Throws<AutoPullBaselineMismatchException>(() => AutoPullPlanner.ConfirmBaseline(Game, Compare()));
        Assert.Equal(AutoPullDisposition.UpToDate, AutoPullPlanner.Evaluate(Game, equal, AutoPullPlanner.ConfirmBaseline(Game, equal)).Disposition);
    }

    [Fact]
    public void OnlyRemoteChangesProduceVerifiedDownloadDecisions()
    {
        var comparison = Compare(Entry("game/save", A, B), Entry("game/new", null, B));
        var result = AutoPullPlanner.Evaluate(Game, comparison, Baseline());
        Assert.Equal(AutoPullDisposition.Pull, result.Disposition);
        Assert.Same(comparison, result.Plan!.ExpectedComparison);
        Assert.All(result.Plan.Decisions, decision => { Assert.Equal(ConflictResolutionAction.KeepRemote, decision.Action); Assert.True(decision.VerifyComparedVersions); });
        Assert.Equal(2, result.Plan.Decisions.Count);
    }

    [Theory]
    [InlineData("local-change")]
    [InlineData("local-add")]
    [InlineData("local-delete")]
    [InlineData("remote-delete")]
    [InlineData("both-delete")]
    [InlineData("aligned-new")]
    public void LocalChangesAndDeletionsRequireReview(string scenario)
    {
        var comparison = scenario switch
        {
            "local-change" => Compare(Entry("game/save", B, A)),
            "local-add" => Compare(Entry("game/save", A, B), Entry("game/new", B, null)),
            "local-delete" => Compare(Entry("game/save", null, A)),
            "remote-delete" => Compare(Entry("game/save", A, null)),
            "both-delete" => Compare(),
            _ => Compare(Entry("game/save", B, B)),
        };
        Assert.Equal(AutoPullDisposition.Review, AutoPullPlanner.Evaluate(Game, comparison, Baseline()).Disposition);
    }

    [Fact]
    public void ChangedConfigurationOrInvalidComparisonCannotAuthorizePull()
    {
        var changed = new SyncComparison { ConfigurationFingerprint = B, Entries = [Entry("game/save", A, B)] };
        Assert.Equal(AutoPullDisposition.Review, AutoPullPlanner.Evaluate(Game, changed, Baseline()).Disposition);
        Assert.Throws<InvalidDataException>(() => AutoPullPlanner.Evaluate(Game, Compare(Entry("game/save", "bad", B)), Baseline()));
        Assert.Throws<InvalidDataException>(() => AutoPullPlanner.Evaluate(Game, Compare(Entry("game/save", A, B), Entry("GAME/SAVE", A, B)), Baseline()));
    }

    [Fact]
    public void BaselineStorePersistsAndPreservesCorruptFiles()
    {
        var root = Path.Combine(Path.GetTempPath(), "gsync-auto-baseline-tests", Guid.NewGuid().ToString("N"));
        var store = new AutoPullBaselineStore(new Paths(root));
        store.Save(Baseline());
        Assert.Equal(A, store.Load(Game)!.Files["GAME/SAVE"]);
        var path = Path.Combine(root, "auto-pull-baselines", Game.ToString("N") + ".json");
        File.WriteAllText(path, "broken");
        Assert.Throws<InvalidDataException>(() => store.Save(Baseline()));
        Assert.Equal("broken", File.ReadAllText(path));
        store.SaveConfirmed(Baseline());
        Assert.Equal(A, store.Load(Game)!.Files["game/save"]);
        Assert.Equal("broken", File.ReadAllText(Assert.Single(Directory.GetFiles(Path.GetDirectoryName(path)!, "*.invalid-*.bak"))));
        File.WriteAllText(path, $"{{\"Version\":1,\"GameInstanceId\":\"{Game}\",\"ConfigurationFingerprint\":\"{Config}\",\"Files\":{{\"game/save\":\"{A}\",\"game/save\":\"{B}\"}}}}");
        Assert.Throws<InvalidDataException>(() => store.Load(Game));
    }

    private sealed class Paths(string root) : IAppPathService
    {
        public string GetAppDataRoot() => root;
        public string GetDatabasePath() => Path.Combine(root, "data.db");
        public string GetDefinitionsDirectory() => Path.Combine(root, "definitions");
        public string GetLogsDirectory() => Path.Combine(root, "logs");
        public string GetManifestCachePath() => Path.Combine(root, "manifest");
        public string GetSnapshotsDirectory() => Path.Combine(root, "snapshots");
        public string GetUiSettingsPath() => Path.Combine(root, "settings");
    }
}

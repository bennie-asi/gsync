using System.Text.Json;
using GSYNC.Core.Abstractions.Data;
using GSYNC.Core.Models;
using GSYNC.Core.Utilities;

namespace GSYNC.Core.Services.Sync;

public sealed record AutoPullBaseline(int Version, Guid GameInstanceId, string ConfigurationFingerprint, Dictionary<string, string> Files);
public enum AutoPullDisposition { UpToDate, Pull, Review }
public sealed record AutoPullEvaluation(AutoPullDisposition Disposition, string Reason, ConflictResolutionPlan? Plan = null);
public sealed class AutoPullBaselineMismatchException() : InvalidOperationException("Local and remote saves must be non-empty and identical before confirming a baseline.") { }

/// <summary>Only a previously confirmed common version can authorize automatic downloads.</summary>
public static class AutoPullPlanner
{
    public static AutoPullBaseline ConfirmBaseline(Guid gameId, SyncComparison comparison)
    {
        var entries = ValidateComparison(comparison);
        if (entries.Count == 0 || entries.Values.Any(e => e.LocalHash is null || !Equal(e.LocalHash, e.RemoteHash)))
            throw new AutoPullBaselineMismatchException();
        return new AutoPullBaseline(1, gameId, comparison.ConfigurationFingerprint!,
            entries.ToDictionary(pair => pair.Key, pair => pair.Value.LocalHash!, StringComparer.OrdinalIgnoreCase));
    }

    public static AutoPullEvaluation Evaluate(Guid gameId, SyncComparison comparison, AutoPullBaseline? baseline)
    {
        var current = ValidateComparison(comparison);
        if (baseline is null) return new(AutoPullDisposition.Review, "baseline-required");
        ValidateBaseline(baseline, gameId);
        if (!Equal(baseline.ConfigurationFingerprint, comparison.ConfigurationFingerprint)) return new(AutoPullDisposition.Review, "configuration-changed");
        if (current.Count == 0 || baseline.Files.Keys.Any(path => !current.ContainsKey(path))) return new(AutoPullDisposition.Review, "file-removed");

        // A newly aligned version still needs explicit confirmation unless our own pull completed it.
        if (current.Values.All(e => e.LocalHash is not null && Equal(e.LocalHash, e.RemoteHash)))
        {
            var same = current.Count == baseline.Files.Count && current.All(pair => baseline.Files.TryGetValue(pair.Key, out var hash) && Equal(hash, pair.Value.LocalHash));
            return new(same ? AutoPullDisposition.UpToDate : AutoPullDisposition.Review, same ? "up-to-date" : "baseline-needs-confirmation");
        }

        foreach (var (path, oldHash) in baseline.Files)
        {
            if (!current.TryGetValue(path, out var entry) || !Equal(entry.LocalHash, oldHash)) return new(AutoPullDisposition.Review, "local-changed");
            if (entry.RemoteHash is null) return new(AutoPullDisposition.Review, "remote-deleted");
        }
        if (current.Any(pair => !baseline.Files.ContainsKey(pair.Key) && pair.Value.LocalHash is not null))
            return new(AutoPullDisposition.Review, "local-added");

        var decisions = current.Values.Where(entry => entry.RemoteHash is not null && !Equal(entry.LocalHash, entry.RemoteHash))
            .Select(entry => new ConflictResolutionDecision
            {
                RelativePath = entry.RelativePath, Action = ConflictResolutionAction.KeepRemote,
                VerifyComparedVersions = true, ExpectedLocalHash = entry.LocalHash, ExpectedRemoteHash = entry.RemoteHash,
            }).ToArray();
        if (decisions.Length == 0) return new(AutoPullDisposition.Review, "unresolved");
        return new(AutoPullDisposition.Pull, "remote-changed", new ConflictResolutionPlan
        {
            GameInstanceId = gameId, Decisions = decisions, ExpectedComparison = comparison,
        });
    }

    public static void ValidateBaseline(AutoPullBaseline baseline, Guid gameId)
    {
        if (baseline.Version != 1 || baseline.GameInstanceId != gameId || !IsHash(baseline.ConfigurationFingerprint) ||
            baseline.Files is null || baseline.Files.Count == 0 || baseline.Files.Count > 100_000 ||
            baseline.Files.Keys.Distinct(StringComparer.OrdinalIgnoreCase).Count() != baseline.Files.Count ||
            baseline.Files.Any(pair => string.IsNullOrWhiteSpace(pair.Key) || !IsHash(pair.Value)))
            throw new InvalidDataException("The automatic-pull baseline is invalid; it was preserved for review.");
    }

    private static Dictionary<string, SyncDiffEntry> ValidateComparison(SyncComparison comparison)
    {
        if (!IsHash(comparison.ConfigurationFingerprint) || comparison.Entries.Count > 100_000)
            throw new InvalidDataException("The save comparison is incomplete.");
        var result = new Dictionary<string, SyncDiffEntry>(StringComparer.OrdinalIgnoreCase);
        foreach (var entry in comparison.Entries)
        {
            if (string.IsNullOrWhiteSpace(entry.RelativePath) || (entry.LocalHash is null && entry.RemoteHash is null) ||
                (entry.LocalHash is not null && !IsHash(entry.LocalHash)) || (entry.RemoteHash is not null && !IsHash(entry.RemoteHash)) ||
                !result.TryAdd(entry.RelativePath, entry)) throw new InvalidDataException("The save comparison contains invalid or duplicate entries.");
        }
        return result;
    }

    private static bool Equal(string? a, string? b) => string.Equals(a, b, StringComparison.OrdinalIgnoreCase);
    private static bool IsHash(string? value) => value is { Length: 64 } && value.All(Uri.IsHexDigit);
}

public sealed class AutoPullBaselineStore(IAppPathService paths)
{
    private readonly object _gate = new();
    private string PathFor(Guid gameId) => Path.Combine(paths.GetAppDataRoot(), "auto-pull-baselines", gameId.ToString("N") + ".json");

    public AutoPullBaseline? Load(Guid gameId)
    {
        lock (_gate)
        {
            var path = PathFor(gameId);
            if (!File.Exists(path)) return null;
            try
            {
                if (new FileInfo(path).Length > 32 * 1024 * 1024) throw new InvalidDataException("The automatic-pull baseline is too large.");
                var json = File.ReadAllText(path);
                using var document = JsonDocument.Parse(json);
                CheckUniqueProperties(document.RootElement);
                if (document.RootElement.TryGetProperty("Files", out var files)) CheckUniqueProperties(files);
                var baseline = JsonSerializer.Deserialize<AutoPullBaseline>(json) ?? throw new InvalidDataException("The automatic-pull baseline is empty.");
                AutoPullPlanner.ValidateBaseline(baseline, gameId);
                return baseline with { Files = new Dictionary<string, string>(baseline.Files, StringComparer.OrdinalIgnoreCase) };
            }
            catch (JsonException exception) { throw new InvalidDataException("The automatic-pull baseline is invalid; it was preserved for review.", exception); }
        }
    }

    private static void CheckUniqueProperties(JsonElement element)
    {
        if (element.ValueKind != JsonValueKind.Object) throw new InvalidDataException("The automatic-pull baseline is invalid; it was preserved for review.");
        var names = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var property in element.EnumerateObject())
            if (!names.Add(property.Name)) throw new InvalidDataException("The automatic-pull baseline contains duplicate entries; it was preserved for review.");
    }

    public void Save(AutoPullBaseline baseline)
    {
        AutoPullPlanner.ValidateBaseline(baseline, baseline.GameInstanceId);
        lock (_gate)
        {
            // Preserve damaged files instead of replacing them with an unverified baseline.
            _ = Load(baseline.GameInstanceId);
            AtomicFile.WriteAllText(PathFor(baseline.GameInstanceId), JsonSerializer.Serialize(baseline));
        }
    }

    public void SaveConfirmed(AutoPullBaseline baseline)
    {
        AutoPullPlanner.ValidateBaseline(baseline, baseline.GameInstanceId);
        lock (_gate)
        {
            var path = PathFor(baseline.GameInstanceId);
            try { _ = Load(baseline.GameInstanceId); }
            catch (InvalidDataException)
            {
                // Explicit confirmation can repair a damaged baseline, but never destroy the evidence.
                File.Copy(path, path + ".invalid-" + Guid.NewGuid().ToString("N") + ".bak");
            }
            AtomicFile.WriteAllText(path, JsonSerializer.Serialize(baseline));
        }
    }
}

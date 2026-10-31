using System.Text.Json;
using GSYNC.Core.Utilities;

namespace GSYNC.Core.Updates;

public sealed record UpdateResult(string State, string Version, string Message, DateTimeOffset AtUtc, string? RecoveryDirectory = null);

public static class UpdateResultStore
{
    public static void Write(string dataRoot, UpdateResult result)
        => AtomicFile.WriteAllBytes(Path.Combine(dataRoot, "updates", "last-result.json"), JsonSerializer.SerializeToUtf8Bytes(result));

    public static UpdateResult? Read(string dataRoot)
    {
        try
        {
            var path = Path.Combine(dataRoot, "updates", "last-result.json");
            if (!File.Exists(path) || new FileInfo(path).Length > 64 * 1024) return null;
            return JsonSerializer.Deserialize<UpdateResult>(File.ReadAllBytes(path));
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException) { return null; }
    }
}

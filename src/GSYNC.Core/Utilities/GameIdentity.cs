using System.Security.Cryptography;
using System.Text;

namespace GSYNC.Core.Utilities;

public static class GameIdentity
{
    public static string ForManualName(string name) => "custom-" + StorageNamespace(name);

    public static string ForLudusaviTitle(string title)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(title);
        // The upstream catalog has distinct games whose titles differ only in case.
        return "ludusavi-" + Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(title))).ToLowerInvariant();
    }

    public static string StorageNamespace(string gameId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(gameId);
        var canonical = gameId.Trim().Normalize(NormalizationForm.FormC).ToUpperInvariant();
        var label = new StringBuilder();
        foreach (var character in gameId.Trim().ToLowerInvariant())
        {
            if (char.IsLetterOrDigit(character)) label.Append(character);
            else if (label.Length > 0 && label[^1] != '-') label.Append('-');
            if (label.Length >= 40) break;
        }
        var readable = label.ToString().Trim('-');
        if (readable.Length == 0) readable = "game";
        return readable + "-" + Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(canonical)))[..24].ToLowerInvariant();
    }

    public static string NamedStorageNamespace(string gameName, string gameId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(gameName);
        ArgumentException.ThrowIfNullOrWhiteSpace(gameId);
        var label = new StringBuilder();
        var hasEnglishName = gameName.Any(character => character is >= 'a' and <= 'z' or >= 'A' and <= 'Z');
        foreach (var character in gameName.Trim().Normalize(NormalizationForm.FormC).ToLowerInvariant())
        {
            if (hasEnglishName ? character is >= 'a' and <= 'z' or >= '0' and <= '9' : char.IsLetterOrDigit(character)) label.Append(character);
            else if (label.Length > 0 && label[^1] != '-') label.Append('-');
            if (label.Length >= 48) break;
        }
        var readable = label.ToString().Trim('-');
        if (readable.Length == 0) return StorageNamespace(gameId);
        var canonicalId = gameId.Trim().Normalize(NormalizationForm.FormC).ToUpperInvariant();
        return readable + "-" + Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(canonicalId)))[..12].ToLowerInvariant();
    }
}

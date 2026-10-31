using System.Security.Cryptography;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace GSYNC.Core.Updates;

/// <summary>A detached signature authenticates these exact manifest bytes before any fields are trusted.</summary>
public sealed record UpdateManifest(int FormatVersion, string Version, string Archive, string Sha256, long Size);

public static class UpdateManifestVerifier
{
    public const long MaximumArchiveBytes = 1024L * 1024 * 1024;
    public const int MaximumManifestBytes = 16 * 1024;
    public const string TrustedPublicKey = """
        -----BEGIN PUBLIC KEY-----
        MFkwEwYHKoZIzj0CAQYIKoZIzj0DAQcDQgAEu4fO6snSeTLSYTnx2LZcxl3J2vrp
        KDOCHkp8Ov5cwLcolzUIN9HQJpPqR46xh66J1NEBmw0qJjYw0Bl/K+T+FQ==
        -----END PUBLIC KEY-----
        """;

    public static UpdateManifest Verify(ReadOnlySpan<byte> manifestBytes, ReadOnlySpan<byte> signature, string expectedVersion, string? publicKey = null)
    {
        if (manifestBytes.Length is 0 or > MaximumManifestBytes || signature.Length != 64)
            throw new InvalidDataException("Invalid signed update metadata size.");
        using var key = ECDsa.Create();
        key.ImportFromPem(publicKey ?? TrustedPublicKey);
        if (!key.VerifyData(manifestBytes, signature, HashAlgorithmName.SHA256, DSASignatureFormat.IeeeP1363FixedFieldConcatenation))
            throw new CryptographicException("The update signature is invalid or not signed by the trusted release key.");
        UpdateManifest manifest;
        try { manifest = JsonSerializer.Deserialize<UpdateManifest>(manifestBytes) ?? throw new InvalidDataException("Empty update manifest."); }
        catch (JsonException ex) { throw new InvalidDataException("Invalid update manifest.", ex); }
        if (manifest.FormatVersion != 1 || !Regex.IsMatch(manifest.Version ?? "", @"^(0|[1-9]\d*)\.(0|[1-9]\d*)\.(0|[1-9]\d*)(?:-[0-9A-Za-z]+(?:[.-][0-9A-Za-z]+)*)?(?:\+[0-9A-Za-z]+(?:[.-][0-9A-Za-z]+)*)?$", RegexOptions.CultureInvariant) ||
            !string.Equals(manifest.Version, expectedVersion, StringComparison.Ordinal) ||
            !string.Equals(manifest.Archive, $"GSYNC-{expectedVersion}-win-x64.zip", StringComparison.Ordinal) ||
            manifest.Size is <= 0 or > MaximumArchiveBytes || !Regex.IsMatch(manifest.Sha256 ?? "", "^[0-9a-fA-F]{64}$", RegexOptions.CultureInvariant))
            throw new InvalidDataException("The signed update metadata does not match the requested release.");
        return manifest;
    }

    public static async Task VerifyArchiveAsync(string path, UpdateManifest manifest, CancellationToken token = default)
    {
        await using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read, 81920, useAsync: true);
        if (stream.Length != manifest.Size) throw new InvalidDataException("The signed update archive has an unexpected size.");
        var actual = await SHA256.HashDataAsync(stream, token);
        if (!CryptographicOperations.FixedTimeEquals(actual, Convert.FromHexString(manifest.Sha256)))
            throw new CryptographicException("The update archive does not match its signed SHA-256 digest.");
    }
}

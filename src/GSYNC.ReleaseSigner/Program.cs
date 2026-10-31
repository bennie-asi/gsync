using System.Security.Cryptography;
using System.Text.Json;
using GSYNC.Core.Updates;

if (args.Length != 2) throw new ArgumentException("Usage: GSYNC.ReleaseSigner <archive> <version>");
var archive = Path.GetFullPath(args[0]);
var version = args[1];
if (!string.Equals(Path.GetFileName(archive), $"GSYNC-{version}-win-x64.zip", StringComparison.Ordinal))
    throw new InvalidDataException("The archive name does not match the version.");
var encoded = Environment.GetEnvironmentVariable("GSYNC_SIGNING_KEY_PKCS8_BASE64");
if (string.IsNullOrWhiteSpace(encoded)) throw new InvalidOperationException("The release signing key is not configured.");
var privateKey = Convert.FromBase64String(encoded);
try
{
    using var key = ECDsa.Create();
    key.ImportPkcs8PrivateKey(privateKey, out var consumed);
    if (consumed != privateKey.Length) throw new CryptographicException("Invalid private key.");
    using var trusted = ECDsa.Create();
    trusted.ImportFromPem(UpdateManifestVerifier.TrustedPublicKey);
    if (!CryptographicOperations.FixedTimeEquals(key.ExportSubjectPublicKeyInfo(), trusted.ExportSubjectPublicKeyInfo()))
        throw new CryptographicException("The release signing key does not match the public key embedded in the app.");
    await using var input = File.OpenRead(archive);
    var manifest = new UpdateManifest(1, version, Path.GetFileName(archive), Convert.ToHexString(await SHA256.HashDataAsync(input)).ToLowerInvariant(), input.Length);
    var bytes = JsonSerializer.SerializeToUtf8Bytes(manifest);
    var signature = key.SignData(bytes, HashAlgorithmName.SHA256, DSASignatureFormat.IeeeP1363FixedFieldConcatenation);
    _ = UpdateManifestVerifier.Verify(bytes, signature, version);
    var prefix = Path.Combine(Path.GetDirectoryName(archive)!, Path.GetFileNameWithoutExtension(archive));
    await File.WriteAllBytesAsync(prefix + ".manifest.json", bytes);
    await File.WriteAllBytesAsync(prefix + ".manifest.sig", signature);
    Console.WriteLine("Signed release metadata created.");
}
finally { CryptographicOperations.ZeroMemory(privateKey); }

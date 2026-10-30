using System.Security.Cryptography;
using System.Text.Json;
using GSYNC.Core.Abstractions.Data;
using GSYNC.Core.Utilities;

namespace GSYNC.Data.Services;

/// <summary>DPAPI encrypts each secret for the current Windows account. Configuration contains only an opaque ID.</summary>
public sealed class ProtectedFileCredentialStore(IAppPathService paths) : ICredentialStore
{
    public string Store(IReadOnlyDictionary<string, string> secrets)
    {
        if (!OperatingSystem.IsWindows()) throw new PlatformNotSupportedException("Configure a system credential store for this operating system.");
        var plaintext = JsonSerializer.SerializeToUtf8Bytes(secrets);
        try
        {
            if (plaintext.Length > 1024 * 1024) throw new InvalidDataException("The credential is too large.");
            var encrypted = ProtectedData.Protect(plaintext, null, DataProtectionScope.CurrentUser);
            var reference = Guid.NewGuid().ToString("N");
            AtomicFile.WriteAllBytes(GetPath(reference), encrypted);
            return reference;
        }
        finally { CryptographicOperations.ZeroMemory(plaintext); }
    }

    public IReadOnlyDictionary<string, string> Read(string reference)
    {
        if (!OperatingSystem.IsWindows()) throw new PlatformNotSupportedException("Configure a system credential store for this operating system.");
        var path = GetPath(reference);
        if (new FileInfo(path).Length > 2 * 1024 * 1024) throw new InvalidDataException("The protected credential is too large.");
        var plaintext = ProtectedData.Unprotect(File.ReadAllBytes(path), null, DataProtectionScope.CurrentUser);
        try
        {
            return new Dictionary<string, string>(JsonSerializer.Deserialize<Dictionary<string, string>>(plaintext)
                ?? throw new InvalidDataException("The credential is empty."), StringComparer.OrdinalIgnoreCase);
        }
        finally { CryptographicOperations.ZeroMemory(plaintext); }
    }

    private string GetPath(string reference)
    {
        if (!Guid.TryParseExact(reference, "N", out _)) throw new InvalidDataException("Invalid credential reference.");
        return SafePath.ResolveUnderRoot(Path.Combine(paths.GetAppDataRoot(), "credentials"), reference + ".bin");
    }
}

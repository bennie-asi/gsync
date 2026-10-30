using GSYNC.Core.Abstractions.Data;

namespace GSYNC.Data.Services;

public sealed class CredentialSettingsService(ICredentialStore store)
{
    public const string ReferenceKey = "credentialId";
    private static readonly HashSet<string> SecretKeys = new(StringComparer.OrdinalIgnoreCase)
    { "password", "username", "token", "accessToken", "refreshToken", "apiKey", "clientSecret" };

    public static bool ContainsSecrets(IReadOnlyDictionary<string, string> settings) => settings.Keys.Any(SecretKeys.Contains);

    public Dictionary<string, string> Protect(IReadOnlyDictionary<string, string> settings)
    {
        var result = new Dictionary<string, string>(settings, StringComparer.OrdinalIgnoreCase);
        var secrets = settings.Where(pair => SecretKeys.Contains(pair.Key)).ToDictionary(pair => pair.Key, pair => pair.Value, StringComparer.OrdinalIgnoreCase);
        if (secrets.Count == 0) return result;
        if (result.TryGetValue(ReferenceKey, out var existing))
            foreach (var pair in store.Read(existing)) secrets.TryAdd(pair.Key, pair.Value);
        result[ReferenceKey] = store.Store(secrets);
        foreach (var key in SecretKeys) result.Remove(key);
        return result;
    }

    public Dictionary<string, string> Resolve(IReadOnlyDictionary<string, string> settings)
    {
        var result = new Dictionary<string, string>(settings, StringComparer.OrdinalIgnoreCase);
        if (result.TryGetValue(ReferenceKey, out var reference))
            foreach (var pair in store.Read(reference))
            {
                if (!SecretKeys.Contains(pair.Key)) throw new InvalidDataException("The credential contains an invalid setting.");
                result[pair.Key] = pair.Value;
            }
        return result;
    }
}

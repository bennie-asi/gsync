using System.Text.Json;
using GSYNC.Core.Abstractions.Data;
using GSYNC.Core.Utilities;
using GSYNC.Data.Services;

namespace GSYNC.App.Infrastructure.Configuration;

public sealed class SyncTargetConfig
{
    public Guid Id { get; init; } = Guid.NewGuid();

    public required string Name { get; set; }

    public required string ProviderId { get; set; }

    public Dictionary<string, string> Settings { get; set; } = new(StringComparer.OrdinalIgnoreCase);

    [System.Text.Json.Serialization.JsonIgnore]
    public bool CredentialsUnavailable { get; set; }

    public DateTimeOffset CreatedAtUtc { get; init; } = DateTimeOffset.UtcNow;

    public DateTimeOffset UpdatedAtUtc { get; set; } = DateTimeOffset.UtcNow;
}

public sealed class SyncTargetStore
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = true,
    };

    private readonly IAppPathService _appPathService;
    private readonly CredentialSettingsService _credentials;
    private readonly object _gate = new();
    private SyncTargetDocument? _document;

    public SyncTargetStore(IAppPathService appPathService, CredentialSettingsService? credentials = null)
    {
        _appPathService = appPathService;
        _credentials = credentials ?? new CredentialSettingsService(new ProtectedFileCredentialStore(appPathService));
    }

    public Dictionary<string, string> ResolveSettings(IReadOnlyDictionary<string, string> settings) => _credentials.Resolve(settings);

    public SyncTargetConfig? GetForEditing(Guid id)
    {
        var target = Get(id);
        if (target is not null)
        {
            try { target.Settings = ResolveSettings(target.Settings); }
            catch (Exception exception) when (exception is IOException or InvalidDataException or UnauthorizedAccessException or System.Security.Cryptography.CryptographicException)
            {
                target.Settings.Remove(CredentialSettingsService.ReferenceKey);
                target.CredentialsUnavailable = true;
            }
        }
        return target;
    }

    public event EventHandler? Changed;

    public IReadOnlyList<SyncTargetConfig> List()
    {
        lock (_gate)
        {
            return EnsureLoaded().Targets.Select(target => Clone(target)!).ToArray();
        }
    }

    public SyncTargetConfig? Get(Guid id)
    {
        lock (_gate)
        {
            return Clone(EnsureLoaded().Targets.FirstOrDefault(target => target.Id == id));
        }
    }

    public SyncTargetConfig? GetDefault()
    {
        lock (_gate)
        {
            var document = EnsureLoaded();
            return Clone(document.Targets.FirstOrDefault(target => target.Id == document.DefaultTargetId)
                ?? document.Targets.FirstOrDefault());
        }
    }

    public SyncTargetConfig? GetFirstByProvider(string providerId)
    {
        lock (_gate)
        {
            return Clone(EnsureLoaded().Targets.FirstOrDefault(target =>
                string.Equals(target.ProviderId, providerId, StringComparison.OrdinalIgnoreCase)));
        }
    }

    public void Upsert(SyncTargetConfig target)
    {
        lock (_gate)
        {
            var document = CopyDocument(EnsureLoaded());
            target = Clone(target)!;
            var index = document.Targets.FindIndex(existing => existing.Id == target.Id);
            target.UpdatedAtUtc = DateTimeOffset.UtcNow;
            if (index >= 0)
            {
                document.Targets[index] = target;
            }
            else
            {
                document.Targets.Add(target);
            }

            if (document.DefaultTargetId is null)
            {
                document.DefaultTargetId = target.Id;
            }

            Save(document);
        }

        Changed?.Invoke(this, EventArgs.Empty);
    }

    public void Remove(Guid id)
    {
        lock (_gate)
        {
            var document = CopyDocument(EnsureLoaded());
            document.Targets.RemoveAll(target => target.Id == id);
            if (document.DefaultTargetId == id)
            {
                document.DefaultTargetId = document.Targets.FirstOrDefault()?.Id;
            }

            Save(document);
        }

        Changed?.Invoke(this, EventArgs.Empty);
    }

    public void SetDefault(Guid id)
    {
        lock (_gate)
        {
            var document = CopyDocument(EnsureLoaded());
            if (document.Targets.Any(target => target.Id == id))
            {
                document.DefaultTargetId = id;
                Save(document);
            }
        }

        Changed?.Invoke(this, EventArgs.Empty);
    }

    private SyncTargetDocument EnsureLoaded()
    {
        if (_document is not null)
        {
            return _document;
        }

        var path = GetStorePath();
        if (File.Exists(path))
        {
            try
            {
                var document = JsonSerializer.Deserialize<SyncTargetDocument>(File.ReadAllText(path), JsonOptions)
                    ?? throw new InvalidDataException("The sync-target configuration is empty.");
                Validate(document);
                if (document.Targets.Any(target => CredentialSettingsService.ContainsSecrets(target.Settings))) Save(document);
                else _document = document;
            }
            catch (JsonException exception)
            {
                throw new InvalidDataException($"Cannot read sync targets from '{path}'. The original configuration has been preserved.", exception);
            }
        }

        if (_document is null)
        {
            Save(CreateSeedDocument());
        }

        return _document!;
    }

    private SyncTargetDocument CreateSeedDocument()
    {
        var defaultTarget = new SyncTargetConfig
        {
            Name = "Local Storage",
            ProviderId = "local-folder",
            Settings = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["rootPath"] = Path.Combine(_appPathService.GetAppDataRoot(), "storage"),
            },
        };

        return new SyncTargetDocument
        {
            DefaultTargetId = defaultTarget.Id,
            Targets = [defaultTarget],
        };
    }

    private void Save(SyncTargetDocument document)
    {
        Validate(document);
        document = CopyDocument(document);
        foreach (var target in document.Targets) target.Settings = _credentials.Protect(target.Settings);
        Directory.CreateDirectory(_appPathService.GetAppDataRoot());
        AtomicFile.WriteAllText(GetStorePath(), JsonSerializer.Serialize(document, JsonOptions));
        _document = document;
    }

    private static SyncTargetConfig? Clone(SyncTargetConfig? target) => target is null ? null : new SyncTargetConfig
    {
        Id = target.Id, Name = target.Name, ProviderId = target.ProviderId, CreatedAtUtc = target.CreatedAtUtc,
        UpdatedAtUtc = target.UpdatedAtUtc, Settings = new Dictionary<string, string>(target.Settings, StringComparer.OrdinalIgnoreCase),
    };

    private static SyncTargetDocument CopyDocument(SyncTargetDocument document) => new()
    {
        DefaultTargetId = document.DefaultTargetId, Targets = document.Targets.Select(target => Clone(target)!).ToList(),
    };

    private static void Validate(SyncTargetDocument document)
    {
        if (document.Targets is null || document.Targets.Any(target => target is null || target.Id == Guid.Empty ||
                string.IsNullOrWhiteSpace(target.Name) || string.IsNullOrWhiteSpace(target.ProviderId) || target.Settings is null) ||
            document.Targets.Select(target => target.Id).Distinct().Count() != document.Targets.Count)
            throw new InvalidDataException("The sync-target configuration contains invalid or duplicate targets.");
        foreach (var target in document.Targets)
        {
            var settings = new Dictionary<string, string>(target.Settings, StringComparer.OrdinalIgnoreCase);
            if (target.ProviderId.Equals("local-folder", StringComparison.OrdinalIgnoreCase) &&
                (!settings.TryGetValue("rootPath", out var path) || string.IsNullOrWhiteSpace(path) || !Path.IsPathFullyQualified(path)))
                throw new InvalidDataException("A local sync target requires an absolute folder path.");
            if (target.ProviderId.Equals("webdav", StringComparison.OrdinalIgnoreCase) &&
                (!settings.TryGetValue("baseUrl", out var url) || !Uri.TryCreate(url, UriKind.Absolute, out var endpoint) ||
                 (endpoint.Scheme != Uri.UriSchemeHttp && endpoint.Scheme != Uri.UriSchemeHttps) || endpoint.Query.Length != 0 || endpoint.Fragment.Length != 0 || endpoint.UserInfo.Length != 0))
                throw new InvalidDataException("A WebDAV sync target requires an absolute http(s) collection URL without a query or fragment.");
        }
    }

    private string GetStorePath()
    {
        return Path.Combine(_appPathService.GetAppDataRoot(), "sync-targets.json");
    }

    private sealed class SyncTargetDocument
    {
        public Guid? DefaultTargetId { get; set; }

        public List<SyncTargetConfig> Targets { get; set; } = [];
    }
}

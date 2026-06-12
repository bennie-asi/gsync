using System.Text.Json;
using GSYNC.Core.Abstractions.Data;

namespace GSYNC.App.Infrastructure.Configuration;

public sealed class SyncTargetConfig
{
    public Guid Id { get; init; } = Guid.NewGuid();

    public required string Name { get; set; }

    public required string ProviderId { get; set; }

    public Dictionary<string, string> Settings { get; set; } = new(StringComparer.OrdinalIgnoreCase);

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
    private readonly object _gate = new();
    private SyncTargetDocument? _document;

    public SyncTargetStore(IAppPathService appPathService)
    {
        _appPathService = appPathService;
    }

    public event EventHandler? Changed;

    public IReadOnlyList<SyncTargetConfig> List()
    {
        lock (_gate)
        {
            return EnsureLoaded().Targets.ToArray();
        }
    }

    public SyncTargetConfig? Get(Guid id)
    {
        lock (_gate)
        {
            return EnsureLoaded().Targets.FirstOrDefault(target => target.Id == id);
        }
    }

    public SyncTargetConfig? GetDefault()
    {
        lock (_gate)
        {
            var document = EnsureLoaded();
            return document.Targets.FirstOrDefault(target => target.Id == document.DefaultTargetId)
                ?? document.Targets.FirstOrDefault();
        }
    }

    public SyncTargetConfig? GetFirstByProvider(string providerId)
    {
        lock (_gate)
        {
            return EnsureLoaded().Targets.FirstOrDefault(target =>
                string.Equals(target.ProviderId, providerId, StringComparison.OrdinalIgnoreCase));
        }
    }

    public void Upsert(SyncTargetConfig target)
    {
        lock (_gate)
        {
            var document = EnsureLoaded();
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
            var document = EnsureLoaded();
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
            var document = EnsureLoaded();
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
                _document = JsonSerializer.Deserialize<SyncTargetDocument>(File.ReadAllText(path), JsonOptions);
            }
            catch
            {
                _document = null;
            }
        }

        if (_document is null || _document.Targets.Count == 0)
        {
            _document = CreateSeedDocument();
            Save(_document);
        }

        return _document;
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
        Directory.CreateDirectory(_appPathService.GetAppDataRoot());
        File.WriteAllText(GetStorePath(), JsonSerializer.Serialize(document, JsonOptions));
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

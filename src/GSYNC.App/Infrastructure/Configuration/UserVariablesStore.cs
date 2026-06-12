using System.Text.Json;
using GSYNC.Core.Abstractions.Data;

namespace GSYNC.App.Infrastructure.Configuration;

public sealed class UserVariable
{
    public required string Name { get; set; }

    public required string Value { get; set; }

    public string? Description { get; set; }

    public DateTimeOffset UpdatedAtUtc { get; set; } = DateTimeOffset.UtcNow;
}

public sealed class UserVariablesStore
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = true,
    };

    private readonly IAppPathService _appPathService;
    private readonly object _gate = new();
    private List<UserVariable>? _variables;

    public UserVariablesStore(IAppPathService appPathService)
    {
        _appPathService = appPathService;
    }

    public event EventHandler? Changed;

    public IReadOnlyList<UserVariable> List()
    {
        lock (_gate)
        {
            return EnsureLoaded().ToArray();
        }
    }

    public IReadOnlyDictionary<string, string> AsDictionary()
    {
        lock (_gate)
        {
            return EnsureLoaded().ToDictionary(
                variable => variable.Name.Trim('%'),
                variable => variable.Value,
                StringComparer.OrdinalIgnoreCase);
        }
    }

    public void Upsert(UserVariable variable)
    {
        lock (_gate)
        {
            var variables = EnsureLoaded();
            var index = variables.FindIndex(existing =>
                string.Equals(existing.Name, variable.Name, StringComparison.OrdinalIgnoreCase));
            variable.UpdatedAtUtc = DateTimeOffset.UtcNow;
            if (index >= 0)
            {
                variables[index] = variable;
            }
            else
            {
                variables.Add(variable);
            }

            Save(variables);
        }

        Changed?.Invoke(this, EventArgs.Empty);
    }

    public void Remove(string name)
    {
        lock (_gate)
        {
            var variables = EnsureLoaded();
            variables.RemoveAll(variable => string.Equals(variable.Name, name, StringComparison.OrdinalIgnoreCase));
            Save(variables);
        }

        Changed?.Invoke(this, EventArgs.Empty);
    }

    private List<UserVariable> EnsureLoaded()
    {
        if (_variables is not null)
        {
            return _variables;
        }

        var path = GetStorePath();
        if (File.Exists(path))
        {
            try
            {
                _variables = JsonSerializer.Deserialize<List<UserVariable>>(File.ReadAllText(path), JsonOptions);
            }
            catch
            {
                _variables = null;
            }
        }

        _variables ??= [];
        return _variables;
    }

    private void Save(List<UserVariable> variables)
    {
        Directory.CreateDirectory(_appPathService.GetAppDataRoot());
        File.WriteAllText(GetStorePath(), JsonSerializer.Serialize(variables, JsonOptions));
    }

    private string GetStorePath()
    {
        return Path.Combine(_appPathService.GetAppDataRoot(), "user-variables.json");
    }
}

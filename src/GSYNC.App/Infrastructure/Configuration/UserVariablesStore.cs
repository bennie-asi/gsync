using System.Text.Json;
using GSYNC.Core.Abstractions.Data;
using GSYNC.Core.Utilities;

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
            return EnsureLoaded().Select(Clone).ToArray();
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
        if (variable.Value is null) throw new InvalidDataException("A variable value cannot be null.");
        lock (_gate)
        {
            var variables = EnsureLoaded().Select(Clone).ToList();
            variable = Clone(variable);
            variable.Name = NormalizeName(variable.Name);
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
            var variables = EnsureLoaded().Select(Clone).ToList();
            variables.RemoveAll(variable => string.Equals(variable.Name, NormalizeName(name), StringComparison.OrdinalIgnoreCase));
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
                var variables = JsonSerializer.Deserialize<List<UserVariable>>(File.ReadAllText(path), JsonOptions)
                    ?? throw new InvalidDataException("The variable configuration is empty.");
                foreach (var variable in variables)
                {
                    if (variable is null || variable.Value is null) throw new InvalidDataException("Invalid user variable.");
                    variable.Name = NormalizeName(variable.Name);
                }
                if (variables.Select(variable => variable.Name).Distinct(StringComparer.OrdinalIgnoreCase).Count() != variables.Count)
                    throw new InvalidDataException("The variable configuration contains duplicate names.");
                _variables = variables;
            }
            catch (JsonException exception)
            {
                throw new InvalidDataException($"Cannot read variables from '{path}'. The original configuration has been preserved.", exception);
            }
        }

        _variables ??= [];
        return _variables;
    }

    private void Save(List<UserVariable> variables)
    {
        Directory.CreateDirectory(_appPathService.GetAppDataRoot());
        AtomicFile.WriteAllText(GetStorePath(), JsonSerializer.Serialize(variables, JsonOptions));
        _variables = variables;
    }

    private static UserVariable Clone(UserVariable variable) => new()
    {
        Name = variable.Name, Value = variable.Value, Description = variable.Description, UpdatedAtUtc = variable.UpdatedAtUtc,
    };

    private static string NormalizeName(string name)
    {
        var key = name?.Trim().Trim('%').ToUpperInvariant();
        if (string.IsNullOrEmpty(key) || key.Any(character => !char.IsAsciiLetterOrDigit(character) && character != '_'))
            throw new InvalidDataException("Variable names may contain only letters, digits and underscores.");
        return "%" + key + "%";
    }

    private string GetStorePath()
    {
        return Path.Combine(_appPathService.GetAppDataRoot(), "user-variables.json");
    }
}

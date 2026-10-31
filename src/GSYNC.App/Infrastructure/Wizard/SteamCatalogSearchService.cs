using System.Text.Json;
using System.Text.RegularExpressions;

namespace GSYNC.App.Infrastructure.Wizard;

/// <summary>Searches public Steam titles so localized names can be mapped to local manifest IDs.</summary>
public sealed class SteamCatalogSearchService(HttpClient client)
{
    public async Task<IReadOnlyList<SteamCatalogTitle>> SearchAsync(string name, bool chinese, CancellationToken cancellationToken)
    {
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(TimeSpan.FromSeconds(5));
        var results = await SearchTermAsync(name, chinese, timeout.Token);
        if (results.Count > 0) return results;

        // Steam treats a Chinese title followed by a number differently when the
        // separating space is missing (for example, 生化危机2 / 生化危机 2).
        var spaced = Regex.Replace(name, @"(?<=[\p{L}])(?=\d)|(?<=\d)(?=[\p{L}])", " ");
        return spaced == name ? results : await SearchTermAsync(spaced, chinese, timeout.Token);
    }

    private async Task<IReadOnlyList<SteamCatalogTitle>> SearchTermAsync(string name, bool chinese, CancellationToken cancellationToken)
    {
        var language = chinese ? "schinese" : "english";
        var country = chinese ? "CN" : "US";
        var url = $"https://store.steampowered.com/api/storesearch/?term={Uri.EscapeDataString(name)}&l={language}&cc={country}";
        using var response = await client.GetAsync(url, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
        response.EnsureSuccessStatusCode();
        const int maxBytes = 1024 * 1024;
        if (response.Content.Headers.ContentLength > maxBytes) throw new InvalidDataException("Steam search response is too large.");
        await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
        using var buffer = new MemoryStream();
        var bytes = new byte[8192];
        int count;
        while ((count = await stream.ReadAsync(bytes, cancellationToken)) > 0)
        {
            if (buffer.Length + count > maxBytes) throw new InvalidDataException("Steam search response is too large.");
            await buffer.WriteAsync(bytes.AsMemory(0, count), cancellationToken);
        }
        buffer.Position = 0;
        using var document = await JsonDocument.ParseAsync(buffer, cancellationToken: cancellationToken);
        if (document.RootElement.ValueKind != JsonValueKind.Object ||
            !document.RootElement.TryGetProperty("items", out var items) || items.ValueKind != JsonValueKind.Array) return [];
        var results = new List<SteamCatalogTitle>();
        foreach (var item in items.EnumerateArray().Take(50))
        {
            if (item.ValueKind != JsonValueKind.Object ||
                !item.TryGetProperty("id", out var id) || id.ValueKind != JsonValueKind.Number || !id.TryGetInt64(out var numericId) || numericId <= 0 ||
                !item.TryGetProperty("name", out var title) || title.ValueKind != JsonValueKind.String)
                continue;
            var titleText = title.GetString();
            if (!string.IsNullOrWhiteSpace(titleText) && titleText.Length <= 256)
                results.Add(new SteamCatalogTitle(numericId.ToString(System.Globalization.CultureInfo.InvariantCulture), titleText));
        }
        return results;
    }
}

public sealed record SteamCatalogTitle(string SteamId, string Name);

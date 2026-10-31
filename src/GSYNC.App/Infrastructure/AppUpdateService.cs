using System.Net;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using GSYNC.Core.Utilities;
using GSYNC.Core.Updates;

namespace GSYNC.App.Infrastructure;

/// <summary>Checks the official stable release and downloads its x64 archive without executing it.</summary>
public sealed class AppUpdateService
{
    private const string ApiUrl = "https://api.github.com/repos/bennie-asi/gsync/releases/latest";
    private const long MaxArchiveBytes = 1024L * 1024 * 1024;
    private static readonly Regex StableVersion = new(@"^(0|[1-9]\d*)\.(0|[1-9]\d*)\.(0|[1-9]\d*)(?:\+[0-9A-Za-z.-]+)?$", RegexOptions.CultureInvariant);
    private readonly HttpClient _http;
    private readonly string _directory;
    private readonly string _currentVersion;
    private readonly TimeProvider _clock;
    private readonly string? _trustedPublicKey;
    private readonly SemaphoreSlim _checkGate = new(1, 1);
    private readonly SemaphoreSlim _downloadGate = new(1, 1);
    private UpdateSnapshot _snapshot;
    private DownloadedRelease? _lastDownload;

    public event EventHandler? Changed;
    public UpdateSnapshot Snapshot => Volatile.Read(ref _snapshot);
    public DownloadedRelease? LastDownload => Volatile.Read(ref _lastDownload);

    public AppUpdateService(string dataRoot, HttpClient http, string currentVersion, TimeProvider? clock = null, string? trustedPublicKey = null)
    {
        _directory = Path.Combine(dataRoot, "updates");
        _http = http;
        _currentVersion = currentVersion;
        _clock = clock ?? TimeProvider.System;
        _trustedPublicKey = trustedPublicKey;
        _snapshot = LoadSnapshot();
    }

    public static HttpClient CreateHttpClient()
    {
        var client = new HttpClient(new HttpClientHandler { AllowAutoRedirect = false }) { Timeout = TimeSpan.FromMinutes(10) };
        client.DefaultRequestHeaders.UserAgent.ParseAdd("GSYNC-Desktop");
        client.DefaultRequestHeaders.Accept.ParseAdd("application/vnd.github+json");
        return client;
    }

    public async Task CheckAsync(bool automatic, CancellationToken token)
    {
        await _checkGate.WaitAsync(token);
        try
        {
            var previous = Snapshot;
            var now = _clock.GetUtcNow();
            if (automatic && previous.CheckedCurrentVersion == _currentVersion && previous.LastAttempt is { } last && now >= last && now - last < TimeSpan.FromDays(1)) return;
            Set(previous with { State = "checking", Message = null });
            try
            {
                using var timeout = CancellationTokenSource.CreateLinkedTokenSource(token);
                timeout.CancelAfter(TimeSpan.FromSeconds(20));
                var checkToken = timeout.Token;
                using var request = new HttpRequestMessage(HttpMethod.Get, ApiUrl);
                using var response = await _http.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, checkToken);
                UpdateSnapshot next;
                if (response.StatusCode == HttpStatusCode.NotFound) next = new("none", now, previous.LastSuccess, _currentVersion);
                else if (response.StatusCode == HttpStatusCode.TooManyRequests ||
                    (response.StatusCode == HttpStatusCode.Forbidden &&
                     (response.Headers.RetryAfter is not null ||
                      (response.Headers.TryGetValues("X-RateLimit-Remaining", out var remaining) && remaining.Contains("0")))))
                    next = new("limited", now, previous.LastSuccess, _currentVersion);
                else
                {
                    response.EnsureSuccessStatusCode();
                    if (response.Content.Headers.ContentLength > 4 * 1024 * 1024) throw new InvalidDataException("Release information is too large.");
                    await using var stream = await response.Content.ReadAsStreamAsync(checkToken);
                    using var limited = new MemoryStream();
                    var buffer = new byte[16384];
                    int count;
                    while ((count = await stream.ReadAsync(buffer, checkToken)) != 0)
                    {
                        if (limited.Length + count > 4 * 1024 * 1024) throw new InvalidDataException("Release information is too large.");
                        limited.Write(buffer, 0, count);
                    }
                    limited.Position = 0;
                    var release = await JsonSerializer.DeserializeAsync<Release>(limited, cancellationToken: checkToken) ?? throw new InvalidDataException("Empty release response.");
                    next = ParseRelease(release, _currentVersion, now);
                    if (next.LastSuccess is null) next = next with { LastSuccess = previous.LastSuccess };
                }
                Set(next);
                SaveSnapshot(next);
            }
            catch (OperationCanceledException) when (token.IsCancellationRequested) { Set(previous); throw; }
            catch (OperationCanceledException)
            {
                var failed = new UpdateSnapshot("error", now, previous.LastSuccess, _currentVersion, Message: "The update check timed out.");
                Set(failed);
                SaveSnapshot(failed);
            }
            catch (Exception ex) when (ex is HttpRequestException or IOException or JsonException or TaskCanceledException or InvalidOperationException)
            {
                var failed = new UpdateSnapshot("error", now, previous.LastSuccess, _currentVersion, Message: ex.Message);
                Set(failed);
                SaveSnapshot(failed);
            }
        }
        finally { _checkGate.Release(); }
    }

    private static UpdateSnapshot ParseRelease(Release release, string current, DateTimeOffset now)
    {
        var tag = release.TagName;
        var normalized = tag?.StartsWith('v') == true ? tag[1..] : tag;
        if (release.Draft || release.Prerelease || normalized is null || !TryStableVersion(normalized, out var available))
            return new("invalid", now, null, current);
        var currentParts = current.Split('+', 2)[0];
        var currentStable = currentParts.Split('-', 2)[0];
        if (!TryStableVersion(currentStable, out var installed)) return new("invalid", now, null, current);
        var newer = available > installed || (available == installed && currentParts.Contains('-'));
        if (!newer) return new("current", now, now, current, Version: normalized);
        var expectedName = $"GSYNC-{normalized}-win-x64.zip";
        var asset = release.Assets?.FirstOrDefault(a => string.Equals(a.Name, expectedName, StringComparison.Ordinal));
        if (asset is null) return new("noasset", now, now, current, Version: normalized, Notes: LimitNotes(release.Body));
        var expectedUrl = $"https://github.com/bennie-asi/gsync/releases/download/{Uri.EscapeDataString(tag!)}/{Uri.EscapeDataString(expectedName)}";
        if (!string.Equals(asset.DownloadUrl, expectedUrl, StringComparison.Ordinal) || asset.Size is <= 0 or > MaxArchiveBytes ||
            asset.Digest is null || !Regex.IsMatch(asset.Digest, @"^sha256:[0-9a-fA-F]{64}$", RegexOptions.CultureInvariant))
            return new("unverified", now, now, current, Version: normalized, Notes: LimitNotes(release.Body));
        var metadataPrefix = $"GSYNC-{normalized}-win-x64";
        string? MetadataUrl(string suffix, long maximum)
        {
            var name = metadataPrefix + suffix;
            var metadata = release.Assets?.FirstOrDefault(a => string.Equals(a.Name, name, StringComparison.Ordinal));
            var url = $"https://github.com/bennie-asi/gsync/releases/download/{Uri.EscapeDataString(tag!)}/{Uri.EscapeDataString(name)}";
            return metadata is not null && metadata.Size > 0 && metadata.Size <= maximum && string.Equals(metadata.DownloadUrl, url, StringComparison.Ordinal) ? url : null;
        }
        return new("available", now, now, current, normalized, LimitNotes(release.Body), asset.DownloadUrl, asset.Digest[7..].ToLowerInvariant(), asset.Size,
            ManifestUrl: MetadataUrl(".manifest.json", UpdateManifestVerifier.MaximumManifestBytes), SignatureUrl: MetadataUrl(".manifest.sig", 64));
    }

    private static bool TryStableVersion(string text, out Version version)
    {
        version = new Version(0, 0, 0);
        var match = StableVersion.Match(text);
        if (!match.Success || !int.TryParse(match.Groups[1].Value, out var major) || !int.TryParse(match.Groups[2].Value, out var minor) || !int.TryParse(match.Groups[3].Value, out var patch)) return false;
        version = new Version(major, minor, patch);
        return true;
    }

    private static string? LimitNotes(string? notes) => notes is { Length: > 16000 } ? notes[..16000] : notes;

    public async Task<string> DownloadAsync(IProgress<DownloadProgress>? progress, CancellationToken token)
    {
        await _downloadGate.WaitAsync(token);
        string? temporary = null;
        try
        {
            var release = Snapshot;
            if (release.State != "available" || release.DownloadUrl is null || release.Sha256 is null || release.Size is null) throw new InvalidOperationException("No verified release is available for download.");
            if (release.Version is null || !TryStableVersion(release.Version, out _) || release.Size <= 0 || release.Size > MaxArchiveBytes ||
                !Regex.IsMatch(release.Sha256, @"^[0-9a-fA-F]{64}$", RegexOptions.CultureInvariant)) throw new InvalidDataException("Invalid saved release information.");
            var expectedFile = $"GSYNC-{release.Version}-win-x64.zip";
            var baseUrl = "https://github.com/bennie-asi/gsync/releases/download/";
            var validUrl = baseUrl + Uri.EscapeDataString(release.Version) + "/" + Uri.EscapeDataString(expectedFile);
            var validVUrl = baseUrl + Uri.EscapeDataString("v" + release.Version) + "/" + Uri.EscapeDataString(expectedFile);
            if (!string.Equals(release.DownloadUrl, validUrl, StringComparison.Ordinal) && !string.Equals(release.DownloadUrl, validVUrl, StringComparison.Ordinal)) throw new InvalidDataException("Invalid saved download address.");
            Directory.CreateDirectory(_directory);
            temporary = Path.Combine(_directory, $".download-{Guid.NewGuid():N}.tmp");
            using var response = await GetArchiveAsync(new Uri(release.DownloadUrl), token);
            response.EnsureSuccessStatusCode();
            if (response.Content.Headers.ContentLength is long size && size != release.Size) throw new InvalidDataException("Download size does not match the release.");
            await using (var input = await response.Content.ReadAsStreamAsync(token))
            await using (var output = new FileStream(temporary, FileMode.CreateNew, FileAccess.Write, FileShare.None, 81920, true))
            using (var hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256))
            {
                var buffer = new byte[81920]; long total = 0; int read;
                while ((read = await input.ReadAsync(buffer, token)) != 0)
                {
                    total += read;
                    if (total > release.Size || total > MaxArchiveBytes) throw new InvalidDataException("Download exceeds the expected size.");
                    hash.AppendData(buffer, 0, read);
                    await output.WriteAsync(buffer.AsMemory(0, read), token);
                    progress?.Report(new DownloadProgress(total, release.Size.Value));
                }
                if (total != release.Size || !string.Equals(Convert.ToHexString(hash.GetHashAndReset()), release.Sha256, StringComparison.OrdinalIgnoreCase))
                    throw new InvalidDataException("The downloaded archive did not pass its size and SHA-256 checks.");
                await output.FlushAsync(token);
                output.Flush(true);
            }
            token.ThrowIfCancellationRequested();
            byte[]? manifestBytes = null;
            byte[]? signatureBytes = null;
            if (release.ManifestUrl is not null && release.SignatureUrl is not null)
            {
                var urlPrefix = release.DownloadUrl[..(release.DownloadUrl.LastIndexOf('/') + 1)];
                var metadataPrefix = $"GSYNC-{release.Version}-win-x64";
                if (release.ManifestUrl != urlPrefix + Uri.EscapeDataString(metadataPrefix + ".manifest.json") ||
                    release.SignatureUrl != urlPrefix + Uri.EscapeDataString(metadataPrefix + ".manifest.sig"))
                    throw new InvalidDataException("Invalid signed metadata address.");
                using var timeout = CancellationTokenSource.CreateLinkedTokenSource(token);
                timeout.CancelAfter(TimeSpan.FromSeconds(30));
                manifestBytes = await DownloadMetadataAsync(release.ManifestUrl, UpdateManifestVerifier.MaximumManifestBytes, timeout.Token);
                signatureBytes = await DownloadMetadataAsync(release.SignatureUrl, 64, timeout.Token);
                var manifest = UpdateManifestVerifier.Verify(manifestBytes, signatureBytes, release.Version, _trustedPublicKey);
                await UpdateManifestVerifier.VerifyArchiveAsync(temporary, manifest, timeout.Token);
            }
            token.ThrowIfCancellationRequested();
            // A fresh name avoids replacing an existing user file.
            var destination = Path.Combine(_directory, $"GSYNC-{release.Version}-win-x64-{Guid.NewGuid():N}.zip");
            File.Move(temporary, destination);
            temporary = null;
            Volatile.Write(ref _lastDownload, new DownloadedRelease(release.Version, destination, manifestBytes, signatureBytes));
            return destination;
        }
        finally
        {
            if (temporary is not null) { try { File.Delete(temporary); } catch (IOException) { } catch (UnauthorizedAccessException) { } }
            _downloadGate.Release();
        }
    }

    private async Task<byte[]> DownloadMetadataAsync(string url, int maximum, CancellationToken token)
    {
        using var response = await GetArchiveAsync(new Uri(url), token);
        response.EnsureSuccessStatusCode();
        if (response.Content.Headers.ContentLength > maximum) throw new InvalidDataException("Signed update metadata is too large.");
        await using var input = await response.Content.ReadAsStreamAsync(token);
        using var output = new MemoryStream();
        var buffer = new byte[4096]; int read;
        while ((read = await input.ReadAsync(buffer, token)) != 0)
        {
            if (output.Length + read > maximum) throw new InvalidDataException("Signed update metadata is too large.");
            output.Write(buffer, 0, read);
        }
        return output.ToArray();
    }

    private async Task<HttpResponseMessage> GetArchiveAsync(Uri url, CancellationToken token)
    {
        for (var redirects = 0; redirects < 5; redirects++)
        {
            if (url.Scheme != Uri.UriSchemeHttps || url.UserInfo.Length != 0 || !url.IsDefaultPort ||
                !(url.Host.Equals("github.com", StringComparison.OrdinalIgnoreCase) || url.Host.Equals("release-assets.githubusercontent.com", StringComparison.OrdinalIgnoreCase) || url.Host.Equals("objects.githubusercontent.com", StringComparison.OrdinalIgnoreCase)))
                throw new InvalidDataException("The update download redirected to an unexpected host.");
            using var request = new HttpRequestMessage(HttpMethod.Get, url);
            request.Headers.Accept.Clear();
            request.Headers.Accept.ParseAdd("application/octet-stream");
            var response = await _http.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, token);
            if ((int)response.StatusCode is < 300 or >= 400) return response;
            var location = response.Headers.Location;
            response.Dispose();
            if (location is null) throw new InvalidDataException("Missing download redirect.");
            url = location.IsAbsoluteUri ? location : new Uri(url, location);
        }
        throw new InvalidDataException("Too many download redirects.");
    }

    private UpdateSnapshot LoadSnapshot()
    {
        try
        {
            var path = Path.Combine(_directory, "check.json");
            if (File.Exists(path) && new FileInfo(path).Length < 65536)
            {
                var result = JsonSerializer.Deserialize<UpdateSnapshot>(File.ReadAllText(path));
                if (result?.CheckedCurrentVersion == _currentVersion && result.State != "checking") return result;
            }
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException) { }
        return new("unchecked", null, null, _currentVersion);
    }

    private void SaveSnapshot(UpdateSnapshot value)
    {
        try { AtomicFile.WriteAllText(Path.Combine(_directory, "check.json"), JsonSerializer.Serialize(value)); }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { }
    }

    private void Set(UpdateSnapshot value)
    {
        Volatile.Write(ref _snapshot, value);
        if (Changed is { } handlers)
            foreach (EventHandler handler in handlers.GetInvocationList()) { try { handler(this, EventArgs.Empty); } catch { } }
    }

    private sealed record Release([property: JsonPropertyName("tag_name")] string? TagName, [property: JsonPropertyName("draft")] bool Draft,
        [property: JsonPropertyName("prerelease")] bool Prerelease, [property: JsonPropertyName("body")] string? Body,
        [property: JsonPropertyName("assets")] ReleaseAsset[]? Assets);
    private sealed record ReleaseAsset([property: JsonPropertyName("name")] string? Name, [property: JsonPropertyName("browser_download_url")] string? DownloadUrl,
        [property: JsonPropertyName("digest")] string? Digest, [property: JsonPropertyName("size")] long Size);
}

public sealed record UpdateSnapshot(string State, DateTimeOffset? LastAttempt, DateTimeOffset? LastSuccess, string CheckedCurrentVersion,
    string? Version = null, string? Notes = null, string? DownloadUrl = null, string? Sha256 = null, long? Size = null, string? Message = null,
    string? ManifestUrl = null, string? SignatureUrl = null);
public sealed record DownloadProgress(long Downloaded, long Total);
public sealed record DownloadedRelease(string Version, string Path, byte[]? Manifest = null, byte[]? Signature = null)
{
    public bool IsSigned => Manifest is not null && Signature is not null;
}

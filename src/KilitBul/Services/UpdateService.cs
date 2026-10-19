using System.Diagnostics;
using System.IO;
using System.Net.Http;
using System.Reflection;
using System.Security.Cryptography;
using System.Text.Json;

namespace KilitBul.Services;

public sealed record UpdateInfo(Version Version, string DownloadUrl, string FileName, string? Sha256, string PageUrl);

/// <summary>
/// GitHub Releases üzerinden güncelleme denetimi: son sürüm daha yeniyse kurulum dosyasını indirip başlatır.
/// </summary>
public static class UpdateService
{
    public const string Repository = "coraspirin/KilitBul";
    private const string LatestReleaseUrl = $"https://api.github.com/repos/{Repository}/releases/latest";

    public static Version CurrentVersion { get; } = Trim(Assembly.GetExecutingAssembly().GetName().Version ?? new Version(0, 0));

    private static readonly HttpClient Http = CreateClient();

    private static HttpClient CreateClient()
    {
        var client = new HttpClient { Timeout = TimeSpan.FromMinutes(5) };
        client.DefaultRequestHeaders.UserAgent.ParseAdd($"KilitBul/{CurrentVersion}");
        return client;
    }

    /// <summary>Daha yeni bir sürüm varsa bilgisini döndürür; yoksa veya ağ hatasında null.</summary>
    public static async Task<UpdateInfo?> CheckAsync(CancellationToken ct = default)
    {
        try
        {
            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(ct);
            timeout.CancelAfter(TimeSpan.FromSeconds(10));
            using var request = new HttpRequestMessage(HttpMethod.Get, LatestReleaseUrl);
            request.Headers.Accept.ParseAdd("application/vnd.github+json");
            using var response = await Http.SendAsync(request, timeout.Token);
            if (!response.IsSuccessStatusCode)
                return null;
            var json = await response.Content.ReadAsStringAsync(timeout.Token);
            return ParseRelease(json, CurrentVersion);
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or JsonException)
        {
            return null; // çevrimdışı vb.; sessizce geç
        }
    }

    /// <summary>GitHub "latest release" yanıtını yorumlar; sürüm <paramref name="current"/>'ten yeni değilse null.</summary>
    internal static UpdateInfo? ParseRelease(string json, Version current)
    {
        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;

        if (root.TryGetProperty("draft", out var draft) && draft.GetBoolean()
            || root.TryGetProperty("prerelease", out var pre) && pre.GetBoolean())
            return null;

        var tag = root.GetProperty("tag_name").GetString() ?? "";
        if (!Version.TryParse(tag.TrimStart('v', 'V'), out var version) || Trim(version) <= current)
            return null;

        if (!root.TryGetProperty("assets", out var assets))
            return null;
        foreach (var asset in assets.EnumerateArray())
        {
            var name = asset.GetProperty("name").GetString() ?? "";
            if (!name.StartsWith("KilitBul-Setup-", StringComparison.OrdinalIgnoreCase)
                || !name.EndsWith(".exe", StringComparison.OrdinalIgnoreCase))
                continue;

            string? sha = null;
            if (asset.TryGetProperty("digest", out var digest) && digest.GetString() is { } d
                && d.StartsWith("sha256:", StringComparison.OrdinalIgnoreCase))
                sha = d["sha256:".Length..];

            return new UpdateInfo(Trim(version), asset.GetProperty("browser_download_url").GetString()!, name, sha,
                root.TryGetProperty("html_url", out var page) ? page.GetString() ?? "" : "");
        }
        return null;
    }

    /// <summary>Kurulum dosyasını geçici klasöre indirir, özetini doğrular ve döndürür.</summary>
    public static async Task<string> DownloadAsync(UpdateInfo update, IProgress<double>? progress = null, CancellationToken ct = default)
    {
        var dir = Path.Combine(Path.GetTempPath(), "KilitBul");
        Directory.CreateDirectory(dir);
        var path = Path.Combine(dir, Path.GetFileName(update.FileName));

        using (var response = await Http.GetAsync(update.DownloadUrl, HttpCompletionOption.ResponseHeadersRead, ct))
        {
            response.EnsureSuccessStatusCode();
            var total = response.Content.Headers.ContentLength;
            await using var source = await response.Content.ReadAsStreamAsync(ct);
            await using var target = File.Create(path);
            var buffer = new byte[81920];
            long read = 0;
            int n;
            while ((n = await source.ReadAsync(buffer, ct)) > 0)
            {
                await target.WriteAsync(buffer.AsMemory(0, n), ct);
                read += n;
                if (total > 0)
                    progress?.Report((double)read / total.Value);
            }
        }

        if (update.Sha256 is { } expected)
        {
            await using var file = File.OpenRead(path);
            var actual = Convert.ToHexString(await SHA256.HashDataAsync(file, ct));
            if (!actual.Equals(expected, StringComparison.OrdinalIgnoreCase))
            {
                File.Delete(path);
                throw new InvalidDataException("İndirilen dosya doğrulanamadı (SHA-256 uyuşmuyor).");
            }
        }
        return path;
    }

    /// <summary>Kurulumu başlatır (UAC onayı kurulum tarafından istenir). Kullanıcı UAC'yi iptal ederse false.</summary>
    public static bool LaunchInstaller(string path)
    {
        try
        {
            Process.Start(new ProcessStartInfo(path) { UseShellExecute = true });
            return true;
        }
        catch (System.ComponentModel.Win32Exception ex) when (ex.NativeErrorCode == 1223) // ERROR_CANCELLED
        {
            return false;
        }
    }

    private static Version Trim(Version v) => new(v.Major, v.Minor, Math.Max(v.Build, 0));
}

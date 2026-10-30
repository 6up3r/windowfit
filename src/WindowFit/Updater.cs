using System.Diagnostics;
using System.Net;
using System.Net.Http.Headers;
using System.Runtime.InteropServices;
using System.Security.Principal;
using WindowFit.Core.Updates;

namespace WindowFit;

/// <summary>
/// Finds the latest release on GitHub, trusts it only through its manifest signed with windowfit's release key, and
/// installs it: the setup is downloaded into the install folder (writable only by Administrators), checked against the
/// manifest while held open against writes, and started /SILENT with its temporary folder there too and only the
/// system's environment names, so no unelevated process can swap or steer what this elevated one runs.
/// </summary>
internal sealed class Updater : IDisposable
{
    private static readonly string InstallDir =
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "windowfit");
    private static readonly string UpdatesDir = Path.Combine(InstallDir, "updates");

    private readonly HttpClient _http = new() { Timeout = TimeSpan.FromMinutes(5) };
    private readonly string? _localSource;

    /// <param name="localSource">Debug builds only: a folder holding release.json, its signature and the setups, read
    /// as the latest release so the install path can be tried before a real release.</param>
    public Updater(string? localSource = null)
    {
        _http.DefaultRequestHeaders.UserAgent.Add(new ProductInfoHeaderValue("windowfit", Current.ToString(3)));
        _localSource = localSource;
    }

    public static Version Current { get; } = typeof(Updater).Assembly.GetName().Version ?? new Version(0, 0, 0);

    public static bool IsElevated { get; } = new WindowsPrincipal(WindowsIdentity.GetCurrent()).IsInRole(WindowsBuiltInRole.Administrator);

    /// <summary>Elevated and running from the install folder: only then may this process run a setup.</summary>
    public static bool CanInstall =>
        IsElevated && string.Equals(Path.GetDirectoryName(Environment.ProcessPath), InstallDir, StringComparison.OrdinalIgnoreCase);

    /// <summary>The newer release this system can install, or null; any failure throws, and the caller decides
    /// whether to say so.</summary>
    public async Task<UpdateOffer?> CheckAsync(CancellationToken ct)
    {
        var current = Current;
        if (!ReleaseCheck.IsRelease(current) || ReleaseCheck.ArchName(RuntimeInformation.OSArchitecture) is not { } arch)
        {
            return null;
        }
        LatestRelease latest;
        if (_localSource is { } dir)
        {
            var files = Directory.EnumerateFiles(dir).ToDictionary(f => Path.GetFileName(f), f => f, StringComparer.Ordinal);
            var local = ReleaseManifest.Verify(File.ReadAllBytes(files[ReleaseManifest.FileName]),
                File.ReadAllBytes(files[ReleaseManifest.SignatureName]), ReleaseKey.PublicKeys);
            latest = new LatestRelease(ReleaseCheck.ParseTag("v" + local.Version), files);
        }
        else
        {
            // GitHub's JSON for the API call only: the downloads are plain files behind a redirect.
            using var request = new HttpRequestMessage(HttpMethod.Get, ReleaseCheck.LatestUrl);
            request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/vnd.github+json"));
            using var response = await _http.SendAsync(request, ct);
            // No release published yet.
            if (response.StatusCode == HttpStatusCode.NotFound)
            {
                return null;
            }
            response.EnsureSuccessStatusCode();
            latest = ReleaseCheck.ParseLatest(await response.Content.ReadAsByteArrayAsync(ct));
        }
        if (latest.Version <= ReleaseCheck.Normalize(current))
        {
            return null;
        }
        var (manifestUrl, signatureUrl) = ReleaseCheck.ManifestUrls(latest);
        var manifest = ReleaseManifest.Verify(await FetchAsync(manifestUrl, ct), await FetchAsync(signatureUrl, ct), ReleaseKey.PublicKeys);
        return ReleaseCheck.Offer(latest, manifest, current, arch);
    }

    /// <summary>Downloads, checks and starts the setup; returns once it runs. Throws before starting anything when the
    /// download fails or does not match the signed manifest.</summary>
    public async Task InstallAsync(UpdateOffer offer, CancellationToken ct)
    {
        if (!CanInstall)
        {
            throw new InvalidOperationException("only the installed, elevated windowfit installs updates");
        }
        if (Path.GetFileName(offer.Asset.Name) != offer.Asset.Name)
        {
            throw new InvalidDataException($"the release names its setup {offer.Asset.Name}");
        }
        var temp = Path.Combine(UpdatesDir, "temp");
        Directory.CreateDirectory(temp);
        var path = Path.Combine(UpdatesDir, offer.Asset.Name);
        await using (var output = new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.None))
        {
            await using var input = await OpenAsync(offer.Url, ct);
            var buffer = new byte[1 << 16];
            long done = 0;
            int n;
            while ((n = await input.ReadAsync(buffer, ct)) > 0)
            {
                done += n;
                // A larger download cannot match the manifest: stop before it fills the disk.
                if (done > offer.Asset.Size)
                {
                    throw new InvalidDataException($"the download is larger than the {offer.Asset.Size} bytes the release says");
                }
                await output.WriteAsync(buffer.AsMemory(0, n), ct);
            }
        }
        // Held without write or delete sharing from the check until the setup has started: nothing replaces it between.
        using var held = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read);
        ReleaseCheck.CheckDownload(held, offer.Asset);
        var start = new ProcessStartInfo(path, "/SILENT /NORESTART") { UseShellExecute = false };
        var inherited = Environment.GetEnvironmentVariables().Cast<System.Collections.DictionaryEntry>()
            .ToDictionary(e => (string)e.Key, e => (string?)e.Value, StringComparer.OrdinalIgnoreCase);
        var environment = ReleaseCheck.SetupEnvironment(inherited, temp, Environment.GetFolderPath(Environment.SpecialFolder.Windows));
        start.Environment.Clear();
        foreach (var (name, value) in environment)
        {
            start.Environment[name] = value;
        }
        using var setup = Process.Start(start) ?? throw new IOException("the setup did not start");
    }

    /// <summary>Removes what the last update left, once the new windowfit runs.</summary>
    public static void CleanUp()
    {
        if (!CanInstall || !Directory.Exists(UpdatesDir))
        {
            return;
        }
        try
        {
            Directory.Delete(UpdatesDir, recursive: true);
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            Log.Write($"updates folder not removed: {e.Message}");
        }
    }

    public void Dispose() => _http.Dispose();

    private async Task<byte[]> FetchAsync(string url, CancellationToken ct) =>
        _localSource is not null ? await File.ReadAllBytesAsync(url, ct) : await _http.GetByteArrayAsync(url, ct);

    private async Task<Stream> OpenAsync(string url, CancellationToken ct) =>
        _localSource is not null ? File.OpenRead(url) : await _http.GetStreamAsync(url, ct);
}

using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace WindowFit.Core.Updates;

/// <summary>The latest GitHub release as <c>releases/latest</c> describes it: its version and its files' download URLs
/// by name.</summary>
public sealed record LatestRelease(Version Version, IReadOnlyDictionary<string, string> AssetUrls);

/// <summary>A newer release this system can install: its version, its signed notes, its page and the setup.</summary>
public sealed record UpdateOffer(Version Version, string Notes, string Page, ReleaseAsset Asset, string Url);

/// <summary>The decisions of an update check, free of HTTP: the exe fetches, these read and judge.</summary>
public static partial class ReleaseCheck
{
    public const string LatestUrl = "https://api.github.com/repos/6up3r/windowfit/releases/latest";

    /// <summary>The version of a <c>vX.Y.Z</c> tag; any other tag is an <see cref="InvalidDataException"/>.</summary>
    public static Version ParseTag(string tag)
    {
        var m = TagPattern().Match(tag);
        if (!m.Success)
        {
            throw new InvalidDataException("the tag is not vX.Y.Z");
        }
        return new Version(int.Parse(m.Groups[1].Value), int.Parse(m.Groups[2].Value), int.Parse(m.Groups[3].Value));
    }

    /// <summary>Three parts, missing ones zero: the assembly's 1.0.3.0 equals the tag's 1.0.3.</summary>
    public static Version Normalize(Version v) => new(v.Major, v.Minor, Math.Max(0, v.Build));

    /// <summary>False for a build published without a version (0.0.0): it neither checks nor records what it showed.</summary>
    public static bool IsRelease(Version current) => Normalize(current) > new Version(0, 0, 0);

    /// <summary>The architecture as the setups name it; null for one no setup exists for.</summary>
    public static string? ArchName(Architecture architecture) => architecture switch
    {
        Architecture.X64 => "x64",
        Architecture.X86 => "x86",
        Architecture.Arm64 => "arm64",
        _ => null,
    };

    public static LatestRelease ParseLatest(byte[] json)
    {
        try
        {
            var root = JsonDocument.Parse(json).RootElement;
            var urls = root.GetProperty("assets").EnumerateArray().ToDictionary(
                a => a.GetProperty("name").GetString()!, a => a.GetProperty("browser_download_url").GetString()!);
            return new LatestRelease(ParseTag(root.GetProperty("tag_name").GetString()!), urls);
        }
        catch (Exception e) when (e is JsonException or KeyNotFoundException or InvalidOperationException or ArgumentException)
        {
            throw new InvalidDataException("the latest release is not readable", e);
        }
    }

    /// <summary>The manifest's and the signature's URLs; a release without both is unsigned, an error.</summary>
    public static (string Manifest, string Signature) ManifestUrls(LatestRelease latest) =>
        latest.AssetUrls.TryGetValue(ReleaseManifest.FileName, out var manifest)
        && latest.AssetUrls.TryGetValue(ReleaseManifest.SignatureName, out var signature)
            ? (manifest, signature)
            : throw new InvalidDataException(
                $"release {latest.Version} has no {ReleaseManifest.FileName} and {ReleaseManifest.SignatureName}");

    /// <summary>The release page of <paramref name="version"/>, built here: the page GitHub's answer names is not
    /// signed, and the unelevated windowfit hands it to the shell.</summary>
    public static string Page(Version version) => $"https://github.com/6up3r/windowfit/releases/tag/v{Normalize(version).ToString(3)}";

    /// <summary>The offer when <paramref name="latest"/> is newer than <paramref name="current"/>; null otherwise. A
    /// manifest of another version than the tag, or a release without a setup for <paramref name="arch"/>, is an
    /// error.</summary>
    public static UpdateOffer? Offer(LatestRelease latest, ReleaseManifest manifest, Version current, string arch)
    {
        if (latest.Version <= Normalize(current))
        {
            return null;
        }
        if (ParseTag("v" + manifest.Version) != latest.Version)
        {
            throw new InvalidDataException($"release {latest.Version} carries the manifest of {manifest.Version}");
        }
        var asset = manifest.AssetFor(arch);
        return asset is not null && latest.AssetUrls.TryGetValue(asset.Name, out var url)
            ? new UpdateOffer(latest.Version, manifest.Notes, Page(latest.Version), asset, url)
            : throw new InvalidDataException($"release {latest.Version} has no setup for {arch}");
    }

    /// <summary>Throws an <see cref="InvalidDataException"/> unless <paramref name="file"/> has the size and SHA-256
    /// the signed manifest gives.</summary>
    public static void CheckDownload(Stream file, ReleaseAsset asset)
    {
        if (file.Length != asset.Size)
        {
            throw new InvalidDataException($"the download is {file.Length} bytes, the release says {asset.Size}");
        }
        file.Position = 0;
        if (!Convert.ToHexStringLower(SHA256.HashData(file)).Equals(asset.Sha256, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidDataException("the download does not match the signed release");
        }
    }

    /// <summary>Why a check or an install failed, in words this code chose: what a server sent never reaches a
    /// window of the elevated process.</summary>
    public static string Reason(Exception e) => e switch
    {
        HttpRequestException { StatusCode: { } code } => $"GitHub answered {(int)code}.",
        HttpRequestException => "No connection to GitHub.",
        OperationCanceledException => "No answer in time.",
        InvalidDataException or IOException or UnauthorizedAccessException => e.Message,
        _ => e.GetType().Name,
    };

    // The names a setup needs, as windowfit-launcher.exe keeps them; profilers, startup hooks, module paths, the command
    // interpreter and the like stay behind.
    private static readonly string[] SetupNames =
    [
        "SystemDrive", "OS", "PROCESSOR_ARCHITECTURE", "PROCESSOR_IDENTIFIER",
        "PROCESSOR_LEVEL", "PROCESSOR_REVISION", "NUMBER_OF_PROCESSORS", "ProgramFiles", "ProgramFiles(x86)", "ProgramW6432",
        "CommonProgramFiles", "CommonProgramFiles(x86)", "CommonProgramW6432", "ProgramData", "ALLUSERSPROFILE", "PUBLIC",
        "COMPUTERNAME", "USERNAME", "USERDOMAIN", "USERPROFILE", "APPDATA", "LOCALAPPDATA", "HOMEDRIVE", "HOMEPATH",
    ];

    /// <summary>The environment an update's setup starts with: the system names of <paramref name="inherited"/>;
    /// SystemRoot, windir and the Path windowfit-launcher.exe builds, from <paramref name="windows"/>; and TMP and TEMP
    /// at <paramref name="temp"/>. The rest of the user's environment, writable without elevation, and the machine's
    /// Path, which installers extend with folders users can write, stay out of the elevated setup and the PowerShell
    /// it runs.</summary>
    public static Dictionary<string, string> SetupEnvironment(IReadOnlyDictionary<string, string?> inherited, string temp, string windows)
    {
        var env = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var name in SetupNames)
        {
            if (inherited.TryGetValue(name, out var value) && value is not null)
            {
                env[name] = value;
            }
        }
        env["SystemRoot"] = windows;
        env["windir"] = windows;
        env["Path"] = $@"{windows}\System32;{windows};{windows}\System32\Wbem;{windows}\System32\WindowsPowerShell\v1.0\";
        env["TMP"] = temp;
        env["TEMP"] = temp;
        return env;
    }

    [GeneratedRegex(@"^v(\d{1,9})\.(\d{1,9})\.(\d{1,9})$")]
    private static partial Regex TagPattern();
}

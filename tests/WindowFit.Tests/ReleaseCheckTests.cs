using System.Runtime.InteropServices;
using System.Text;
using WindowFit.Core.Updates;

namespace WindowFit.Tests;

public class ReleaseCheckTests
{
    private const string Latest = """
        {
          "tag_name": "v1.0.4",
          "html_url": "https://github.com/6up3r/windowfit/releases/tag/v1.0.4",
          "draft": false,
          "assets": [
            { "name": "windowfit-setup-x64.exe", "browser_download_url": "https://example.test/x64.exe", "size": 3 },
            { "name": "release.json", "browser_download_url": "https://example.test/release.json" },
            { "name": "release.json.sig", "browser_download_url": "https://example.test/release.json.sig" }
          ]
        }
        """;

    private static readonly ReleaseAsset X64 = ReleaseAsset.Of("x64", "windowfit-setup-x64.exe", new MemoryStream("abc"u8.ToArray()));

    private static LatestRelease Parse(string json = Latest) => ReleaseCheck.ParseLatest(Encoding.UTF8.GetBytes(json));

    [Fact]
    public void ParseTag_V103_Is103() => Assert.Equal(new Version(1, 0, 3), ReleaseCheck.ParseTag("v1.0.3"));

    [Theory]
    [InlineData("v1.0.4-rc1")]
    [InlineData("nightly")]
    [InlineData("1.0.3")]
    [InlineData("v1.0")]
    public void ParseTag_NotAVersion_Throws(string tag) => Assert.Throws<InvalidDataException>(() => ReleaseCheck.ParseTag(tag));

    [Fact]
    public void IsRelease_Zero_IsFalse()
    {
        Assert.False(ReleaseCheck.IsRelease(new Version(0, 0, 0, 0)));
        Assert.True(ReleaseCheck.IsRelease(new Version(1, 0, 3, 0)));
    }

    [Fact]
    public void ArchName_MapsTheSetupArchitectures()
    {
        Assert.Equal("x64", ReleaseCheck.ArchName(Architecture.X64));
        Assert.Equal("x86", ReleaseCheck.ArchName(Architecture.X86));
        Assert.Equal("arm64", ReleaseCheck.ArchName(Architecture.Arm64));
        Assert.Null(ReleaseCheck.ArchName(Architecture.Arm));
    }

    [Fact]
    public void ParseLatest_ReadsTagPageAndAssets()
    {
        var latest = Parse();
        Assert.Equal(new Version(1, 0, 4), latest.Version);
        Assert.Equal("https://github.com/6up3r/windowfit/releases/tag/v1.0.4", latest.Page);
        Assert.Equal("https://example.test/x64.exe", latest.AssetUrls["windowfit-setup-x64.exe"]);
    }

    [Theory]
    [InlineData("{}")]
    [InlineData("[]")]
    [InlineData("\"x\"")]
    [InlineData("not json")]
    [InlineData("""{"tag_name":"v1.0.4","html_url":"p","assets":[{"name":"a"}]}""")]
    public void ParseLatest_NotARelease_Throws(string json) => Assert.Throws<InvalidDataException>(() => Parse(json));

    [Fact]
    public void ManifestUrls_ReturnsBoth() =>
        Assert.Equal(("https://example.test/release.json", "https://example.test/release.json.sig"), ReleaseCheck.ManifestUrls(Parse()));

    [Fact]
    public void ManifestUrls_Missing_Throws()
    {
        var latest = Parse() with { AssetUrls = new Dictionary<string, string> { ["release.json"] = "u" } };
        Assert.Throws<InvalidDataException>(() => ReleaseCheck.ManifestUrls(latest));
    }

    [Fact]
    public void Offer_Newer_ReturnsArchAsset()
    {
        var offer = ReleaseCheck.Offer(Parse(), new ReleaseManifest("1.0.4", "notes", [X64]), new Version(1, 0, 3, 0), "x64");
        Assert.NotNull(offer);
        Assert.Equal(new Version(1, 0, 4), offer.Version);
        Assert.Equal("notes", offer.Notes);
        Assert.Equal(X64, offer.Asset);
        Assert.Equal("https://example.test/x64.exe", offer.Url);
        Assert.Equal("https://github.com/6up3r/windowfit/releases/tag/v1.0.4", offer.Page);
    }

    [Theory]
    [InlineData(1, 0, 4)]
    [InlineData(1, 0, 5)]
    public void Offer_SameOrOlder_IsNull(int major, int minor, int build) =>
        Assert.Null(ReleaseCheck.Offer(Parse(), new ReleaseManifest("1.0.4", "", [X64]), new Version(major, minor, build, 0), "x64"));

    [Fact]
    public void Offer_NoAssetForArch_IsNull() =>
        Assert.Null(ReleaseCheck.Offer(Parse(), new ReleaseManifest("1.0.4", "", [X64]), new Version(1, 0, 3), "arm64"));

    [Fact]
    public void Offer_AssetWithoutUrl_IsNull() =>
        Assert.Null(ReleaseCheck.Offer(Parse(), new ReleaseManifest("1.0.4", "", [X64 with { Name = "other.exe" }]), new Version(1, 0, 3), "x64"));

    [Fact]
    public void Offer_ManifestOfAnotherVersion_Throws() =>
        Assert.Throws<InvalidDataException>(() => ReleaseCheck.Offer(Parse(), new ReleaseManifest("1.0.3", "", [X64]), new Version(1, 0, 2), "x64"));

    [Fact]
    public void CheckDownload_WrongSize_Throws() =>
        Assert.Throws<InvalidDataException>(() => ReleaseCheck.CheckDownload(new MemoryStream("abcd"u8.ToArray()), X64));

    [Fact]
    public void CheckDownload_WrongHash_Throws() =>
        Assert.Throws<InvalidDataException>(() => ReleaseCheck.CheckDownload(new MemoryStream("abd"u8.ToArray()), X64));

    [Fact]
    public void CheckDownload_Match_Passes() => ReleaseCheck.CheckDownload(new MemoryStream("abc"u8.ToArray()), X64);

    [Fact]
    public void ParseTag_Message_OmitsTheTag() =>
        Assert.DoesNotContain("planted", Assert.Throws<InvalidDataException>(() => ReleaseCheck.ParseTag("planted text")).Message);

    [Fact]
    public void ParseLatest_Message_OmitsTheAnswersText()
    {
        var duplicate = """{"tag_name":"v1.0.4","html_url":"p","assets":[{"name":"planted","browser_download_url":"u"},{"name":"planted","browser_download_url":"u"}]}""";
        Assert.DoesNotContain("planted", Assert.Throws<InvalidDataException>(() => Parse(duplicate)).Message);
        Assert.DoesNotContain("planted", Assert.Throws<InvalidDataException>(() => Parse("""{"tag_name":"planted","html_url":"p","assets":[]}""")).Message);
    }

    [Fact]
    public void Reason_HttpStatus_NamesTheCodeOnly() =>
        Assert.Equal("GitHub answered 403.",
            ReleaseCheck.Reason(new HttpRequestException("planted reason phrase", null, System.Net.HttpStatusCode.Forbidden)));

    [Fact]
    public void Reason_NoStatus_IsNoConnection() =>
        Assert.Equal("No connection to GitHub.", ReleaseCheck.Reason(new HttpRequestException("planted")));

    [Fact]
    public void Reason_Timeout_IsNoAnswer() =>
        Assert.Equal("No answer in time.", ReleaseCheck.Reason(new TaskCanceledException("planted")));

    [Fact]
    public void Reason_OwnChecks_KeepTheirMessage() =>
        Assert.Equal("the download does not match the signed release",
            ReleaseCheck.Reason(new InvalidDataException("the download does not match the signed release")));

    [Fact]
    public void Reason_Other_IsTheTypeName() =>
        Assert.Equal("FormatException", ReleaseCheck.Reason(new FormatException("planted")));

    [Fact]
    public void SetupEnvironment_KeepsOnlyTheSystemNames()
    {
        var inherited = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase)
        {
            ["SystemRoot"] = @"C:\Windows",
            ["USERPROFILE"] = @"C:\Users\u",
            ["Path"] = @"C:\Users\u\bin;C:\Windows\system32",
            ["TEMP"] = @"C:\Users\u\AppData\Local\Temp",
            ["COR_ENABLE_PROFILING"] = "1",
            ["COR_PROFILER_PATH"] = @"C:\Users\u\p.dll",
            ["CORECLR_ENABLE_PROFILING"] = "1",
            ["DOTNET_STARTUP_HOOKS"] = @"C:\Users\u\h.dll",
            ["PSModulePath"] = @"C:\Users\u\modules",
            ["__PSLockdownPolicy"] = "0",
            ["ComSpec"] = @"C:\Users\u\cmd.exe",
            ["PATHEXT"] = ".PLANTED",
        };
        var env = ReleaseCheck.SetupEnvironment(inherited, @"C:\Program Files\windowfit\updates\temp", @"C:\Windows\system32;C:\Windows");
        Assert.Equal(@"C:\Windows", env["SystemRoot"]);
        Assert.Equal(@"C:\Users\u", env["USERPROFILE"]);
        Assert.Equal(@"C:\Windows\system32;C:\Windows", env["Path"]);
        Assert.Equal(@"C:\Program Files\windowfit\updates\temp", env["TEMP"]);
        Assert.Equal(@"C:\Program Files\windowfit\updates\temp", env["TMP"]);
        Assert.All(new[] { "COR_ENABLE_PROFILING", "COR_PROFILER_PATH", "CORECLR_ENABLE_PROFILING", "DOTNET_STARTUP_HOOKS", "PSModulePath", "__PSLockdownPolicy", "ComSpec", "PATHEXT" },
            name => Assert.False(env.ContainsKey(name), name));
    }
}

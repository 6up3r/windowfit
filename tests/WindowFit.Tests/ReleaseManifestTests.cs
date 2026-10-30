using System.Security.Cryptography;
using System.Text;
using WindowFit.Core.Updates;

namespace WindowFit.Tests;

public class ReleaseManifestTests
{
    private static readonly ReleaseManifest Manifest = new("1.0.4", "### Fixed\n\n- a fix",
    [
        new ReleaseAsset("x64", "windowfit-setup-x64.exe", new string('a', 64), 100),
        new ReleaseAsset("arm64", "windowfit-setup-arm64.exe", new string('b', 64), 200),
    ]);

    private static (ECDsa Key, string Public) NewKey()
    {
        var key = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        return (key, Convert.ToBase64String(key.ExportSubjectPublicKeyInfo()));
    }

    [Fact]
    public void ToJson_Verify_RoundTrips()
    {
        var (key, pub) = NewKey();
        var json = Manifest.ToJson();
        var read = ReleaseManifest.Verify(json, ReleaseManifest.Sign(json, key), [pub]);
        Assert.Equal(Manifest.Version, read.Version);
        Assert.Equal(Manifest.Notes, read.Notes);
        Assert.Equal(Manifest.Assets, read.Assets);
    }

    [Fact]
    public void Verify_TamperedJson_Throws()
    {
        var (key, pub) = NewKey();
        var json = Manifest.ToJson();
        var signature = ReleaseManifest.Sign(json, key);
        var tampered = Encoding.UTF8.GetBytes(Encoding.UTF8.GetString(json).Replace("1.0.4", "1.0.5"));
        Assert.Throws<InvalidDataException>(() => ReleaseManifest.Verify(tampered, signature, [pub]));
    }

    [Fact]
    public void Verify_OtherKey_Throws()
    {
        var (key, _) = NewKey();
        var (_, other) = NewKey();
        var json = Manifest.ToJson();
        Assert.Throws<InvalidDataException>(() => ReleaseManifest.Verify(json, ReleaseManifest.Sign(json, key), [other]));
    }

    [Fact]
    public void Verify_NoKeys_Throws()
    {
        var (key, _) = NewKey();
        var json = Manifest.ToJson();
        Assert.Throws<InvalidDataException>(() => ReleaseManifest.Verify(json, ReleaseManifest.Sign(json, key), []));
    }

    [Fact]
    public void Verify_SecondKeyOfList_Accepts()
    {
        var (key, pub) = NewKey();
        var (_, other) = NewKey();
        var json = Manifest.ToJson();
        Assert.Equal("1.0.4", ReleaseManifest.Verify(json, ReleaseManifest.Sign(json, key), [other, pub]).Version);
    }

    [Fact]
    public void Verify_NotJson_ThrowsInvalidData()
    {
        var (key, pub) = NewKey();
        foreach (var text in new[] { "not json", "{}", "[]", """{"version":"1.0.4","notes":"","assets":[{"arch":"x64"}]}""" })
        {
            var json = Encoding.UTF8.GetBytes(text);
            Assert.Throws<InvalidDataException>(() => ReleaseManifest.Verify(json, ReleaseManifest.Sign(json, key), [pub]));
        }
    }

    [Fact]
    public void ReleaseKey_EveryKeyIsP256()
    {
        Assert.NotEmpty(ReleaseKey.PublicKeys);
        Assert.All(ReleaseKey.PublicKeys, k =>
        {
            using var key = ECDsa.Create();
            key.ImportSubjectPublicKeyInfo(Convert.FromBase64String(k), out _);
            Assert.Equal("1.2.840.10045.3.1.7", key.ExportParameters(false).Curve.Oid.Value);
        });
    }

    [Fact]
    public void AssetFor_PicksArch()
    {
        Assert.Equal("windowfit-setup-arm64.exe", Manifest.AssetFor("arm64")?.Name);
        Assert.Null(Manifest.AssetFor("x86"));
    }

    [Fact]
    public void Of_HashesTheContent()
    {
        var asset = ReleaseAsset.Of("x64", "windowfit-setup-x64.exe", new MemoryStream("abc"u8.ToArray()));
        Assert.Equal("ba7816bf8f01cfea414140de5dae2223b00361a396177a9cb410ff61f20015ad", asset.Sha256);
        Assert.Equal(3, asset.Size);
    }
}

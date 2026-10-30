using System.Security.Cryptography;
using System.Text.Json;

namespace WindowFit.Core.Updates;

/// <summary>One setup of a release: the architecture it installs on, its file name among the release's files, its
/// SHA-256 (lowercase hex) and size.</summary>
public sealed record ReleaseAsset(string Arch, string Name, string Sha256, long Size)
{
    public static ReleaseAsset Of(string arch, string name, Stream content)
    {
        using var sha = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        var buffer = new byte[1 << 16];
        long size = 0;
        int n;
        while ((n = content.Read(buffer)) > 0)
        {
            sha.AppendData(buffer, 0, n);
            size += n;
        }
        return new ReleaseAsset(arch, name, Convert.ToHexStringLower(sha.GetHashAndReset()), size);
    }
}

/// <summary>
/// What a release carries, signed with windowfit's release key: its version, its notes (the changelog's section) and
/// its setups. The signature is ECDSA P-256 over the manifest's exact bytes, SHA-256, DER.
/// </summary>
public sealed record ReleaseManifest(string Version, string Notes, IReadOnlyList<ReleaseAsset> Assets)
{
    public const string FileName = "release.json";
    public const string SignatureName = "release.json.sig";

    public byte[] ToJson()
    {
        using var ms = new MemoryStream();
        using (var w = new Utf8JsonWriter(ms, new JsonWriterOptions { Indented = true }))
        {
            w.WriteStartObject();
            w.WriteString("version", Version);
            w.WriteString("notes", Notes);
            w.WriteStartArray("assets");
            foreach (var a in Assets)
            {
                w.WriteStartObject();
                w.WriteString("arch", a.Arch);
                w.WriteString("name", a.Name);
                w.WriteString("sha256", a.Sha256);
                w.WriteNumber("size", a.Size);
                w.WriteEndObject();
            }
            w.WriteEndArray();
            w.WriteEndObject();
        }
        return ms.ToArray();
    }

    public static byte[] Sign(byte[] json, ECDsa key) =>
        key.SignData(json, HashAlgorithmName.SHA256, DSASignatureFormat.Rfc3279DerSequence);

    /// <summary>The manifest, when one of <paramref name="publicKeys"/> (SubjectPublicKeyInfo, DER, base64) signed
    /// exactly these bytes; else an <see cref="InvalidDataException"/>.</summary>
    public static ReleaseManifest Verify(byte[] json, byte[] signature, IReadOnlyList<string> publicKeys)
    {
        if (!publicKeys.Any(k => Verifies(json, signature, k)))
        {
            throw new InvalidDataException("the release is not signed by windowfit's key");
        }
        try
        {
            var root = JsonDocument.Parse(json).RootElement;
            return new ReleaseManifest(root.GetProperty("version").GetString()!, root.GetProperty("notes").GetString()!,
                root.GetProperty("assets").EnumerateArray().Select(a => new ReleaseAsset(a.GetProperty("arch").GetString()!,
                    a.GetProperty("name").GetString()!, a.GetProperty("sha256").GetString()!, a.GetProperty("size").GetInt64())).ToList());
        }
        catch (Exception e) when (e is JsonException or KeyNotFoundException or InvalidOperationException or FormatException)
        {
            throw new InvalidDataException($"the release's {FileName} is not a manifest: {e.Message}", e);
        }
    }

    public ReleaseAsset? AssetFor(string arch) => Assets.FirstOrDefault(a => a.Arch == arch);

    private static bool Verifies(byte[] json, byte[] signature, string publicKey)
    {
        using var key = ECDsa.Create();
        key.ImportSubjectPublicKeyInfo(Convert.FromBase64String(publicKey), out _);
        return key.VerifyData(json, signature, HashAlgorithmName.SHA256, DSASignatureFormat.Rfc3279DerSequence);
    }
}

using System.Security.Cryptography;
using System.Text;
using WindowFit.Core.Updates;

// The release author's tool, not shipped with the app: it makes windowfit's release key once, writes a version's
// release notes from CHANGELOG.md, and signs a release's manifest after the CI has built the setups.
//   windowfit-signer keygen [--key PATH]
//   windowfit-signer notes --version X.Y.Z --out FILE [--changelog FILE]
//   windowfit-signer sign --version X.Y.Z --setups DIR [--changelog FILE] [--key PATH]
// The private key stays at PATH, by default ~/.windowfit/release-signing-key.pem, outside every repository, encrypted
// with a passphrase asked on the console; keygen prints only the public key, which goes into ReleaseKey.PublicKeys.
// sign reads windowfit-setup-<arch>.exe and its .sha256 for each architecture in DIR (as gh release download leaves
// them) and writes release.json and release.json.sig beside them.

string[] arches = ["x64", "x86", "arm64"];

string Arg(string name, string? fallback = null)
{
    int i = Array.IndexOf(args, name);
    return i >= 0 && i + 1 < args.Length ? args[i + 1] : fallback ?? throw new ArgumentException($"missing {name}");
}

var keyPath = Arg("--key", Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".windowfit", "release-signing-key.pem"));
var changelog = Arg("--changelog", "CHANGELOG.md");
try
{
    switch (args.FirstOrDefault())
    {
        case "keygen":
            return Keygen(keyPath);
        case "notes":
            return Notes(Arg("--version"), changelog, Arg("--out"));
        case "sign":
            return Sign(Arg("--version"), Arg("--setups"), changelog, keyPath, arches);
        default:
            Console.Error.WriteLine("usage: keygen [--key PATH] | notes --version X.Y.Z --out FILE [--changelog FILE]"
                                    + " | sign --version X.Y.Z --setups DIR [--changelog FILE] [--key PATH]");
            return 2;
    }
}
catch (Exception e) when (e is InvalidDataException or ArgumentException or IOException or CryptographicException)
{
    Console.Error.WriteLine(e.Message);
    return 1;
}

static int Keygen(string keyPath)
{
    if (File.Exists(keyPath))
    {
        Console.Error.WriteLine($"a key already exists at {keyPath}: it is never overwritten");
        return 1;
    }
    var passphrase = Passphrase("new passphrase (12 characters or more): ");
    if (passphrase.Length < 12)
    {
        Console.Error.WriteLine("the passphrase is shorter than 12 characters");
        return 1;
    }
    if (Passphrase("the same passphrase again: ") != passphrase)
    {
        Console.Error.WriteLine("the two passphrases differ");
        return 1;
    }
    Directory.CreateDirectory(Path.GetDirectoryName(keyPath)!);
    using var key = ECDsa.Create(ECCurve.NamedCurves.nistP256);
    var pbe = new PbeParameters(PbeEncryptionAlgorithm.Aes256Cbc, HashAlgorithmName.SHA256, 600_000);
    File.WriteAllText(keyPath, key.ExportEncryptedPkcs8PrivateKeyPem(passphrase, pbe));
    Console.WriteLine($"private key written to {keyPath}, encrypted (keep it and a backup out of every repository)");
    Console.WriteLine("public key: " + Convert.ToBase64String(key.ExportSubjectPublicKeyInfo()));
    return 0;
}

static int Notes(string version, string changelog, string outFile)
{
    var notes = Changelog.Section(File.ReadAllText(changelog), ReleaseCheck.ParseTag("v" + version));
    File.WriteAllText(outFile, notes + "\n", new UTF8Encoding(false));
    Console.WriteLine($"notes of {version} written to {outFile}");
    return 0;
}

static int Sign(string version, string setups, string changelog, string keyPath, string[] arches)
{
    var notes = Changelog.Section(File.ReadAllText(changelog), ReleaseCheck.ParseTag("v" + version));
    var assets = new List<ReleaseAsset>();
    foreach (var arch in arches)
    {
        var name = $"windowfit-setup-{arch}.exe";
        ReleaseAsset asset;
        using (var file = File.OpenRead(Path.Combine(setups, name)))
        {
            asset = ReleaseAsset.Of(arch, name, file);
        }
        // The CI's hash, "<hash>  <name>": the file signed is the file the CI built.
        var ci = File.ReadAllText(Path.Combine(setups, name + ".sha256")).Trim().Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (ci.Length != 2 || ci[1] != name || !ci[0].Equals(asset.Sha256, StringComparison.OrdinalIgnoreCase))
        {
            Console.Error.WriteLine($"{name} does not match {name}.sha256");
            return 1;
        }
        assets.Add(asset);
    }
    var manifest = new ReleaseManifest(version, notes, assets).ToJson();
    using var key = ECDsa.Create();
    key.ImportFromEncryptedPem(File.ReadAllText(keyPath), Passphrase("passphrase: "));
    var publicKey = Convert.ToBase64String(key.ExportSubjectPublicKeyInfo());
    // A manifest no installed windowfit accepts would leave that release unoffered: nothing is written.
    if (!ReleaseKey.PublicKeys.Contains(publicKey))
    {
        Console.Error.WriteLine("the key is not in ReleaseKey.PublicKeys: installed windowfit would reject this release");
        return 1;
    }
    var manifestPath = Path.Combine(setups, ReleaseManifest.FileName);
    var signaturePath = Path.Combine(setups, ReleaseManifest.SignatureName);
    File.WriteAllBytes(manifestPath, manifest);
    File.WriteAllBytes(signaturePath, ReleaseManifest.Sign(manifest, key));
    ReleaseManifest.Verify(File.ReadAllBytes(manifestPath), File.ReadAllBytes(signaturePath), [publicKey]);
    Console.WriteLine($"release {version}: {string.Join(", ", assets.Select(a => a.Name))}, "
                      + $"{ReleaseManifest.FileName} and {ReleaseManifest.SignatureName} in {setups}");
    return 0;
}

// Read from the console without echo, never from arguments, the environment or a file.
static string Passphrase(string prompt)
{
    if (Console.IsInputRedirected)
    {
        Console.Error.WriteLine("run this in a console: it asks the passphrase");
        Environment.Exit(2);
    }
    Console.Write(prompt);
    var text = new StringBuilder();
    for (var k = Console.ReadKey(intercept: true); k.Key != ConsoleKey.Enter; k = Console.ReadKey(intercept: true))
    {
        if (k.Key == ConsoleKey.Backspace)
        {
            if (text.Length > 0)
            {
                text.Length--;
            }
        }
        else if (!char.IsControl(k.KeyChar))
        {
            text.Append(k.KeyChar);
        }
    }
    Console.WriteLine();
    return text.ToString();
}

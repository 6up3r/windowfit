namespace WindowFit.Core.Updates;

/// <summary>The public halves of windowfit's release keys (SubjectPublicKeyInfo, DER, base64): a manifest signed by
/// any of them is trusted, so a new key can be added before an old one is retired.</summary>
public static class ReleaseKey
{
    public static readonly IReadOnlyList<string> PublicKeys =
    [
        "MFkwEwYHKoZIzj0CAQYIKoZIzj0DAQcDQgAE7UE+r2SfgwHjHztzIgxVlhHv2D+N47AG1RkVNRs6pYWsHztTe+Tt7E2GnKjssOD+hsyG262rea8LmefOM4yC3w==",
    ];
}

namespace WindowFit.Core;

/// <summary>
/// The License window's contents: windowfit's own LICENSE first, then THIRD-PARTY-NOTICES.txt in its sections
/// (the overview, then each work under a rule, "n. Title", a rule).
/// </summary>
public static class LicenseNotices
{
    private const string Rule = "--------------------------------------------------------------------------------";

    public static IReadOnlyList<(string Title, string Text)> Sections(string license, string notices)
    {
        license = license.Replace("\r\n", "\n");
        notices = notices.Replace("\r\n", "\n");
        var sections = new List<(string, string)> { ("windowfit", license.Trim('\n') + "\n") };
        // The headings are looked for in their order, so a numbered clause inside a licence's own text is never taken for one.
        var heads = new List<(int At, int Body, string Title)>();
        for (int n = 1, from = 0; ; n++)
        {
            var at = notices.IndexOf($"\n{Rule}\n{n}. ", from, StringComparison.Ordinal);
            if (at < 0)
            {
                break;
            }
            var titleAt = at + Rule.Length + 2 + $"{n}. ".Length;
            var titleEnd = notices.IndexOf('\n', titleAt);
            if (titleEnd < 0 || string.CompareOrdinal(notices, titleEnd + 1, Rule, 0, Rule.Length) != 0)
            {
                break;
            }
            heads.Add((at, Math.Min(titleEnd + Rule.Length + 2, notices.Length), notices[titleAt..titleEnd]));
            from = titleEnd;
        }
        sections.Add(("Third-party notices", notices[..(heads.Count > 0 ? heads[0].At : notices.Length)].Trim('\n') + "\n"));
        for (var i = 0; i < heads.Count; i++)
        {
            var end = i + 1 < heads.Count ? heads[i + 1].At : notices.Length;
            sections.Add((heads[i].Title, notices[heads[i].Body..end].Trim('\n') + "\n"));
        }
        return sections;
    }

    /// <summary>The license's "Copyright ..." line, so the window names the holder the license file names.</summary>
    public static string Copyright(string license) =>
        license.Replace("\r\n", "\n").Split('\n').FirstOrDefault(l => l.StartsWith("Copyright", StringComparison.Ordinal))?.Trim()
        ?? throw new InvalidOperationException("LICENSE has no Copyright line");
}

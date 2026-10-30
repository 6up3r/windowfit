using System.Text.RegularExpressions;

namespace WindowFit.Core.Updates;

/// <summary>One released version of the changelog: its heading without <c>## </c> and its body in Markdown.</summary>
public sealed record ChangelogEntry(Version Version, string Title, string Body);

/// <summary>
/// CHANGELOG.md (Keep a Changelog: <c>## [X.Y.Z] - date</c> sections, newest first, link references at the end),
/// embedded in the exe for "What's new" and cut into each release's notes.
/// </summary>
public static partial class Changelog
{
    public static IReadOnlyList<ChangelogEntry> Parse(string markdown)
    {
        var entries = new List<ChangelogEntry>();
        (Version Version, string Title, List<string> Lines)? open = null;
        void Close()
        {
            if (open is { } e)
            {
                entries.Add(new ChangelogEntry(e.Version, e.Title, string.Join('\n', e.Lines).Trim()));
            }
            open = null;
        }
        foreach (var line in markdown.Replace("\r\n", "\n").Split('\n'))
        {
            if (line.StartsWith("## ", StringComparison.Ordinal))
            {
                Close();
                // Any other second-level heading, [Unreleased] among them, is no released version.
                if (Heading().Match(line) is { Success: true } m)
                {
                    open = (Version.Parse(m.Groups[1].Value), line[3..].Trim(), []);
                }
            }
            else if (!LinkReference().IsMatch(line))
            {
                open?.Lines.Add(line);
            }
        }
        Close();
        return entries;
    }

    /// <summary>The body of <paramref name="version"/>'s section; an <see cref="InvalidDataException"/> when the
    /// changelog has none.</summary>
    public static string Section(string markdown, Version version) =>
        Parse(markdown).FirstOrDefault(e => e.Version == ReleaseCheck.Normalize(version))?.Body
        ?? throw new InvalidDataException($"CHANGELOG.md has no section for {ReleaseCheck.Normalize(version)}");

    /// <summary>What a user who last saw <paramref name="seen"/> has not read, newest first; nothing for a first run
    /// (no <paramref name="seen"/>) or a downgrade.</summary>
    public static IReadOnlyList<ChangelogEntry> Since(IEnumerable<ChangelogEntry> entries, Version? seen, Version current)
    {
        if (seen is null)
        {
            return [];
        }
        var from = ReleaseCheck.Normalize(seen);
        var to = ReleaseCheck.Normalize(current);
        return [.. entries.Where(e => e.Version > from && e.Version <= to).OrderByDescending(e => e.Version)];
    }

    public static string Text(IEnumerable<ChangelogEntry> entries) =>
        string.Join("\n\n", entries.Select(e => $"{e.Title}\n\n{e.Body}"));

    [GeneratedRegex(@"^## \[(\d+\.\d+\.\d+)\]")]
    private static partial Regex Heading();

    [GeneratedRegex(@"^\[[^\]]+\]: \S+\s*$")]
    private static partial Regex LinkReference();
}

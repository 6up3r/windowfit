using WindowFit.Core.Updates;

namespace WindowFit.Tests;

public class ChangelogTests
{
    private const string Markdown = """
        # Changelog

        All notable changes are listed here.

        ## [Unreleased]

        - not yet

        ## [1.0.4] - 2026-11-20

        ### Fixed

        - a fix

        ## [1.0.3] - 2026-10-30

        ### Added

        - groups

        ## [1.0.0] - 2026-10-23

        ### Added

        - tiling

        [1.0.4]: https://github.com/6up3r/windowfit/releases/tag/v1.0.4
        [1.0.3]: https://github.com/6up3r/windowfit/releases/tag/v1.0.3
        [1.0.0]: https://github.com/6up3r/windowfit/releases/tag/v1.0.0
        """;

    [Fact]
    public void Parse_ReadsVersionsTitlesBodies()
    {
        var entries = Changelog.Parse(Markdown);
        Assert.Equal([new Version(1, 0, 4), new Version(1, 0, 3), new Version(1, 0, 0)], entries.Select(e => e.Version));
        Assert.Equal("[1.0.3] - 2026-10-30", entries[1].Title);
        Assert.Equal("### Added\n\n- groups", entries[1].Body);
    }

    [Fact]
    public void Parse_SkipsUnreleasedAndLinkReferences()
    {
        var entries = Changelog.Parse(Markdown);
        Assert.DoesNotContain(entries, e => e.Body.Contains("not yet"));
        Assert.Equal("### Added\n\n- tiling", entries[2].Body);
    }

    [Fact]
    public void Parse_Crlf_SameAsLf() =>
        Assert.Equal(Changelog.Parse(Markdown.Replace("\r\n", "\n")), Changelog.Parse(Markdown.Replace("\r\n", "\n").Replace("\n", "\r\n")));

    [Fact]
    public void Section_IsTheVersionsBody() => Assert.Equal("### Fixed\n\n- a fix", Changelog.Section(Markdown, new Version(1, 0, 4)));

    [Fact]
    public void Section_Missing_Throws() => Assert.Throws<InvalidDataException>(() => Changelog.Section(Markdown, new Version(1, 0, 1)));

    [Fact]
    public void Since_NullSeen_IsEmpty() => Assert.Empty(Changelog.Since(Changelog.Parse(Markdown), null, new Version(1, 0, 3)));

    [Fact]
    public void Since_SeenNewerThanCurrent_IsEmpty() =>
        Assert.Empty(Changelog.Since(Changelog.Parse(Markdown), new Version(1, 0, 4), new Version(1, 0, 3)));

    [Fact]
    public void Since_ReturnsBetweenNewestFirst()
    {
        var entries = Changelog.Parse(Markdown);
        Assert.Equal([new Version(1, 0, 3)], Changelog.Since(entries, new Version(1, 0, 0), new Version(1, 0, 3)).Select(e => e.Version));
        Assert.Equal([new Version(1, 0, 4), new Version(1, 0, 3)],
            Changelog.Since(entries, new Version(1, 0, 0), new Version(1, 0, 4, 0)).Select(e => e.Version));
    }

    [Fact]
    public void Text_JoinsTitlesAndBodies() =>
        Assert.Equal("[1.0.4] - 2026-11-20\n\n### Fixed\n\n- a fix\n\n[1.0.3] - 2026-10-30\n\n### Added\n\n- groups",
            Changelog.Text(Changelog.Parse(Markdown).Take(2)));

    [Fact]
    public void RepositoryChangelog_Has103And100()
    {
        var root = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", ".."));
        var markdown = File.ReadAllText(Path.Combine(root, "CHANGELOG.md"));
        Assert.NotEmpty(Changelog.Section(markdown, new Version(1, 0, 3)));
        Assert.NotEmpty(Changelog.Section(markdown, new Version(1, 0, 0)));
    }
}

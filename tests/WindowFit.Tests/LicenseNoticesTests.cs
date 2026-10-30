using WindowFit.Core;

namespace WindowFit.Tests;

public class LicenseNoticesTests
{
    private const string Rule = "--------------------------------------------------------------------------------";

    private static readonly string Notices =
        "Overview\r\n\r\n  1. A\r\n  2. B\r\n\r\n" +
        $"{Rule}\r\n1. A - MIT\r\n{Rule}\r\n\r\nA text\r\n1. a numbered clause\r\n\r\n" +
        $"{Rule}\r\n2. B - BSD\r\n{Rule}\r\n\r\nB text\r\n";

    [Fact]
    public void Sections_LicenseFirstThenOverviewThenEachWork()
    {
        var sections = LicenseNotices.Sections("MIT License\r\n\r\nCopyright (c) 2026 6Up3r\r\n", Notices);
        Assert.Equal(["windowfit", "Third-party notices", "A - MIT", "B - BSD"], sections.Select(s => s.Title));
        Assert.Equal("MIT License\n\nCopyright (c) 2026 6Up3r\n", sections[0].Text);
        Assert.Equal("A text\n1. a numbered clause\n", sections[2].Text);
        Assert.Equal("B text\n", sections[3].Text);
    }

    [Fact]
    public void Sections_FileEndingOnARule_HasAnEmptyLastWork()
    {
        var sections = LicenseNotices.Sections("L", $"Overview\n{Rule}\n1. A - MIT\n{Rule}");
        Assert.Equal(["windowfit", "Third-party notices", "A - MIT"], sections.Select(s => s.Title));
        Assert.Equal("\n", sections[2].Text);
    }

    [Fact]
    public void Sections_NoticesWithoutWorks_IsOneOverview() =>
        Assert.Equal(2, LicenseNotices.Sections("L", "only an overview").Count);

    [Fact]
    public void Copyright_IsTheLicensesCopyrightLine() =>
        Assert.Equal("Copyright (c) 2026 6Up3r", LicenseNotices.Copyright("MIT License\r\n\r\nCopyright (c) 2026 6Up3r\r\n\r\nPermission"));

    [Fact]
    public void RepositoryFiles_SplitIntoEveryListedWork()
    {
        var root = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", ".."));
        var notices = File.ReadAllText(Path.Combine(root, "THIRD-PARTY-NOTICES.txt"));
        var license = File.ReadAllText(Path.Combine(root, "LICENSE"));
        var overview = notices.Replace("\r\n", "\n").Split("\n" + Rule + "\n")[0];
        var listed = overview.Split('\n').Count(l => System.Text.RegularExpressions.Regex.IsMatch(l, @"^  \d+\. "));
        Assert.Equal(3, listed);
        Assert.Equal(2 + listed, LicenseNotices.Sections(license, notices).Count);
        Assert.Equal("Copyright (c) 2026 6Up3r", LicenseNotices.Copyright(license));
    }
}

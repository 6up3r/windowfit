using WindowFit.Core;

namespace WindowFit.Tests;

public class TrayTextTests
{
    [Theory]
    [InlineData("it-IT", "Esci")]
    [InlineData("it", "Esci")]
    [InlineData("en-US", "Exit")]
    [InlineData("xx-YY", "Exit")]
    [InlineData("", "Exit")]
    [InlineData("zh-TW", "Exit")]
    public void For_PicksTheLanguageOrEnglish(string culture, string exit) =>
        Assert.Equal(exit, TrayText.For(culture).Exit);

    [Fact]
    public void For_SimplifiedChinese_IsNotEnglish() =>
        Assert.NotEqual("Exit", TrayText.For("zh-CN").Exit);

    [Fact]
    public void For_EveryListedLanguage_HasItsOwnStrings()
    {
        string[] cultures = ["it", "de", "fr", "es", "pt-BR", "nl", "pl", "ru", "ja", "zh-CN", "ko", "tr"];
        Assert.All(cultures, c => Assert.NotEqual(TrayText.For("en").Exit, TrayText.For(c).Exit));
        Assert.All(cultures, c => Assert.NotEqual(TrayText.For("en").License, TrayText.For(c).License));
        Assert.All(cultures, c => Assert.NotEqual(TrayText.For("en").Groups, TrayText.For(c).Groups));
        Assert.All(cultures, c => Assert.NotEqual(TrayText.For("en").CheckForUpdates, TrayText.For(c).CheckForUpdates));
        Assert.All(cultures, c => Assert.NotEqual(TrayText.For("en").WhatsNew, TrayText.For(c).WhatsNew));
        Assert.All(cultures, c => Assert.NotEqual(TrayText.For("en").Install, TrayText.For(c).Install));
    }

    [Theory]
    [InlineData("en")]
    [InlineData("it")]
    [InlineData("de")]
    [InlineData("fr")]
    [InlineData("es")]
    [InlineData("pt-BR")]
    [InlineData("nl")]
    [InlineData("pl")]
    [InlineData("ru")]
    [InlineData("ja")]
    [InlineData("zh-CN")]
    [InlineData("ko")]
    [InlineData("tr")]
    public void EveryLanguage_KeepsThePlaceholders(string culture)
    {
        var t = TrayText.For(culture);
        Assert.All(new[] { t.InstallUpdate, t.UpdateAvailable, t.CheckFailed, t.InstallFailed },
            s => Assert.Single(System.Text.RegularExpressions.Regex.Matches(s, @"\{0\}")));
    }

    [Fact]
    public void InstallUpdateItem_ShowsThreePartsAndEllipsis()
    {
        var item = TrayText.For("it").InstallUpdateItem(new Version(1, 0, 4, 0));
        Assert.Contains("1.0.4", item);
        Assert.DoesNotContain("1.0.4.0", item);
        Assert.EndsWith("...", item);
    }

    [Fact]
    public void Available_NamesTheVersion() =>
        Assert.Equal("windowfit 1.0.4 is available.", TrayText.For("en").Available(new Version(1, 0, 4, 0)));

    [Fact]
    public void Tooltip_ShowsPausedOnlyWhenDisabled()
    {
        Assert.Equal("windowfit", TrayText.For("it").Tooltip(enabled: true));
        Assert.Equal("windowfit (in pausa)", TrayText.For("it").Tooltip(enabled: false));
    }
}

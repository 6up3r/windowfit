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
    }

    [Fact]
    public void Tooltip_ShowsPausedOnlyWhenDisabled()
    {
        Assert.Equal("windowfit", TrayText.For("it").Tooltip(enabled: true));
        Assert.Equal("windowfit (in pausa)", TrayText.For("it").Tooltip(enabled: false));
    }
}

namespace WindowFit.Core;

/// <summary>The tray menu's words in the Windows UI language, English for any language not listed.</summary>
public sealed record TrayText(string Enabled, string Exit, string Paused)
{
    private static readonly TrayText English = new("Enabled", "Exit", "paused");

    private static readonly Dictionary<string, TrayText> ByLanguage = new()
    {
        ["it"] = new("Attivo", "Esci", "in pausa"),
        ["de"] = new("Aktiviert", "Beenden", "angehalten"),
        ["fr"] = new("Activé", "Quitter", "en pause"),
        ["es"] = new("Activado", "Salir", "en pausa"),
        ["pt"] = new("Ativado", "Sair", "em pausa"),
        ["nl"] = new("Ingeschakeld", "Afsluiten", "gepauzeerd"),
        ["pl"] = new("Włączone", "Zakończ", "wstrzymane"),
        ["ru"] = new("Включено", "Выход", "приостановлено"),
        ["ja"] = new("有効", "終了", "一時停止中"),
        ["zh"] = new("启用", "退出", "已暂停"),
        ["ko"] = new("사용", "종료", "일시 중지됨"),
        ["tr"] = new("Etkin", "Çıkış", "duraklatıldı"),
    };

    /// <summary>Picks by the culture name's language; Chinese only in its Simplified forms.</summary>
    public static TrayText For(string cultureName)
    {
        var parts = cultureName.ToLowerInvariant().Split('-');
        var language = parts[0];
        if (language == "zh" && !(parts.Length > 1 && parts[1] is "cn" or "sg" or "hans"))
        {
            return English;
        }
        return ByLanguage.GetValueOrDefault(language, English);
    }

    public string Tooltip(bool enabled) => enabled ? "windowfit" : $"windowfit ({Paused})";
}

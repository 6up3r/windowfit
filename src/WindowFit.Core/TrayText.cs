namespace WindowFit.Core;

/// <summary>The tray menu's words in the Windows UI language, English for any language not listed.</summary>
public sealed record TrayText(
    string Enabled, string Exit, string Paused, string License, string Groups,
    string CheckForUpdates, string InstallUpdate, string WhatsNew, string UpToDate, string UpdateAvailable,
    string CheckFailed, string Install, string Later, string InstallFailed)
{
    private static readonly TrayText English = new("Enabled", "Exit", "paused", "License", "Groups",
        "Check for updates", "Install update {0}", "What's new", "windowfit is up to date.", "windowfit {0} is available.",
        "The update check failed: {0}", "Install", "Later", "The update was not installed: {0}");

    private static readonly Dictionary<string, TrayText> ByLanguage = new()
    {
        ["it"] = new("Attivo", "Esci", "in pausa", "Licenza", "Gruppi",
            "Cerca aggiornamenti", "Installa aggiornamento {0}", "Novità", "windowfit è aggiornato.", "windowfit {0} è disponibile.",
            "Controllo degli aggiornamenti non riuscito: {0}", "Installa", "Più tardi", "L'aggiornamento non è stato installato: {0}"),
        ["de"] = new("Aktiviert", "Beenden", "angehalten", "Lizenz", "Gruppen",
            "Nach Updates suchen", "Update {0} installieren", "Neuigkeiten", "windowfit ist auf dem neuesten Stand.", "windowfit {0} ist verfügbar.",
            "Die Suche nach Updates ist fehlgeschlagen: {0}", "Installieren", "Später", "Das Update wurde nicht installiert: {0}"),
        ["fr"] = new("Activé", "Quitter", "en pause", "Licence", "Groupes",
            "Rechercher des mises à jour", "Installer la mise à jour {0}", "Nouveautés", "windowfit est à jour.", "windowfit {0} est disponible.",
            "La recherche de mises à jour a échoué : {0}", "Installer", "Plus tard", "La mise à jour n'a pas été installée : {0}"),
        ["es"] = new("Activado", "Salir", "en pausa", "Licencia", "Grupos",
            "Buscar actualizaciones", "Instalar la actualización {0}", "Novedades", "windowfit está actualizado.", "windowfit {0} está disponible.",
            "No se pudieron buscar actualizaciones: {0}", "Instalar", "Más tarde", "La actualización no se instaló: {0}"),
        ["pt"] = new("Ativado", "Sair", "em pausa", "Licença", "Grupos",
            "Procurar atualizações", "Instalar a atualização {0}", "Novidades", "O windowfit está atualizado.", "O windowfit {0} está disponível.",
            "Falha ao procurar atualizações: {0}", "Instalar", "Mais tarde", "A atualização não foi instalada: {0}"),
        ["nl"] = new("Ingeschakeld", "Afsluiten", "gepauzeerd", "Licentie", "Groepen",
            "Controleren op updates", "Update {0} installeren", "Wat is er nieuw", "windowfit is up-to-date.", "windowfit {0} is beschikbaar.",
            "Controleren op updates is mislukt: {0}", "Installeren", "Later", "De update is niet geïnstalleerd: {0}"),
        ["pl"] = new("Włączone", "Zakończ", "wstrzymane", "Licencja", "Grupy",
            "Sprawdź aktualizacje", "Zainstaluj aktualizację {0}", "Co nowego", "windowfit jest aktualny.", "windowfit {0} jest dostępny.",
            "Sprawdzanie aktualizacji nie powiodło się: {0}", "Zainstaluj", "Później", "Aktualizacja nie została zainstalowana: {0}"),
        ["ru"] = new("Включено", "Выход", "приостановлено", "Лицензия", "Группы",
            "Проверить обновления", "Установить обновление {0}", "Что нового", "windowfit обновлён.", "Доступна версия windowfit {0}.",
            "Не удалось проверить обновления: {0}", "Установить", "Позже", "Обновление не установлено: {0}"),
        ["ja"] = new("有効", "終了", "一時停止中", "ライセンス", "グループ",
            "更新を確認", "更新プログラム {0} をインストール", "新機能", "windowfit は最新です。", "windowfit {0} を利用できます。",
            "更新の確認に失敗しました: {0}", "インストール", "後で", "更新はインストールされませんでした: {0}"),
        ["zh"] = new("启用", "退出", "已暂停", "许可证", "分组",
            "检查更新", "安装更新 {0}", "新功能", "windowfit 已是最新版本。", "windowfit {0} 可用。",
            "检查更新失败：{0}", "安装", "稍后", "更新未安装：{0}"),
        ["ko"] = new("사용", "종료", "일시 중지됨", "라이선스", "그룹",
            "업데이트 확인", "업데이트 {0} 설치", "새로운 기능", "windowfit이 최신 버전입니다.", "windowfit {0}을(를) 사용할 수 있습니다.",
            "업데이트 확인 실패: {0}", "설치", "나중에", "업데이트가 설치되지 않았습니다: {0}"),
        ["tr"] = new("Etkin", "Çıkış", "duraklatıldı", "Lisans", "Gruplar",
            "Güncellemeleri denetle", "{0} güncellemesini yükle", "Yenilikler", "windowfit güncel.", "windowfit {0} kullanılabilir.",
            "Güncelleme denetimi başarısız oldu: {0}", "Yükle", "Daha sonra", "Güncelleme yüklenmedi: {0}"),
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

    /// <summary>A menu item that opens a window, with the ellipsis Windows menus give one.</summary>
    public string LicenseItem => $"{License}...";

    public string WhatsNewItem => $"{WhatsNew}...";

    public string InstallUpdateItem(Version version) => string.Format(InstallUpdate, version.ToString(3)) + "...";

    public string Available(Version version) => string.Format(UpdateAvailable, version.ToString(3));

    public string Tooltip(bool enabled) => enabled ? "windowfit" : $"windowfit ({Paused})";
}

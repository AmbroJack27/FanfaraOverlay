using System.Collections.Generic;

namespace Fanfara;

/// <summary>
/// Translations for the native (non-WebView) UI: the tray menu and the
/// update message boxes. The notification overlay and the Settings window
/// get their strings from web/i18n.js instead; this table keeps the handful
/// of C#-side strings in sync with the five supported languages.
/// </summary>
public static class Loc
{
    private static readonly Dictionary<string, Dictionary<string, string>> T = new()
    {
        ["it"] = new()
        {
            ["settings"]       = "Impostazioni",
            ["testNotif"]      = "Prova notifica",
            ["checkUpdates"]   = "Controlla aggiornamenti",
            ["quit"]           = "Esci",
            ["preview"]        = "Anteprima!",
            ["noUpdateBody"]   = "Nessun aggiornamento disponibile: hai già l'ultima versione.",
            ["updateAvailTitle"] = "Aggiornamento disponibile",
            ["updateAvailBody"]  = "È disponibile Fanfara {0}.\n\nVuoi scaricarla e aggiornare ora?\n(Fanfara si chiuderà per installare la nuova versione.)",
            ["downloadFailBody"] = "Non è stato possibile scaricare l'aggiornamento. Riprova più tardi, oppure scaricalo dalla pagina GitHub.",
            ["restartGame"]      = "Gioco preparato per le notifiche in sovrimpressione. Riavvia il gioco una volta: da adesso in poi sarà automatico.",
            ["mpoRestartTitle"]  = "Riavvio necessario",
            ["mpoRestartBody"]   = "Impostazione applicata. Per attivarla serve riavviare il PC (una sola volta: poi resta attiva).\n\nRiavviare ora?",
            ["mpoCancelBody"]    = "Operazione annullata: l'opzione non è stata modificata.",
        },
        ["en"] = new()
        {
            ["settings"]       = "Settings",
            ["testNotif"]      = "Test notification",
            ["checkUpdates"]   = "Check for updates",
            ["quit"]           = "Quit",
            ["preview"]        = "Preview!",
            ["noUpdateBody"]   = "No update available: you already have the latest version.",
            ["updateAvailTitle"] = "Update available",
            ["updateAvailBody"]  = "Fanfara {0} is available.\n\nDo you want to download and update now?\n(Fanfara will close to install the new version.)",
            ["downloadFailBody"] = "The update could not be downloaded. Try again later, or download it from the GitHub page.",
            ["restartGame"]      = "Game prepared for on-screen notifications. Restart the game once — from now on it's automatic.",
            ["mpoRestartTitle"]  = "Restart required",
            ["mpoRestartBody"]   = "Setting applied. A one-time PC restart is needed to activate it (after that it stays on).\n\nRestart now?",
            ["mpoCancelBody"]    = "Cancelled: the option was not changed.",
        },
        ["fr"] = new()
        {
            ["settings"]       = "Paramètres",
            ["testNotif"]      = "Tester la notification",
            ["checkUpdates"]   = "Vérifier les mises à jour",
            ["quit"]           = "Quitter",
            ["preview"]        = "Aperçu !",
            ["noUpdateBody"]   = "Aucune mise à jour disponible : vous avez déjà la dernière version.",
            ["updateAvailTitle"] = "Mise à jour disponible",
            ["updateAvailBody"]  = "Fanfara {0} est disponible.\n\nVoulez-vous la télécharger et mettre à jour maintenant ?\n(Fanfara se fermera pour installer la nouvelle version.)",
            ["downloadFailBody"] = "Impossible de télécharger la mise à jour. Réessayez plus tard ou téléchargez-la depuis la page GitHub.",
            ["restartGame"]      = "Jeu préparé pour les notifications à l'écran. Redémarre le jeu une fois : à partir de maintenant, c'est automatique.",
            ["mpoRestartTitle"]  = "Redémarrage nécessaire",
            ["mpoRestartBody"]   = "Réglage appliqué. Un redémarrage du PC est nécessaire pour l'activer (une seule fois : ensuite il reste actif).\n\nRedémarrer maintenant ?",
            ["mpoCancelBody"]    = "Annulé : l'option n'a pas été modifiée.",
        },
        ["de"] = new()
        {
            ["settings"]       = "Einstellungen",
            ["testNotif"]      = "Benachrichtigung testen",
            ["checkUpdates"]   = "Nach Updates suchen",
            ["quit"]           = "Beenden",
            ["preview"]        = "Vorschau!",
            ["noUpdateBody"]   = "Kein Update verfügbar: Du hast bereits die neueste Version.",
            ["updateAvailTitle"] = "Update verfügbar",
            ["updateAvailBody"]  = "Fanfara {0} ist verfügbar.\n\nMöchtest du es jetzt herunterladen und aktualisieren?\n(Fanfara wird geschlossen, um die neue Version zu installieren.)",
            ["downloadFailBody"] = "Das Update konnte nicht heruntergeladen werden. Versuche es später erneut oder lade es von der GitHub-Seite herunter.",
            ["restartGame"]      = "Spiel für Bildschirm-Benachrichtigungen vorbereitet. Starte das Spiel einmal neu – ab jetzt geht es automatisch.",
            ["mpoRestartTitle"]  = "Neustart erforderlich",
            ["mpoRestartBody"]   = "Einstellung übernommen. Zum Aktivieren ist ein einmaliger PC-Neustart nötig (danach bleibt sie aktiv).\n\nJetzt neu starten?",
            ["mpoCancelBody"]    = "Abgebrochen: Die Option wurde nicht geändert.",
        },
        ["es"] = new()
        {
            ["settings"]       = "Ajustes",
            ["testNotif"]      = "Probar notificación",
            ["checkUpdates"]   = "Buscar actualizaciones",
            ["quit"]           = "Salir",
            ["preview"]        = "¡Vista previa!",
            ["noUpdateBody"]   = "No hay actualizaciones disponibles: ya tienes la última versión.",
            ["updateAvailTitle"] = "Actualización disponible",
            ["updateAvailBody"]  = "Fanfara {0} está disponible.\n\n¿Quieres descargarla y actualizar ahora?\n(Fanfara se cerrará para instalar la nueva versión.)",
            ["downloadFailBody"] = "No se pudo descargar la actualización. Inténtalo más tarde o descárgala desde la página de GitHub.",
            ["restartGame"]      = "Juego preparado para las notificaciones en pantalla. Reinicia el juego una vez: a partir de ahora es automático.",
            ["mpoRestartTitle"]  = "Reinicio necesario",
            ["mpoRestartBody"]   = "Ajuste aplicado. Para activarlo hace falta reiniciar el PC (una sola vez: luego queda activo).\n\n¿Reiniciar ahora?",
            ["mpoCancelBody"]    = "Cancelado: la opción no se modificó.",
        },
    };

    /// <summary>Look up a key for the given language, falling back to English then the key itself.</summary>
    public static string S(string lang, string key)
    {
        if (T.TryGetValue(lang, out var map) && map.TryGetValue(key, out var v)) return v;
        if (T["en"].TryGetValue(key, out var en)) return en;
        return key;
    }
}

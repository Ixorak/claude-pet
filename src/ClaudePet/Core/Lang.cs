namespace ClaudePet.Core;

/// <summary>
/// Langue de Pet : l'anglais par défaut, le français au choix (réglages des bulles, ou menu de l'icône).
/// Tous les textes que Pet affiche sont ici ; le journal de développement reste en français.
/// </summary>
internal static class Lang
{
    public const string En = "en", Fr = "fr";

    public static bool French { get; private set; }
    public static string Code => French ? Fr : En;

    public static void Set(string? code) => French = code == Fr;

    private static string T(string en, string fr) => French ? fr : en;

    // menus (bulles et icône de notification)
    public static string Calm => T("Calm", "Calme");
    public static string Mute => T("Mute", "Muet");
    public static string Feed => T("Feed", "Nourrir");
    public static string Sleep => T("Sleep", "Dormir");
    public static string GoHome => T("Go home", "Rentrer à la maison");
    public static string Settings => T("Settings", "Réglages");
    public static string Hunger => T("Hunger", "Faim");
    public static string Language => T("Language", "Langue");

    // réglages
    public const string Volume = "Volume";
    public static string Size => T("Size", "Taille");

    // ce que Pet dit
    public static string Hungry => T("I'm hungry!", "J'ai faim !");
}

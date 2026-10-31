using System.Globalization;
using ClaudePet.Util;

namespace ClaudePet.Core;

// Parties de l'application qui grandissent au fil des étapes (bulles, baie, sons...).
internal sealed unsafe partial class App
{
    public const string BubbleClass = "ClaudePet.Bubbles";
    public const string BerryClass = "ClaudePet.Berry";

    /// <summary>Nom du raccourci de la maison sur le bureau (modifiable pour les tests : --house &lt;nom&gt;).</summary>
    public static string HouseName = "Pet";

    private bool spawnPending;
    private double spawnDeadline, nextSpawnTry, quitAt;

    private partial void InitExtras(string[] args)
    {
        int hi = Array.IndexOf(args, "--house");
        if (hi >= 0 && hi + 1 < args.Length) HouseName = args[hi + 1];
    }

    /// <summary>Apparition : par la porte de l'icône maison, sinon en sortant du sol en bas du Dell.</summary>
    private partial void StartPet(string[] args)
    {
        spawnPending = true;
        spawnDeadline = Clock.Now + 8;   // au démarrage du PC, le bureau peut ne pas être encore prêt
        nextSpawnTry = 0;
        TrySpawn(Clock.Now);
    }

    private void TrySpawn(double now)
    {
        var r = DesktopIcons.Find(HouseName, out var icon);
        Desk.Refresh();
        if (r == DesktopIcons.Result.Found)
        {
            var s = Desk.At((icon.Rect.Left + icon.Rect.Right) / 2.0, (icon.Rect.Top + icon.Rect.Bottom) / 2.0);
            if (s != null && !s.Fullscreen)
            {
                spawnPending = false;
                Pet.SpawnAtDoor(icon);
                return;
            }
        }
        if (r == DesktopIcons.Result.ShellNotReady && now < spawnDeadline)
        {
            nextSpawnTry = now + 0.5;
            return;
        }
        spawnPending = false;
        var free = Desk.Primary.Fullscreen ? Desk.NearestFree(Desk.Primary.CenterX, Desk.Primary.Floor) : Desk.Primary;
        if (free == null) { Log.Write("Tous les écrans sont en plein écran : Pet attend caché"); Pet.HideAll(); return; }
        Pet.EmergeAt(free, free.CenterX + Rng.Range(-0.15, 0.15) * free.Work.Width);
    }

    /// <summary>Quitte l'appli après un court délai (le temps de finir une animation).</summary>
    public void QuitSoon(double delay)
    {
        double t = Clock.Now + delay;
        if (quitAt == 0 || t < quitAt) quitAt = t;
        Wake();
    }

    private partial double ExtrasInterval()
    {
        double d = double.MaxValue;
        if (spawnPending) d = Math.Min(d, Math.Max(0.02, nextSpawnTry - Clock.Now));
        if (quitAt > 0) d = Math.Min(d, Math.Max(0.01, quitAt - Clock.Now));
        return d;
    }

    private partial void BeforePetUpdate(double dt, double now)
    {
        if (spawnPending && now >= nextSpawnTry) TrySpawn(now);
        if (quitAt > 0 && now >= quitAt) Quit();
    }

    private partial void AfterPetUpdate(double dt, double now) { }

    private partial void RenderExtras(double now) { }

    private partial void BringExtrasToTop() { }

    private partial void SaveState() => Cfg.Save();

    private partial bool HandleExtras(nint hwnd, uint msg, nuint wp, nint lp, ref nint result) => false;

    private partial void OnPetMouseDown(double now) => Pet.OnMouseDown(Cursor.X, Cursor.Y, now);

    private partial void PlaySound(Snd s, double volume) { }

    private partial void OnMuteChanged() { }

    private partial void ShutdownExtras() { }

    /// <summary>Commandes de test envoyées par « ClaudePet.exe --cmd ... » (outil de développement).</summary>
    private partial void DebugCommand(string text)
    {
        Log.Write("Commande : " + text);
        var a = text.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (a.Length == 0) return;
        double D(int i, double def = 0) => a.Length > i && double.TryParse(a[i], NumberStyles.Float, CultureInfo.InvariantCulture, out var v) ? v : def;
        switch (a[0])
        {
            case "throw":
                Pet.Debug_Throw(D(1), D(2));
                break;
            case "goto":
                Pet.Debug_Goto(D(1), D(2));
                break;
            case "state":
                Log.Write(Pet.Describe());
                break;
            case "sit":
                Pet.Enter(St.Sit, D(1, 30));
                break;
            case "home":
                GoHome();
                break;
            case "findicon":
                var r = DesktopIcons.Find(a.Length > 1 ? string.Join(' ', a.Skip(1)) : HouseName, out var ic);
                Log.Write($"findicon : {r} {ic.Rect} taille={ic.Size} via {ic.How}");
                break;
            case "dig":
                Pet.StartDig(quit: false);
                break;
            case "quit":
                Quit();
                break;
            default:
                DebugCommandMore(a);
                break;
        }
        Wake();
    }

    private void DebugCommandMore(string[] a) { }
}

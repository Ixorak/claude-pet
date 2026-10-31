using System.Globalization;
using ClaudePet.Audio;
using ClaudePet.Native;
using ClaudePet.Util;

namespace ClaudePet.Core;

// Parties de l'application qui grandissent au fil des étapes (bulles, baie, sons...).
internal sealed unsafe partial class App
{
    public const string BubbleClass = "ClaudePet.Bubbles";
    public const string BerryClass = "ClaudePet.Berry";

    /// <summary>Nom du raccourci de la maison sur le bureau (modifiable pour les tests : --house &lt;nom&gt;).</summary>
    public static string HouseName = "Pet";

    public const uint MsgCloseBubbles = C.WM_APP + 20;

    private bool spawnPending;
    private double spawnDeadline, nextSpawnTry, quitAt;
    private Mixer? mixer;
    public Bubbles Bubbles = null!;

    private partial void InitExtras(string[] args)
    {
        int hi = Array.IndexOf(args, "--house");
        if (hi >= 0 && hi + 1 < args.Length) HouseName = args[hi + 1];
        try { mixer = new Mixer(); } catch (Exception e) { Log.Write("Audio indisponible : " + e.Message); }
        ApplyVolume();
        Bubbles = new Bubbles(this);
        InitFeed();
    }

    /// <summary>Volume et muet : appliqués au mélangeur (courbe adaptée à l'oreille).</summary>
    public void ApplyVolume()
    {
        float v = Cfg.Muted ? 0 : (float)(Math.Pow(Cfg.Volume / 100.0, 1.7) * 1.2);
        mixer?.SetMaster(v);
    }

    public void OpenBubbles()
    {
        if (Pet.State is St.Hidden or St.Air or St.Dragged or St.HangCursor or St.SitCursor or St.ClimbCursor
            or St.HomeJump or St.EnterDoor or St.Dig or St.Underground or St.ExitDoor or St.Emerge) return;
        if (Pet.State == St.Sleep) Pet.WakeUp();
        Pet.PrepareForBubbles();
        Bubbles.Open();
        Wake();
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
        if (Bubbles.IsOpen) d = Bubbles.Animating ? 1.0 / 60 : 1.0 / 20;
        if (spawnPending) d = Math.Min(d, Math.Max(0.02, nextSpawnTry - Clock.Now));
        if (quitAt > 0) d = Math.Min(d, Math.Max(0.01, quitAt - Clock.Now));
        if (Cfg.Pending) d = Math.Min(d, 0.5);
        if (FeedingActive) d = Math.Min(d, Math.Min(Berry.DesiredInterval, 1.0 / 30));
        return d;
    }

    private partial void BeforePetUpdate(double dt, double now)
    {
        if (spawnPending && now >= nextSpawnTry) TrySpawn(now);
        if (quitAt > 0 && now >= quitAt) Quit();
        UpdateHunger(now);
    }

    private partial void AfterPetUpdate(double dt, double now)
    {
        PetWin.SetClickThrough(Pet.ClickThrough);
        Bubbles.Update(now);
        Berry.Update(dt, now);
        Cfg.Flush();
    }

    private partial void RenderExtras(double now) { }

    private partial void BringExtrasToTop()
    {
        if (Bubbles.IsOpen) Bubbles.Win.BringToTop();
        if (FeedingActive) Berry.Win.BringToTop();
    }

    private partial void SaveState()
    {
        StoreHunger();
        Cfg.Flush(force: true);
    }

    private partial bool HandleExtras(nint hwnd, uint msg, nuint wp, nint lp, ref nint result)
    {
        if (Bubbles != null && hwnd == Bubbles.Win.Hwnd)
        {
            switch (msg)
            {
                case C.WM_MOUSEACTIVATE: result = C.MA_ACTIVATE; return true;
                case C.WM_ACTIVATE:
                    if ((int)(wp & 0xFFFF) == C.WA_INACTIVE) Bubbles.OnDeactivated();
                    return false;
                case C.WM_KEYDOWN:
                    if ((int)wp == C.VK_ESCAPE) { Bubbles.Close(); Wake(); }
                    return true;
                case C.WM_SETCURSOR: User32.SetCursor(User32.LoadCursor(0, C.IDC_HAND)); result = 1; return true;
                case C.WM_LBUTTONDOWN: Cursor.Update(Clock.Now); Bubbles.OnMouseDown((int)Cursor.X, (int)Cursor.Y); Wake(); return true;
                case C.WM_MOUSEMOVE: Cursor.Update(Clock.Now); Bubbles.OnMouseMove((int)Cursor.X, (int)Cursor.Y); Wake(); return true;
                case C.WM_LBUTTONUP: Cursor.Update(Clock.Now); Bubbles.OnMouseUp((int)Cursor.X, (int)Cursor.Y); Wake(); return true;
                case C.WM_CAPTURECHANGED: Bubbles.OnCaptureLost(); return true;
                case C.WM_RBUTTONDOWN: case C.WM_RBUTTONUP: return true;
                case MsgCloseBubbles: Bubbles.Close(); Wake(); return true;
            }
            return false;
        }
        return HandleFeedWindow(hwnd, msg, wp, lp, ref result);
    }

    private partial bool HandleFeedWindow(nint hwnd, uint msg, nuint wp, nint lp, ref nint result);

    private partial void OnPetMouseDown(double now)
    {
        if (Bubbles.IsOpen) { Bubbles.Close(); return; }
        Pet.OnMouseDown(Cursor.X, Cursor.Y, now);
    }

    private partial void PlaySound(Snd s, double volume)
    {
        if (mixer == null || Cfg.Muted || Cfg.Volume <= 0 || volume <= 0) return;
        double pitch = Rng.Range(0.95, 1.05);
        mixer.Play(Sfx.Make(s, pitch), (float)volume);
    }

    private partial void OnMuteChanged()
    {
        ApplyVolume();
        Bubbles?.MarkDirty();
    }

    private partial void ShutdownExtras()
    {
        Bubbles.Close();
        mixer?.Dispose();
        User32.DestroyWindow(Bubbles.Win.Hwnd);
        ShutdownFeed();
    }

    private partial void ShutdownFeed();

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

    private void DebugCommandMore(string[] a)
    {
        if (FeedDebugCommand(a)) return;
        switch (a[0])
        {
            case "calm": SetCalm(a.Length < 2 || a[1] == "on"); break;
            case "mute": SetMuted(a.Length < 2 || a[1] == "on"); break;
            case "bubbles": OpenBubbles(); break;
            case "stats": Log.Write(TakeStats()); break;
            case "sleep": Pet.SleepUntilClicked(); break;
            case "sound":
                if (a.Length > 1 && Enum.TryParse<Snd>(a[1], true, out var snd)) Play(snd, 1.0);
                break;
            case "debug": Pet.DebugAction(a.Length > 1 ? a[1] : ""); break;
            case "dumpui":
                Bubbles.DumpBmp(Path.Combine(Config.Folder, "bulles.bmp"), false);
                Bubbles.DumpBmp(Path.Combine(Config.Folder, "bulles-reglages.bmp"), true);
                break;
        }
    }
}

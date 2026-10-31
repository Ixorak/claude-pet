using System.Runtime.InteropServices;
using ClaudePet.Art;
using ClaudePet.Gfx;
using ClaudePet.Native;
using ClaudePet.Util;

namespace ClaudePet.Core;

/// <summary>L'application : fenêtres, boucle principale (sans boucle à fond), zone de notification, messages Windows.</summary>
internal sealed unsafe partial class App
{
    public static App I = null!;

    public const string ControllerClass = "ClaudePet.Controller";
    public const string SpriteClass = "ClaudePet.Sprite";
    public const string FxClass = "ClaudePet.Fx";
    public const uint MsgGoHome = C.WM_APP + 10;   // une 2e instance demande à Pet de rentrer
    public const uint MsgQuitNow = C.WM_APP + 11;  // l'installateur demande de quitter tout de suite

    public readonly Config Cfg = new();
    public readonly Desk Desk = new();
    public readonly CursorTracker Cursor = new();
    public Pet Pet = null!;
    public Fx Fx = null!;
    public LayeredWindow PetWin = null!, FxWin = null!;
    public nint Ctrl;
    private readonly Tray tray = new();
    private nint timer;
    private bool quit;
    private double nextTick, lastTick;
    private uint taskbarCreatedMsg;
    private bool screensDirty = true;
    private double nextTopmost, nextScreenCheck, nextSave;
    public bool SessionLocked;

    // cache de rendu de Pet
    private readonly ArtCanvas canvas = PetPainter.NewCanvas();
    private PetPose cachedPose;
    private int cachedFacing;
    private double cachedScale = -1;
    private int bx0, by0, bw, bh, lastDrawnH = -1;
    private bool haveCache, contentDirty;

    public void Init(string[] args)
    {
        Cfg.Load();
        RegisterClasses();
        nint inst = Kernel32.GetModuleHandle(null);
        Ctrl = User32.CreateWindowEx(C.WS_EX_TOOLWINDOW, ControllerClass, "Claude Pet", C.WS_POPUP, 0, 0, 0, 0, 0, 0, inst, 0);
        PetWin = new LayeredWindow(SpriteClass, clickThrough: false);
        FxWin = new LayeredWindow(FxClass, clickThrough: true);
        Fx = new Fx(FxWin);
        taskbarCreatedMsg = User32.RegisterWindowMessage("TaskbarCreated");
        WtsApi32.WTSRegisterSessionNotification(Ctrl, C.NOTIFY_FOR_THIS_SESSION);
        timer = Kernel32.CreateWaitableTimerEx(0, null, C.CREATE_WAITABLE_TIMER_HIGH_RESOLUTION, C.TIMER_ALL_ACCESS);
        if (timer == 0) timer = Kernel32.CreateWaitableTimerEx(0, null, 0, C.TIMER_ALL_ACCESS);

        Desk.Refresh();
        screensDirty = false;
        tray.Add(Ctrl);
        Pet = new Pet(this);
        InitExtras(args);
        Cursor.Update(Clock.Now);
        StartPet(args);
        lastTick = Clock.Now;
    }

    private partial void InitExtras(string[] args);
    private partial void StartPet(string[] args);

    private void RegisterClasses()
    {
        nint inst = Kernel32.GetModuleHandle(null);
        nint arrow = User32.LoadCursor(0, C.IDC_ARROW);
        foreach (var name in new[] { ControllerClass, SpriteClass, FxClass, BubbleClass, BerryClass })
        {
            fixed (char* n = name)
            {
                WNDCLASSEXW wc = new()
                {
                    cbSize = (uint)sizeof(WNDCLASSEXW),
                    lpfnWndProc = &WndProc,
                    hInstance = inst,
                    hCursor = arrow,
                    lpszClassName = n,
                };
                User32.RegisterClassEx(&wc);
            }
        }
    }

    // ------------------------------------------------------------------ boucle

    public void Run()
    {
        nint h = timer;
        while (!quit)
        {
            double now = Clock.Now;
            if (now >= nextTick - 0.0007)
            {
                Tick(now);
                double delay = DesiredDelay();
                nextTick = now + delay;
                Arm(delay);
            }
            User32.MsgWaitForMultipleObjectsEx(1, &h, C.INFINITE, C.QS_ALLINPUT, C.MWMO_INPUTAVAILABLE);
            MSG msg;
            while (User32.PeekMessage(&msg, 0, 0, 0, C.PM_REMOVE) != 0)
            {
                if (msg.message == C.WM_QUIT) { quit = true; break; }
                User32.TranslateMessage(&msg);
                User32.DispatchMessage(&msg);
            }
        }
    }

    private void Arm(double seconds)
    {
        long due = -(long)Math.Max(10_000, seconds * 10_000_000);
        Kernel32.SetWaitableTimer(timer, &due, 0, 0, 0, 0);
    }

    /// <summary>Demande une mise à jour immédiate (après un clic, par exemple).</summary>
    public void Wake()
    {
        nextTick = 0;
        Arm(0.001);
    }

    private double DesiredDelay()
    {
        double d = Pet.DesiredInterval();
        d = Math.Min(d, Fx.DesiredInterval);
        if (Pet.IsPressing) d = Math.Min(d, 1.0 / 60);
        d = Math.Min(d, ExtrasInterval());
        if (SessionLocked) d = Math.Max(d, 1.0);
        return d;
    }

    private partial double ExtrasInterval();

    // mesure de la charge (commande de test « stats »)
    private long statTicks;
    private double statBusy, statSince;
    private readonly double[] statPart = new double[5]; // logique, rendu Pet, rendu effets, premier plan, reste

    public string TakeStats()
    {
        double now = Clock.Now, span = Math.Max(0.001, now - statSince);
        double n = Math.Max(1, statTicks);
        string s = $"Charge : {statTicks / span:F1} mises à jour par seconde, {statBusy * 1000 / n:F2} ms chacune, {statBusy / span * 100:F2} % d'un cœur"
                 + $" | logique {statPart[0] * 1000 / n:F2} ms, Pet {statPart[1] * 1000 / n:F2} ms, effets {statPart[2] * 1000 / n:F2} ms, premier plan {statPart[3] * 1000 / n:F2} ms, reste {statPart[4] * 1000 / n:F2} ms";
        statTicks = 0; statBusy = 0; statSince = now;
        Array.Clear(statPart);
        return s;
    }

    private double statMark;
    private void Lap(int part)
    {
        double t = Clock.Now;
        statPart[part] += t - statMark;
        statMark = t;
    }

    private void Tick(double now)
    {
        TickInner(now);
        statTicks++;
        statBusy += Clock.Now - now;
    }

    private void TickInner(double now)
    {
        double dt = Math.Clamp(now - lastTick, 0, 0.05);
        lastTick = now;
        statMark = now;
        Cursor.Update(now);
        if (screensDirty || now >= nextScreenCheck)
        {
            screensDirty = false;
            nextScreenCheck = now + 5;
            if (Desk.Refresh()) OnScreensChanged();
        }
        BeforePetUpdate(dt, now);
        Pet.Update(dt, now);
        AfterPetUpdate(dt, now);
        Fx.Update(dt);
        Lap(0);
        RenderPet();
        Lap(1);
        Fx.Render(Pet);
        Lap(2);
        RenderExtras(now);
        if (now >= nextTopmost)
        {
            // rappel « au premier plan » : plus espacé quand il dort ou qu'Ixorak est absent
            nextTopmost = now + (Pet.State == St.Sleep || SysInfo.UserIdleSeconds > 60 ? 5 : 1.5);
            PetWin.BringToTop();
            FxWin.BringToTop();
            BringExtrasToTop();
        }
        Lap(3);
        if (now >= nextSave)
        {
            nextSave = now + 60;
            SaveState();
        }
        Lap(4);
    }

    private partial void BeforePetUpdate(double dt, double now);
    private partial void AfterPetUpdate(double dt, double now);
    private partial void RenderExtras(double now);
    private partial void BringExtrasToTop();
    private partial void SaveState();

    private void OnScreensChanged()
    {
        nextWinScan = 0;
        if (Pet.State == St.Hidden) return;
        Pet.OnScreensChanged();
    }

    private void RenderPet()
    {
        if (Pet.State == St.Hidden)
        {
            PetWin.Hide();
            haveCache = false;
            return;
        }
        var pose = Pet.GetPose(out double clipY);
        double s = Pet.RenderScale;
        if (!haveCache || pose != cachedPose || Pet.Facing != cachedFacing || s != cachedScale)
        {
            PetPainter.Draw(canvas, pose);
            if (Pet.Facing < 0) canvas.MirrorX();
            if (!canvas.Bounds(out bx0, out by0, out int x1, out int y1)) { PetWin.Hide(); return; }
            bw = x1 - bx0 + 1;
            bh = y1 - by0 + 1;
            cachedPose = pose; cachedFacing = Pet.Facing; cachedScale = s;
            haveCache = true;
            contentDirty = true;
        }
        int left = (int)Math.Round(Pet.X + (bx0 - canvas.OX) * s);
        int top = (int)Math.Round(Pet.Y + (by0 - canvas.OY) * s);
        int dw = Math.Max(1, (int)Math.Ceiling(bw * s));
        int dh = Math.Max(1, (int)Math.Ceiling(bh * s));
        if (Math.Abs(Pet.RenderAngle) > 0.004)
        {
            RenderRotated(left, top, dw, dh, s, Pet.RenderAngle);
            return;
        }
        if (rotatedShown) { rotatedShown = false; contentDirty = true; }
        int drawH = dh;
        if (!double.IsNaN(clipY)) drawH = Math.Min(dh, (int)Math.Floor(clipY) - top);
        if (drawH <= 0) { PetWin.Hide(); lastDrawnH = -1; return; }
        if (contentDirty || drawH != lastDrawnH || !PetWin.Shown || dw != PetWin.W)
        {
            uint* bits = PetWin.Begin(dw, drawH);
            Blit.Art(canvas, bx0, by0, bw, bh, s, bits, PetWin.Stride, dw, drawH);
            PetWin.Commit(left, top, dw, drawH);
            contentDirty = false;
            lastDrawnH = drawH;
        }
        else PetWin.Move(left, top);
    }

    private bool rotatedShown;
    private double rotatedAngle;

    /// <summary>
    /// Pet tourné autour du pivot (le bout du pointeur quand il s'y balance). (left, top, dw, dh) : le rectangle du dessin
    /// droit ; la fenêtre prend la taille du rectangle tourné.
    /// </summary>
    private void RenderRotated(int left, int top, int dw, int dh, double s, double angle)
    {
        double pivX = Pet.PivotX - left, pivY = Pet.PivotY - top;
        double c = Math.Cos(angle), sn = Math.Sin(angle);
        double minX = double.MaxValue, minY = double.MaxValue, maxX = double.MinValue, maxY = double.MinValue;
        for (int k = 0; k < 4; k++)
        {
            double rx = ((k & 1) == 0 ? 0 : dw) - pivX, ry = ((k & 2) == 0 ? 0 : dh) - pivY;
            double fx = c * rx + sn * ry + pivX, fy = -sn * rx + c * ry + pivY;
            if (fx < minX) minX = fx;
            if (fx > maxX) maxX = fx;
            if (fy < minY) minY = fy;
            if (fy > maxY) maxY = fy;
        }
        int offX = (int)Math.Floor(minX), offY = (int)Math.Floor(minY);
        int rw = (int)Math.Ceiling(maxX) - offX + 1, rh = (int)Math.Ceiling(maxY) - offY + 1;
        bool redraw = contentDirty || !rotatedShown || !PetWin.Shown || Math.Abs(angle - rotatedAngle) > 0.003
                      || rw != PetWin.W || rh != PetWin.H;
        if (redraw)
        {
            uint* bits = PetWin.Begin(rw, rh);
            Blit.ArtRotated(canvas, bx0, by0, bw, bh, s, c, sn, pivX, pivY, offX, offY, bits, PetWin.Stride, rw, rh);
            PetWin.Commit(left + offX, top + offY, rw, rh);
            contentDirty = false;
            rotatedShown = true;
            rotatedAngle = angle;
            lastDrawnH = -1;
        }
        else PetWin.Move(left + offX, top + offY);
    }

    public void Play(Snd s, double volume = 1.0) => PlaySound(s, volume);

    private partial void PlaySound(Snd s, double volume);

    // ------------------------------------------------------------------ messages

    [UnmanagedCallersOnly]
    private static nint WndProc(nint hwnd, uint msg, nuint wp, nint lp)
    {
        try
        {
            var app = I;
            if (app != null && app.Handle(hwnd, msg, wp, lp, out nint r)) return r;
        }
        catch (Exception e)
        {
            Log.Write("Erreur dans WndProc : " + e);
        }
        return User32.DefWindowProc(hwnd, msg, wp, lp);
    }

    private bool Handle(nint hwnd, uint msg, nuint wp, nint lp, out nint result)
    {
        result = 0;
        switch (msg)
        {
            case C.WM_DISPLAYCHANGE:
                screensDirty = true; Wake();
                return false;
            case C.WM_SETTINGCHANGE:
                if ((uint)wp == C.SPI_SETWORKAREA) { screensDirty = true; Wake(); }
                return false;
            case C.WM_DPICHANGED:
                screensDirty = true; Wake();
                return true; // on gère nous-mêmes taille et position
        }

        if (PetWin != null && hwnd == PetWin.Hwnd) return HandleSprite(hwnd, msg, wp, lp, ref result);
        if (hwnd == Ctrl) return HandleController(hwnd, msg, wp, lp, ref result);
        return HandleExtras(hwnd, msg, wp, lp, ref result);
    }

    private partial bool HandleExtras(nint hwnd, uint msg, nuint wp, nint lp, ref nint result);

    private bool HandleSprite(nint hwnd, uint msg, nuint wp, nint lp, ref nint result)
    {
        double now = Clock.Now;
        switch (msg)
        {
            case C.WM_MOUSEACTIVATE:
                result = C.MA_NOACTIVATE;
                return true;
            case C.WM_SETCURSOR:
                User32.SetCursor(User32.LoadCursor(0, C.IDC_HAND));
                result = 1;
                return true;
            case C.WM_LBUTTONDOWN:
                Cursor.Update(now);
                User32.SetCapture(hwnd);
                OnPetMouseDown(now);
                Wake();
                return true;
            case C.WM_MOUSEMOVE:
                if (Pet.IsPressing)
                {
                    Cursor.Update(now);
                    Pet.OnMouseMove(Cursor.X, Cursor.Y);
                }
                else if (Pet.IsPetting)
                {
                    Cursor.Update(now);
                    Pet.OnPettingMove(Cursor.X, Cursor.Y, now);
                    Wake();
                }
                return true;
            case C.WM_LBUTTONUP:
                Cursor.Update(now);
                bool wasPressing = Pet.IsPressing;
                Pet.OnMouseUp(Cursor.X, Cursor.Y, now);
                if (User32.GetCapture() == hwnd) User32.ReleaseCapture();
                if (wasPressing) Wake();
                return true;
            case C.WM_CAPTURECHANGED:
                if (lp != hwnd) { Pet.OnCaptureLost(now); Pet.StopPetting(now); }
                return true;
            case C.WM_RBUTTONDOWN:
                // clic droit maintenu : des caresses (aucun menu ne s'ouvre)
                Cursor.Update(now);
                if (Bubbles.IsOpen) Bubbles.Close();
                if (!Pet.IsPressing && Pet.StartPetting(now)) User32.SetCapture(hwnd);
                Wake();
                return true;
            case C.WM_RBUTTONUP:
                if (Pet.IsPetting)
                {
                    Pet.StopPetting(now);
                    if (User32.GetCapture() == hwnd) User32.ReleaseCapture();
                    Wake();
                }
                return true;
            case C.WM_CONTEXTMENU:
                return true;
        }
        return false;
    }

    private partial void OnPetMouseDown(double now);

    private bool HandleController(nint hwnd, uint msg, nuint wp, nint lp, ref nint result)
    {
        if (msg == Tray.CallbackMessage)
        {
            uint ev = (uint)(lp & 0xFFFF);
            if (ev == C.WM_CONTEXTMENU || ev == C.WM_RBUTTONUP || ev == C.NIN_SELECT || ev == C.NIN_KEYSELECT)
            {
                Cursor.Update(Clock.Now);
                uint cmd = tray.ShowMenu((int)Cursor.X, (int)Cursor.Y, Cfg.Calm, Cfg.Muted);
                OnTrayCommand(cmd);
            }
            return true;
        }
        if (msg == taskbarCreatedMsg && taskbarCreatedMsg != 0)
        {
            tray.Add(Ctrl);
            return true;
        }
        switch (msg)
        {
            case MsgGoHome:
                GoHome();
                return true;
            case MsgQuitNow:
            case C.WM_CLOSE:
                Quit();
                return true;
            case C.WM_QUERYENDSESSION:
                SaveState();
                result = 1;
                return true;
            case C.WM_ENDSESSION:
                if (wp != 0) { SaveState(); Quit(); }
                return true;
            case C.WM_POWERBROADCAST:
                if ((uint)wp == C.PBT_APMSUSPEND) SaveState();
                else if ((uint)wp == C.PBT_APMRESUMEAUTOMATIC || (uint)wp == C.PBT_APMRESUMESUSPEND) { screensDirty = true; Wake(); }
                result = 1;
                return true;
            case C.WM_WTSSESSION_CHANGE:
                if ((uint)wp == C.WTS_SESSION_LOCK) SessionLocked = true;
                else if ((uint)wp == C.WTS_SESSION_UNLOCK) { SessionLocked = false; screensDirty = true; Wake(); }
                return true;
            case C.WM_COPYDATA:
                var cds = (COPYDATASTRUCT*)lp;
                if (cds != null && cds->lpData != 0 && cds->cbData > 0 && cds->cbData < 4096)
                {
                    string text = new string((char*)cds->lpData, 0, (int)(cds->cbData / 2));
                    DebugCommand(text.TrimEnd('\0'));
                }
                result = 1;
                return true;
        }
        return false;
    }

    private void OnTrayCommand(uint cmd)
    {
        switch (cmd)
        {
            case Tray.CmdCalm: SetCalm(!Cfg.Calm); break;
            case Tray.CmdMute: SetMuted(!Cfg.Muted); break;
            case Tray.CmdHome: GoHome(); break;
        }
    }

    public void SetCalm(bool on)
    {
        Cfg.Calm = on;
        Pet.OnCalmChanged();
    }

    public void SetMuted(bool on)
    {
        Cfg.Muted = on;
        OnMuteChanged();
    }

    private partial void OnMuteChanged();
    private partial void DebugCommand(string text);

    /// <summary>Rentrer à la maison, puis quitter.</summary>
    public void GoHome()
    {
        if (FeedingActive) { Berry.Poof(); Pet.OnFeedingCancelled(); }
        Pet.GoHome();
    }

    public void Quit()
    {
        if (quit) return;
        quit = true;
        User32.PostQuitMessage(0);
    }

    public void Shutdown()
    {
        SaveState();
        ShutdownExtras();
        tray.Remove();
        WtsApi32.WTSUnRegisterSessionNotification(Ctrl);
        if (timer != 0) Kernel32.CloseHandle(timer);
        User32.DestroyWindow(PetWin.Hwnd);
        User32.DestroyWindow(FxWin.Hwnd);
        User32.DestroyWindow(Ctrl);
    }

    private partial void ShutdownExtras();
}

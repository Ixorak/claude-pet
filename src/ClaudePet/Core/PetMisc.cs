using System.Globalization;
using ClaudePet.Util;

namespace ClaudePet.Core;

internal sealed partial class Pet
{
    /// <summary>Les écrans ont changé (branchement, résolution, barre des tâches...).</summary>
    public void OnScreensChanged()
    {
        Scr = Desk.At(X, Y - HH * 0.5) ?? Desk.Nearest(X, Y - HH * 0.5);
        Scale = TargetScale(Scr);
        if (!Physical) return;
        if (!FreeAt(X, Y))
        {
            Resolve();
            if (!Supported()) StartFall(0, 0);
        }
    }

    public void OnCalmChanged()
    {
        if (State == St.Walk && !running) speed = app.Cfg.Calm ? BodyW * 0.25 : BodyW * 0.55;
    }

    public void Debug_Throw(double vx, double vy)
    {
        if (State == St.Hidden) return;
        StartFall(vx * K, vy * K, AirKind.Thrown);
    }

    public void DebugAction(string what)
    {
        double now = Clock.Now;
        switch (what)
        {
            case "tap": Tap(now); break;
            case "angry": StartAngry(now); break;
            case "grab": Grab(false); break;
            case "grabcalm": Grab(true); break;
            case "wake": WakeUp(); break;
            case "swingtest": SwingTest(now); break;
            case "swinglow": SwingLowTest(now); break;
            case "reachtest": ReachTest(now); break;
            case "pettest": PetTest(now); break;
            case "sittest": SitTest(now); break;
            case "climbwin": Log.Write("Escalade d'une fenêtre : " + TryClimbWindow()); break;
            case "climb": if (State != St.Hidden) Log.Write("Grimper : " + GoClimb()); break;
            case "jumpwin": if (State != St.Hidden) Log.Write("Saut vers une fenêtre : " + TryJumpToWindow()); break;
            case "hopdown": Log.Write("Redescendre : " + TryHopDown()); break;
            case "tunnel": Log.Write("Tunnel : " + StartTunnel()); break;
            case "coffee": if (CanStartActivity()) StartCoffee(); break;
            case "laptop": if (CanStartActivity()) StartLaptop(); break;
            case "music": if (CanStartActivity()) StartMusic(); break;
            case "read": if (CanStartActivity()) StartRead(); break;
            case "bubbles": if (CanStartActivity()) StartBubbles(); break;
            case "happy": app.SetLonely(0); Log.Write("Humeur : " + MoodName); break;
            case "meh": app.SetLonely(30 * 60); Log.Write("Humeur : " + MoodName); break;
            case "sad": app.SetLonely(3 * 3600); Log.Write("Humeur : " + MoodName); break;
            case "climbpage":
                FloorRange(BodyW * 12, out double minX, out double maxX);
                Log.Write("Escalade d'une page : " + (State is St.Idle or St.Sit or St.Look or St.Walk && GoClimbPage(minX, maxX)));
                break;
        }
    }

    /// <summary>Outil de test : il est au sol, libre de commencer une occupation.</summary>
    private bool CanStartActivity() => (State is St.Idle or St.Sit or St.Look or St.Walk || BusyWithActivity) && Supported();

    /// <summary>
    /// Outil de test du balancement : un pointeur imaginaire (la vraie souris ne bouge pas) attrapé au-dessus de Pet,
    /// qui fait un grand pas à droite, un à gauche, puis de petits allers-retours ; à la fin, Pet est lâché.
    /// </summary>
    private void SwingTest(double now0)
    {
        if (!Physical || State == St.Air) return;
        // assez haut pour qu'il se balance librement au-dessus du sol
        double sx = X, sy = Math.Max(Scr.Work.Top + HH, Scr.Floor - HH * 2.8), step = BodyW * 2;
        app.Cursor.FakeUntil = now0 + 8;
        app.Cursor.Fake = t =>
        {
            double u = t - now0;
            double x = sx;
            if (u < 0.8) x = sx;
            else if (u < 1.15) x = sx + step * MathX.SmoothStep((u - 0.8) / 0.35);
            else if (u < 3.0) x = sx + step;
            else if (u < 3.3) x = sx + step - step * MathX.SmoothStep((u - 3.0) / 0.3);
            else if (u < 5.0) x = sx;
            else if (u < 6.6) x = sx + Math.Sin((u - 5.0) * 2 * Math.PI / 1.4) * BodyW * 0.3;
            return (x, sy);
        };
        app.Cursor.Update(now0);
        Grab(false);
        hangUntil = now0 + 60;
        swingTestEnd = now0 + 7.6;
        Log.Write("Test du balancement (pointeur imaginaire, 7,6 s)");
    }

    /// <summary>Test : pendu à un pointeur imaginaire près du bas de l'écran, qui descend encore (il doit lâcher).</summary>
    private void SwingLowTest(double now0)
    {
        if (!Physical || State == St.Air) return;
        double floor = Scr.Floor, sx = X;
        app.Cursor.FakeUntil = now0 + 6;
        app.Cursor.Fake = t =>
        {
            double u = t - now0;
            double y = u < 2.5 ? floor - HH * 1.25 : floor - HH * 1.25 + (HH * 0.6) * MathX.SmoothStep((u - 2.5) / 1.5);
            return (sx, y);
        };
        app.Cursor.Update(now0);
        Grab(false);
        hangUntil = now0 + 60;
        swingTestEnd = now0 + 5.8;
        Log.Write("Test : pendu près du bas de l'écran (pointeur imaginaire)");
    }

    /// <summary>Test : un pointeur imaginaire au-dessus de Pet, qui passe de droite à gauche : ses bras le visent.</summary>
    private void ReachTest(double now0)
    {
        if (!Physical || State == St.Air) return;
        double sx = X, sy = Y;
        // (instant, écart en largeurs de corps, hauteur au-dessus des pieds en hauteurs de Pet) : au-dessus de sa tête,
        // d'un côté à l'autre, puis plus bas sur chaque côté
        (double t, double dx, double up)[] keys =
        {
            (0.0, 1.0, 2.3), (0.8, 1.0, 2.3), (2.3, 0.0, 1.5), (3.8, -1.0, 2.3),
            (4.6, -2.0, 1.0), (5.6, -2.0, 1.0), (6.6, 0.0, 1.6), (7.6, 2.0, 1.0),
        };
        app.Cursor.FakeUntil = now0 + 9;
        app.Cursor.Fake = t =>
        {
            double u = t - now0;
            int i = 0;
            while (i < keys.Length - 1 && u > keys[i + 1].t) i++;
            var a = keys[i];
            var b = keys[Math.Min(i + 1, keys.Length - 1)];
            double k = b.t > a.t ? MathX.SmoothStep(MathX.Clamp((u - a.t) / (b.t - a.t), 0, 1)) : 0;
            return (sx + BodyW * MathX.Lerp(a.dx, b.dx, k), sy - HH * MathX.Lerp(a.up, b.up, k));
        };
        app.Cursor.Update(now0);
        StartReachUp(now0);
        reachJumpAt = now0 + 100;
        Log.Write("Test : bras levés vers un pointeur imaginaire");
    }

    /// <summary>Test : assis 6 s sur un pointeur imaginaire immobile, au-dessus de lui, puis il saute.</summary>
    private void SitTest(double now0)
    {
        if (!Physical || State == St.Air) return;
        double sx = X, sy = Y - HH * 2.5;
        app.Cursor.FakeUntil = now0 + 6.5;
        app.Cursor.Fake = t => (sx, sy);
        app.Cursor.Update(now0);
        LeaveSurface();
        VX = 0; VY = 0;
        X = sx; Y = sy + SitOnCursorOffset;
        sitUntil = now0 + 6;
        sitNextHum = now0 + 1.0;
        Enter(St.SitCursor);
        Log.Write("Test : assis sur un pointeur imaginaire");
    }

    /// <summary>Test : un pointeur imaginaire caresse Pet pendant 4 s.</summary>
    private void PetTest(double now0)
    {
        double cx = X, cy = Y - HH * 0.6;
        app.Cursor.FakeUntil = now0 + 4.2;
        app.Cursor.Fake = t => (cx + Math.Sin((t - now0) * 2 * Math.PI / 0.55) * HW * 0.7, cy);
        app.Cursor.Update(now0);
        if (StartPetting(now0)) { petTestUntil = now0 + 4; Log.Write("Test : caresses (pointeur imaginaire)"); }
    }

    public void Debug_Goto(double x, double y)
    {
        X = x; Y = y;
        StartFall(0, 0);
    }

    public string Describe() => string.Create(CultureInfo.InvariantCulture,
        $"état={State} t={T:F2} X={X:F1} Y={Y:F1} VX={VX:F0} VY={VY:F0} échelle={Scale:F2} écran={Scr} face={Facing} humeur={MoodName}");
}

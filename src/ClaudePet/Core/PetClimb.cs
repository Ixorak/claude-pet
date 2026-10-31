using ClaudePet.Art;
using ClaudePet.Util;

namespace ClaudePet.Core;

// Étape 5 : grimper aux bords de l'écran, sauter de fenêtre en fenêtre, redescendre, creuser un tunnel.
internal sealed partial class Pet
{
    // Escalade
    private int climbSide, climbMode;     // mode : 0 monte, 1 accroché, 2 redescend
    private double climbTargetY, climbDist;
    private bool wantClimb;
    private bool climbForWindow;          // il monte à la hauteur d'une fenêtre pour sauter dessus
    private Platform climbWindow;

    // Tunnel sous un écran en plein écran
    private bool tunnelWalk;
    private Screen? tunnelTarget;
    private int tunnelDir;

    // ------------------------------------------------------------------ grimper

    /// <summary>Un mur (bord d'écran ou zone morte) juste devant : il commence à grimper.</summary>
    private bool TryStartClimb()
    {
        if (app.Cfg.Calm || app.Hungry || app.FeedingActive || homeBound) return false;
        int side = Facing;
        if (FreeAt(X + side * 3, Y)) return false;                // pas de mur
        double room = Y - (Scr.Work.Top + HH + 8);
        if (room < HH * 1.5) return false;                         // pas la place de monter
        climbSide = side;
        climbMode = 0;
        climbDist = 0;
        climbTargetY = Y - Rng.Range(HH * 1.2, Math.Min(room, HH * 9));
        // une fenêtre près de ce mur, plus haut ? il monte jusqu'à sa hauteur pour sauter dessus
        climbForWindow = false;
        if (Rng.Chance(0.75) && FindWindowNearWall(side, out var p))
        {
            climbForWindow = true;
            climbWindow = p;
            climbTargetY = Math.Max(p.Y + HH * 0.25, Scr.Work.Top + HH + 8);
        }
        LeaveSurface();
        VX = 0; VY = 0;
        app.Play(Snd.Grab, 0.35);
        Enter(St.Climb);
        return true;
    }

    private void UpdateClimb(double dt, double now)
    {
        Facing = climbSide;
        if (FreeAt(X + climbSide * 3, Y))
        {
            // le mur s'arrête (le haut d'une marche entre deux écrans) : il se hisse dessus
            StartFall(climbSide * BodyW * 0.8, -Math.Sqrt(2 * G * HH * 0.45), AirKind.Jump);
            return;
        }
        double sp = BodyW * 0.55;
        switch (climbMode)
        {
            case 0: // monte
            {
                double ny = Y - sp * dt;
                if (ny <= climbTargetY || !FreeAt(X, ny))
                {
                    climbMode = 1;
                    T = 0;
                    Dur = Rng.Range(0.5, 1.6);
                    break;
                }
                double prev = climbDist;
                climbDist += Y - ny;
                Y = ny;
                double stepLen = BodyW * 0.36;
                if ((int)(climbDist / stepLen) != (int)(prev / stepLen)) app.Play(Snd.Step, 0.2);
                break;
            }
            case 1: // accroché, il regarde en bas
                if (T >= Dur)
                {
                    if (climbForWindow)
                    {
                        climbForWindow = false;
                        Facing = -climbSide;
                        app.ScanWindowsNow();
                        foreach (var q in Wins.Platforms)
                            if (q.Hwnd == climbWindow.Hwnd && Math.Abs(q.Y - climbWindow.Y) < 40 && q.X1 - q.X0 >= HW * 1.8)
                            {
                                double lo = q.X0 + HW * 0.8, hi = q.X1 - HW * 0.8;
                                double landX = Math.Clamp(X - climbSide * BodyW * Rng.Range(1.5, 3.5), lo, hi);
                                if (JumpOnto(q, landX, miss: Rng.Chance(1.0 / 6), tooFar: false)) return;
                            }
                    }
                    double r = Rng.F();
                    if (r < 0.45)
                    {
                        // saute du mur, vers l'intérieur de l'écran
                        Facing = -climbSide;
                        app.Play(Snd.Jump, 0.6);
                        StartFall(-climbSide * BodyW * Rng.Range(1.0, 2.2), -Math.Sqrt(2 * G * HH * Rng.Range(0.3, 0.7)), AirKind.Jump);
                    }
                    else if (r < 0.65)
                    {
                        // lâche prise (sonné à l'arrivée si c'est haut)
                        app.Play(Snd.Fall, 0.4);
                        StartFall(-climbSide * BodyW * 0.25, 0, AirKind.Fall);
                    }
                    else climbMode = 2;
                }
                break;
            case 2: // redescend
            {
                double prev = climbDist;
                double step = sp * 1.3 * dt;
                var c = MoveY(step);
                climbDist += step;
                double stepLen = BodyW * 0.36;
                if ((int)(climbDist / stepLen) != (int)(prev / stepLen)) app.Play(Snd.Step, 0.18);
                if (c == Contact.Floor || c == Contact.Platform)
                {
                    if (c == Contact.Platform) AttachPlatformFromLanding(); else AttachFloor();
                    Facing = -climbSide;
                    Enter(St.Idle, Rng.Range(0.4, 0.9));
                }
                break;
            }
        }
    }

    /// <summary>Part vers le mur le plus proche pour l'escalader.</summary>
    private bool GoClimb()
    {
        if (OnWindow) return false;
        // seulement un mur assez proche (sinon la marche serait interminable)
        FloorRange(BodyW * 12, out double minX, out double maxX);
        double step = Math.Max(4, HW * 0.5);
        bool wallL = !FreeAt(minX - step, Y), wallR = !FreeAt(maxX + step, Y);
        if (!wallL && !wallR) return false;
        int dir = wallL && wallR ? (X - minX < maxX - X ? -1 : 1) : wallL ? -1 : 1;
        double target = dir < 0 ? minX - BodyW : maxX + BodyW;
        WalkTo(target, run: Math.Abs(target - X) > BodyW * 5 || Rng.Chance(0.3));
        wantClimb = true;
        return true;
    }

    private PetPose ClimbPose()
    {
        bool hold = climbMode == 1;
        int f = hold ? 0 : (int)(climbDist / (BodyW * 0.18)) & 1;
        return PetPose.Stand with
        {
            DX = 1,
            L = f == 0 ? Arm.Up : Arm.Reach,
            R = f == 0 ? Arm.Reach : Arm.Up,
            LiftA = hold ? 1 : f == 0 ? 2 : 0,
            LiftB = hold ? 1 : f == 0 ? 0 : 2,
            LookX = hold ? -1 : 1,
            LookY = hold || climbMode == 2 ? 1 : -1,
            Eyes = hold ? Eyes.Open : Eyes.Squeeze,
            Mouth = hold ? Mouth.SmallO : Mouth.None,
        };
    }

    // ------------------------------------------------------------------ sauter sur les fenêtres

    /// <summary>
    /// Saute sur une autre fenêtre proche. Il rate environ une fois sur six, et parfois il tente un saut trop long.
    /// </summary>
    private bool TryJumpToWindow()
    {
        if (app.Cfg.Calm || app.Hungry) return false;
        app.ScanWindowsNow();
        var list = Wins.Platforms;
        if (list.Count == 0) return false;
        bool tooFar = Rng.Chance(0.08);
        double maxDX = BodyW * 7, maxUp = HH * 5.5, maxDown = HH * 12;
        if (tooFar) { maxDX *= 1.7; maxUp *= 1.5; }

        Span<double> weights = list.Count <= 256 ? stackalloc double[list.Count] : new double[list.Count];
        Span<double> targets = list.Count <= 256 ? stackalloc double[list.Count] : new double[list.Count];
        double total = 0;
        for (int i = 0; i < list.Count; i++)
        {
            weights[i] = 0;
            var p = list[i];
            if (p.Hwnd == supportHwnd && Math.Abs(p.Y - Y) < 3) continue;  // la fenêtre où il est déjà
            if (p.X1 - p.X0 < HW * 1.8) continue;                          // trop étroit pour s'y poser
            double lo = p.X0 + HW * 0.8, hi = p.X1 - HW * 0.8;
            double tx = Math.Clamp(X + Rng.Range(-maxDX, maxDX), lo, hi);
            double dx = tx - X, up = Y - p.Y;
            if (Math.Abs(dx) < BodyW * 0.8 && Math.Abs(up) < HH * 0.5) continue;
            if (Math.Abs(dx) > maxDX || up > maxUp || -up > maxDown) continue;
            if (!FreeAt(tx, p.Y)) continue;
            targets[i] = tx;
            weights[i] = 1 / (1 + Math.Abs(dx) / BodyW + Math.Abs(up) / HH);
            total += weights[i];
        }
        if (total <= 0) return false;
        int pick = Rng.Pick(weights);
        return JumpOnto(list[pick], targets[pick], miss: !tooFar && Rng.Chance(1.0 / 6), tooFar);
    }

    /// <summary>
    /// Saut en cloche vers le haut d'une fenêtre (depuis le sol, une fenêtre ou un mur).
    /// <paramref name="miss"/> : il vise un peu court et passe sous le bord ; <paramref name="tooFar"/> : son élan ne suffira pas.
    /// </summary>
    private bool JumpOnto(Platform t, double landX, bool miss, bool tooFar)
    {
        if (miss) landX = landX > X ? t.X0 - HW * Rng.Range(0.7, 1.6) : t.X1 + HW * Rng.Range(0.7, 1.6);
        double topLimit = Math.Max(Scr.Work.Top, (Desk.At(landX, t.Y - 1) ?? Scr).Work.Top) + HH + 4;
        double apexY = Math.Min(Y, t.Y) - HH * Rng.Range(0.45, 0.9);
        if (apexY < topLimit) apexY = topLimit;
        if (apexY > Math.Min(Y, t.Y) - 8) return false; // pas la place de passer sous le plafond
        double rise = Y - apexY;
        if (tooFar) rise = Math.Min(rise, HH * 4.2);
        double vy0 = -Math.Sqrt(2 * G * rise);
        double t1 = -vy0 / G;
        double t2 = Math.Sqrt(2 * Math.Max(1, t.Y - (Y - rise)) / G);
        double vx = (landX - X) / (t1 + t2);
        if (tooFar) vx = MathX.Clamp(vx, -BodyW * 5, BodyW * 5);
        Log.Write($"Saut vers une fenêtre ({t.X0:F0}-{t.X1:F0} à y={t.Y:F0}){(miss ? " : raté exprès" : "")}{(tooFar ? " : trop loin" : "")}");
        if (State == St.Climb)
        {
            // depuis le mur : il s'élance tout de suite
            if (Math.Abs(vx) > 1) Facing = Math.Sign(vx);
            app.Play(Snd.Jump, 0.7);
            StartFall(vx, vy0, AirKind.Jump);
        }
        else PrepareJump(vx, vy0, AirKind.Jump, 0.14);
        windowJump = true;
        windowJumpY = t.Y;
        return true;
    }

    /// <summary>Une fenêtre près du mur, plus haut que Pet, où il peut sauter depuis le mur.</summary>
    private bool FindWindowNearWall(int side, out Platform best)
    {
        best = default;
        app.ScanWindowsNow();
        double wallX = X + side * HW, maxUp = Y - (Scr.Work.Top + HH + 8);
        double bestScore = double.MaxValue;
        foreach (var p in Wins.Platforms)
        {
            if (p.X1 - p.X0 < HW * 1.8) continue;
            if (side > 0 ? p.X1 > wallX + 2 : p.X0 < wallX - 2) continue; // de l'autre côté du mur
            double gap = side > 0 ? wallX - p.X1 : p.X0 - wallX;           // du mur au bout le plus proche
            if (gap > BodyW * 6) continue;
            double up = Y - p.Y;
            if (up < HH * 1.5 || up > maxUp) continue;
            double score = up + Math.Max(0, gap);
            if (score < bestScore) { bestScore = score; best = p; }
        }
        return bestScore < double.MaxValue;
    }

    /// <summary>Redescend du haut d'une fenêtre : un petit saut vers le sol.</summary>
    private bool TryHopDown()
    {
        if (!OnWindow) return false;
        CurrentSegment(out double x0, out double x1);
        int dir = X - x0 < x1 - X ? -1 : 1;
        if (Rng.Chance(0.3)) dir = -dir;
        double edge = dir < 0 ? x0 : x1;
        double tx = edge + dir * BodyW * Rng.Range(0.8, 2.0);
        var s = Desk.At(tx, Y) ?? Scr;
        if (s.Fullscreen) return false;
        tx = Math.Clamp(tx, s.Work.Left + HW + 2, s.Work.Right - HW - 2);
        double ty = s.Floor;
        double rise = HH * Rng.Range(0.3, 0.6);
        double vy0 = -Math.Sqrt(2 * G * rise);
        double t1 = -vy0 / G;
        double t2 = Math.Sqrt(2 * Math.Max(1, ty - (Y - rise)) / G);
        double vx = (tx - X) / (t1 + t2);
        PrepareJump(vx, vy0, AirKind.Jump, 0.12);
        return true;
    }

    // ------------------------------------------------------------------ tunnel

    /// <summary>Un écran libre qu'on ne peut pas rejoindre à pied (un écran en plein écran est entre les deux).</summary>
    private Screen? TunnelCandidate(out int dir)
    {
        dir = 0;
        if (Scr.Fullscreen || OnWindow) return null;
        var list = Desk.Screens;
        int i = list.IndexOf(Scr);
        if (i < 0) return null;
        Screen? right = null, left = null;
        for (int j = i + 1; j < list.Count; j++)
            if (!list[j].Fullscreen) { if (j > i + 1) right = list[j]; break; }
        for (int j = i - 1; j >= 0; j--)
            if (!list[j].Fullscreen) { if (j < i - 1) left = list[j]; break; }
        if (right != null && (left == null || Rng.Chance(0.5))) { dir = 1; return right; }
        if (left != null) { dir = -1; return left; }
        return null;
    }

    private bool StartTunnel()
    {
        var target = TunnelCandidate(out int dir);
        if (target == null) return false;
        var w = Scr.Work;
        double edge = dir > 0 ? w.Right - HW - 1 : w.Left + HW + 1;
        tunnelTarget = target;
        tunnelDir = dir;
        tunnelWalk = true;
        WalkTo(edge + dir * BodyW, run: Math.Abs(edge - X) > BodyW * 5 || Rng.Chance(0.5));
        Log.Write($"Tunnel : Pet va creuser sous l'écran en plein écran vers l'écran {target.Index}");
        return true;
    }

    /// <summary>Arrivé au bord de l'écran (ou contre le mur) pour le tunnel : il creuse.</summary>
    private bool TunnelArrived()
    {
        if (!tunnelWalk) return false;
        tunnelWalk = false;
        var target = tunnelTarget;
        tunnelTarget = null;
        if (target == null || target.Fullscreen) return false;
        emergeScreen = target;
        emergeX = tunnelDir > 0 ? target.Work.Left + BodyW * 1.5 : target.Work.Right - BodyW * 1.5;
        Facing = tunnelDir;
        StartDig(quit: false);
        return true;
    }
}

using ClaudePet.Art;
using ClaudePet.Native;
using ClaudePet.Util;

namespace ClaudePet.Core;

// Pet et la souris : taper, double-clic, énervé, s'accrocher au pointeur, mode calme, dormir.
internal sealed partial class Pet
{
    public const double DoubleClickTime = 0.25;

    // Clics
    private bool clickPending;
    private double clickPendingAt, lastClickUp = -10;
    private readonly List<double> taps = new();

    // Énervé
    private double angryStart = -100, angryUntil = -100;
    private bool wasAngry;
    private double nextSmoke, nextGrumble;
    public bool Angry => Clock.Now < angryUntil;

    // Pointeur
    private double grabCooldown;
    private bool cursorWasInZone;
    private bool hangCalm;
    private double hangGrabX, hangGrabY, hangUntil, sitUntil, attachedAt;

    // Balancement au bout du pointeur : un pendule. Le centre du corps (« bob ») est une masse au bout d'un fil
    // rigide accroché à la main qui tient le pointeur ; le dessin tourne autour de la main.
    /// <summary>Angle du dessin (corps vers la droite = positif, en radians) ; 0 = droit.</summary>
    public double RenderAngle;
    /// <summary>Pivot de la rotation du dessin (le bout du pointeur), en pixels écran.</summary>
    public double PivotX, PivotY;
    private double bobX, bobY, bobVX, bobVY, pivPX, pivPY;
    private double bobL, bobRest, climbStartAngle, swingTestEnd;
    private const double MaxSwing = 1.35; // environ 77° : jamais au-dessus de la main

    /// <summary>Vitesse de balancement (radians par seconde).</summary>
    private double SwingSpeed => MathX.Hypot(bobVX, bobVY) / Math.Max(1, bobL);

    // Sommeil
    private bool manualSleep;   // bulle « Dormir » : jusqu'à ce qu'Ixorak clique
    private bool idleSleep;     // Ixorak ne touche plus au PC : jusqu'à son retour
    private double nextZ, nextSnore, nextIdleCheck;
    private int zVariant;

    /// <summary>Les bulles d'options sont ouvertes : Pet reste tranquille et regarde Ixorak.</summary>
    public bool Attentive;

    public void PrepareForBubbles()
    {
        clickPending = false;
        if (State is St.Walk or St.Look or St.Stretch or St.Sit or St.Stunned or St.Tapped or St.GetUp or St.WakeUp or St.ReachUp or St.Land)
        {
            VX = 0;
            Enter(St.Idle, 1);
        }
    }

    // ------------------------------------------------------------------ clics

    private bool TryDoubleClick(double now)
    {
        if (!clickPending || now - lastClickUp > DoubleClickTime) return false;
        clickPending = false;
        pressing = false;
        app.OpenBubbles();
        return true;
    }

    private partial void OnClick(double cx, double cy, double now)
    {
        clickPending = true;
        lastClickUp = now;
        clickPendingAt = now + DoubleClickTime;
    }

    private void UpdateClicks(double now)
    {
        if (clickPending && now >= clickPendingAt)
        {
            clickPending = false;
            Tap(now);
        }
    }

    /// <summary>Clic simple : petit bruit de tape, Pet est sonné. Tapé souvent et vite : il s'énerve.</summary>
    private void Tap(double now)
    {
        if (State == St.Sleep) { WakeUp(); return; }
        if (!Physical && State != St.Hidden) return;
        app.Play(Snd.Tap, 0.75);
        taps.Add(now);
        taps.RemoveAll(t => now - t > 4.0);
        if (!Angry && taps.Count >= 5)
        {
            StartAngry(now);
            return;
        }
        if (State == St.Air) { VY = Math.Min(VY, 0) - 250 * K; return; }
        LeaveSurfaceIfNeeded();
        Enter(St.Tapped, Angry ? 0.5 : 1.15);
        tapStarsPlayed = false;
    }

    private bool tapStarsPlayed;

    private void LeaveSurfaceIfNeeded() { }

    private void UpdateTapped(double dt)
    {
        if (!CheckSupport()) return;
        if (T > 0.12 && !tapStarsPlayed)
        {
            tapStarsPlayed = true;
            app.Play(Snd.Stars, 0.45);
        }
        if (T >= Dur) Enter(St.Idle, Rng.Range(0.5, 1.0));
    }

    // ------------------------------------------------------------------ énervé

    private void StartAngry(double now)
    {
        angryStart = now;
        angryUntil = now + 30;
        wasAngry = true;
        taps.Clear();
        nextSmoke = now;
        nextGrumble = now + Rng.Range(4, 6);
        app.Play(Snd.Angry, 0.9);
        ExclaimUntil = now + 0.8;
        ExclaimQuestion = false;
        Log.Write("Pet est énervé !");
        if (Physical && State != St.Air) Enter(St.Idle, 0.4);
    }

    private double AngryRed()
    {
        double now = Clock.Now;
        if (now >= angryUntil) return 0;
        return MathX.Clamp((now - angryStart) / 0.3, 0, 1) * MathX.Clamp((angryUntil - now) / 3.0, 0, 1);
    }

    private void UpdateMood(double dt, double now)
    {
        UpdateClicks(now);
        if (Angry)
        {
            if (State != St.Hidden && State != St.Underground && now >= nextSmoke)
            {
                nextSmoke = now + Rng.Range(0.12, 0.22);
                HeadPoint(out double hx, out double hy);
                int side = Rng.Sign();
                app.Fx.Add(FxKind.Smoke, hx + side * BodyW * Rng.Range(0.15, 0.4), hy - Scale * 2,
                    side * Rng.Range(10, 50) * K, -Rng.Range(70, 140) * K, Rng.Range(0.7, 1.1), 0, drag: 0.5);
            }
            if (now >= nextGrumble)
            {
                nextGrumble = now + Rng.Range(4, 7);
                app.Play(Snd.Angry, 0.45);
            }
        }
        else if (wasAngry)
        {
            wasAngry = false;
            Log.Write("Pet se calme");
            if (State is St.Idle or St.Walk or St.Look or St.Sit) Enter(St.Sit, 3.0);
        }
    }

    /// <summary>Une baie ou des caresses calment Pet tout de suite quand il est énervé : le rouge s'efface vite.</summary>
    private void CalmDown(string why)
    {
        if (!Angry) return;
        double now = Clock.Now;
        angryUntil = now + 0.5;
        angryStart = Math.Min(angryStart, now - 0.3);
        taps.Clear();
        Log.Write("Pet se calme : " + why);
    }

    private void AngryDecide()
    {
        Span<double> w = stackalloc double[4];
        w[0] = 35; w[1] = 38; w[2] = 15; w[3] = 12;
        switch (Rng.Pick(w))
        {
            case 0:
                if (StartWander(true)) speed = BodyW * 3.2;
                else WildJump();
                break;
            case 1: WildJump(); break;
            case 2: Hop(); break;
            default: Enter(St.Look, Rng.Range(0.3, 0.8)); break;
        }
    }

    private void WildJump()
    {
        double h = HH * Rng.Range(1.2, 3.4);
        double vy = -Math.Sqrt(2 * G * h);
        double vx = Rng.Sign() * BodyW * Rng.Range(0.8, 4.5);
        PrepareJump(vx, vy, AirKind.Wild, 0.08);
    }

    // ------------------------------------------------------------------ le pointeur

    private bool FreeToReact => !homeBound && !Attentive && !pressing && !app.FeedingActive
        && State is St.Idle or St.Walk or St.Sit or St.Look or St.Stretch;

    private static bool MouseButtonDown()
        => (User32.GetAsyncKeyState(C.VK_LBUTTON) & 0x8000) != 0 || (User32.GetAsyncKeyState(C.VK_RBUTTON) & 0x8000) != 0;

    /// <summary>Réagit au pointeur proche : saute pour l'attraper (normal) ou lève les bras (calme).</summary>
    private bool ReactToCursor(double now)
    {
        if (!FreeToReact) return false;
        if (app.Cfg.Calm && !Angry) return TryReachUp(now);
        return TryGrabCursor(now);
    }

    /// <summary>Le pointeur est au-dessus de Pet, à portée de saut.</summary>
    private bool CursorInGrabZone(out double dx)
    {
        var c = app.Cursor;
        double above = Y - c.Y;
        dx = c.X - X;
        double maxUp = Angry ? 4.5 : 3.5;
        return above > HH * 0.8 && above < HH * maxUp && Math.Abs(dx) < BodyW * (Angry ? 4.5 : 2.6)
               && Desk.At(c.X, c.Y) is Screen s && !s.Fullscreen;
    }

    private bool TryGrabCursor(double now)
    {
        if (!CursorInGrabZone(out _)) { cursorWasInZone = false; return false; }
        if (cursorWasInZone || now < grabCooldown || MouseButtonDown()) return false;
        cursorWasInZone = true;
        if ((Angry || Rng.Chance(0.45)) && CanHangFrom(app.Cursor.X, app.Cursor.Y)) { GrabJump(now); return true; }
        // sinon il lève d'abord les bras vers le pointeur, et saute l'attraper s'il reste là
        StartReachUp(now);
        return true;
    }

    private double reachJumpAt;
    /// <summary>Bras gauche / droit (dans le dessin) qui passe devant le corps vers le pointeur placé sur le côté.</summary>
    private bool aimCrossL, aimCrossR;

    private void StartReachUp(double now)
    {
        reachJumpAt = now + Rng.Range(1.0, 2.6);
        aimCrossL = aimCrossR = false;
        Enter(St.ReachUp);
        app.Play(Snd.Happy, 0.25);
    }

    /// <summary>Écart entre les pieds et le haut de la main qui tient le pointeur.</summary>
    private void HoldOffset(out double ox, out double oy)
    {
        var p = HangBasePose;
        var (hx, hy) = PetPainter.HoldHand(p);
        ox = Facing * (hx + 0.5) * RenderScale;
        oy = (hy + 0.5) * RenderScale;
    }

    private static PetPose HangBasePose => PetPose.Stand with { Legs = Legs.Dangle, L = Arm.Hold };

    private void GrabJump(double now)
    {
        var c = app.Cursor;
        if (Math.Abs(c.X - X) > BodyW * 0.2) Facing = Math.Sign(c.X - X);
        ExclaimUntil = now + 0.45;
        ExclaimQuestion = false;
        HoldOffset(out double ox, out double oy);
        var (vx, vy) = c.Velocity(now);
        double tx = c.X + vx * 0.12 - ox, ty = c.Y + vy * 0.08 - oy;
        double rise = Math.Max(HH * 0.3, Y - ty + HH * 0.08);
        double vy0 = -Math.Sqrt(2 * G * rise);
        double tUp = -vy0 / G;
        double jvx = MathX.Clamp((tx - X) / tUp, -BodyW * 5, BodyW * 5);
        grabCooldown = now + Rng.Range(6, 12);
        PrepareJump(jvx, vy0, AirKind.Grab, 0.1);
    }

    private void CheckGrabInAir()
    {
        if (MouseButtonDown()) return;
        var c = app.Cursor;
        if (!CanHangFrom(c.X, c.Y)) return;
        HoldOffset(out double ox, out double oy);
        double hx = X + ox, hy = Y + oy;
        if (MathX.Hypot(c.X - hx, c.Y - hy) < BodyW * 0.6) Grab(false);
    }

    private void Grab(bool calm)
    {
        double now = Clock.Now;
        app.Play(Snd.Grab, 0.6);
        LeaveSurface();
        hangCalm = calm;
        hangGrabX = app.Cursor.X;
        hangGrabY = app.Cursor.Y;
        attachedAt = now;
        hangUntil = now + (Angry ? Rng.Range(1.5, 3.0) : Rng.Range(2.5, 5.0));
        double jumpVX = VX;
        VX = 0; VY = 0;
        Enter(St.HangCursor);
        FollowHang();
        InitSwing(jumpVX);
    }

    /// <summary>Longueur du fil (de la main au centre du corps) et angle du centre dans le dessin droit.</summary>
    private void HangGeometry(out double len, out double rest)
    {
        HoldOffset(out double ox, out double oy);
        double cx = -ox, cy = -HH * 0.45 - oy;      // de la main au centre du corps, dans le dessin droit
        len = Math.Max(1, MathX.Hypot(cx, cy));
        rest = Math.Atan2(cx, cy);                  // le dessin a la main un peu sur le côté
    }

    /// <summary>De combien le dessin tourné descend sous le centre du corps, pour un angle de fil donné.</summary>
    private double LowestBelowBob(double phi, double rest)
    {
        double th = phi - rest, c = Math.Cos(th);
        return HW * Math.Abs(Math.Sin(th)) + Math.Max(0.45 * HH * c, -0.55 * HH * c);
    }

    /// <summary>Le bas de l'écran (ou le haut de la barre des tâches) sous un point.</summary>
    private double FloorBelow(double x, double y) => (Desk.At(x, y) ?? Desk.Nearest(x, y)).Work.Bottom;

    /// <summary>Angle de fil le plus proche de <paramref name="from"/> où le corps reste au-dessus du sol (NaN si aucun).</summary>
    private double FitAboveFloor(double px, double py, double from, double len, double rest)
    {
        double floor = FloorBelow(px, py) - 2 * RenderScale; // un peu de marge sous ses pieds
        bool Fits(double a) => py + Math.Cos(a) * len + LowestBelowBob(a, rest) <= floor;
        if (Fits(from)) return from;
        int first = Math.Abs(from) > 0.05 ? Math.Sign(from) : (rest >= 0 ? 1 : -1);
        foreach (int side in new[] { first, -first })
            for (double m = side == first ? Math.Abs(from) : 0; m <= MaxSwing + 1e-9; m += 0.02)
                if (Fits(side * m)) return side * m;
        return double.NaN;
    }

    /// <summary>Peut-il se pendre à ce point sans que son corps sorte de l'écran par le bas (au besoin de travers) ?</summary>
    private bool CanHangFrom(double x, double y)
    {
        HangGeometry(out double len, out double rest);
        return !double.IsNaN(FitAboveFloor(x, y, 0, len, rest));
    }

    /// <summary>Le pendule au moment où il attrape le pointeur : il garde un peu l'élan de son saut.</summary>
    private void InitSwing(double jumpVX)
    {
        HangGeometry(out bobL, out bobRest);
        var c = app.Cursor;
        double a = MathX.Clamp(jumpVX / (3000 * K), -0.5, 0.5);
        bobX = c.X + Math.Sin(a) * bobL;
        bobY = c.Y + Math.Cos(a) * bobL;
        bobVX = jumpVX * 0.6;
        bobVY = 0;
        pivPX = c.X; pivPY = c.Y;
        UpdateSwingAngle();
    }

    /// <summary>
    /// Fait avancer le pendule : gravité, léger amortissement, fil rigide jusqu'à la main (qui suit le pointeur).
    /// Quand la souris accélère, le corps prend du retard et se balance. Le bas de l'écran repousse le corps sur le
    /// côté (il s'accroche de travers). Renvoie faux si le pointeur est trop bas pour qu'il tienne, même de travers.
    /// </summary>
    private bool StepSwing(double dt)
    {
        var c = app.Cursor;
        double px1 = c.X, py1 = c.Y;
        int n = Math.Clamp((int)Math.Ceiling(dt * 240), 1, 16);
        double h = dt / n;
        double damp = Math.Exp(-0.9 * h);
        for (int i = 1; i <= n; i++)
        {
            double t = (double)i / n;
            double px = pivPX + (px1 - pivPX) * t, py = pivPY + (py1 - pivPY) * t;
            double ox = bobX, oy = bobY;
            bobVX *= damp;
            bobVY = bobVY * damp + G * h;
            bobX += bobVX * h;
            bobY += bobVY * h;
            double dx = bobX - px, dy = bobY - py, d = Math.Max(1e-3, MathX.Hypot(dx, dy));
            bobX = px + dx / d * bobL;
            bobY = py + dy / d * bobL;
            double a = Math.Atan2(bobX - px, bobY - py);
            if (Math.Abs(a) > MaxSwing)
            {
                a = Math.Sign(a) * MaxSwing;
                bobX = px + Math.Sin(a) * bobL;
                bobY = py + Math.Cos(a) * bobL;
            }
            // le bas de l'écran : son corps ne passe pas dessous, il est poussé sur le côté
            double fit = FitAboveFloor(px, py, a, bobL, bobRest);
            if (double.IsNaN(fit)) { pivPX = px; pivPY = py; return false; }
            bool pushed = fit != a;
            if (pushed)
            {
                bobX = px + Math.Sin(fit) * bobL;
                bobY = py + Math.Cos(fit) * bobL;
            }
            bobVX = (bobX - ox) / h;
            bobVY = (bobY - oy) / h;
            if (pushed) { bobVX *= 0.6; bobVY *= 0.6; }
        }
        pivPX = px1; pivPY = py1;
        UpdateSwingAngle();
        return true;
    }

    private void UpdateSwingAngle()
    {
        PivotX = pivPX;
        PivotY = pivPY;
        RenderAngle = Math.Atan2(bobX - pivPX, bobY - pivPY) - bobRest;
    }

    /// <summary>Les pieds là où le dessin tourné les montre (au moment de lâcher le pointeur).</summary>
    private void ApplyRotationToFeet()
    {
        if (RenderAngle == 0) return;
        double c = Math.Cos(RenderAngle), s = Math.Sin(RenderAngle);
        double dx = X - PivotX, dy = Y - PivotY;
        X = PivotX + c * dx + s * dy;
        Y = PivotY - s * dx + c * dy;
        RenderAngle = 0;
    }

    private void FollowHang()
    {
        HoldOffset(out double ox, out double oy);
        X = app.Cursor.X - ox;
        Y = app.Cursor.Y - oy;
    }

    private bool CursorOnFullscreen() => Desk.At(app.Cursor.X, app.Cursor.Y) is Screen s && s.Fullscreen;

    private bool Shaken(double now) => now - attachedAt > 0.35 && app.Cursor.Reversals(now, 0.7, 1000 * K) >= 3;

    private void DropFromCursor(bool fling, double now)
    {
        // il part avec l'élan de son balancement (qui contient déjà le mouvement de la souris)
        double vx, vy;
        if (State is St.HangCursor or St.ClimbCursor) { vx = bobVX; vy = bobVY; }
        else (vx, vy) = app.Cursor.Velocity(now);
        double max = 5000 * K, sp = MathX.Hypot(vx, vy);
        if (sp > max) { vx *= max / sp; vy *= max / sp; }
        ApplyRotationToFeet();
        Resolve();
        if (fling) app.Play(Snd.Fall, 0.45);
        double k = fling ? 0.9 : 0.5;
        StartFall(vx * k, vy * k, fling ? AirKind.Thrown : AirKind.Fall);
    }

    private void UpdateHang(double dt, double now)
    {
        var c = app.Cursor;
        if (CursorOnFullscreen()) { DropFromCursor(false, now); return; }
        if (hangCalm)
        {
            if (MathX.Hypot(c.X - hangGrabX, c.Y - hangGrabY) > 6 * K) { DropFromCursor(false, now); return; }
        }
        else if (Shaken(now)) { DropFromCursor(true, now); return; }
        if (swingTestEnd > 0 && now >= swingTestEnd) { swingTestEnd = 0; DropFromCursor(true, now); return; }

        FollowHang();
        if (!StepSwing(dt))
        {
            // le pointeur est trop bas pour qu'il tienne, même de travers : il lâche et retombe debout
            UpdateSwingAngle();
            DropFromCursor(false, now);
            return;
        }
        // il grimpe s'asseoir sur le pointeur une fois calmé ; tant qu'on le balance, il reste pendu (8 s de plus au plus)
        if (!hangCalm && now > hangUntil && (SwingSpeed < 1.2 || now > hangUntil + 8))
        {
            if (Angry) { DropFromCursor(true, now); return; }
            climbStartAngle = RenderAngle;
            Enter(St.ClimbCursor, 0.9);
            app.Play(Snd.Jump, 0.4);
        }
    }

    private void UpdateClimbCursor(double dt, double now)
    {
        var c = app.Cursor;
        if (CursorOnFullscreen()) { DropFromCursor(false, now); return; }
        if (Shaken(now)) { DropFromCursor(true, now); return; }
        HoldOffset(out double ox, out double oy);
        double u = MathX.SmoothStep(T / Dur);
        double hangX = c.X - ox, hangY = c.Y - oy;
        double sitX = c.X, sitY = c.Y + SitOnCursorOffset;
        X = MathX.Lerp(hangX, sitX, u);
        Y = MathX.Lerp(hangY, sitY, u);
        // en grimpant, il se redresse
        RenderAngle = climbStartAngle * (1 - u);
        PivotX = c.X; PivotY = c.Y;
        pivPX = c.X; pivPY = c.Y;
        if (T >= Dur)
        {
            sitUntil = now + Rng.Range(40, 120);
            app.Play(Snd.Happy, 0.4);
            Enter(St.SitCursor);
        }
    }

    private double SitOnCursorOffset => 5.5 * RenderScale;

    private void UpdateSitCursor(double dt, double now)
    {
        var c = app.Cursor;
        if (CursorOnFullscreen()) { DropFromCursor(false, now); return; }
        if (Shaken(now)) { DropFromCursor(true, now); return; }
        X = c.X;
        Y = c.Y + SitOnCursorOffset;
        if (Clock.Now >= nextLookChange)
        {
            nextLookChange = now + Rng.Range(1.0, 3.0);
            lookX = Rng.Int(-1, 2);
            lookY = Rng.Chance(0.3) ? 1 : 0;
        }
        if (now > sitUntil)
        {
            Resolve();
            StartFall(Rng.Sign() * BodyW * 0.8, -Math.Sqrt(2 * G * HH * 0.5), AirKind.Jump);
        }
    }

    // ------------------------------------------------------------------ mode calme

    private bool CursorAboveNear(out double dx)
    {
        var c = app.Cursor;
        double above = Y - c.Y;
        dx = c.X - X;
        return above > HH * 0.5 && above < HH * 3.0 && Math.Abs(dx) < BodyW * 2.2;
    }

    private bool TryReachUp(double now)
    {
        if (!CursorAboveNear(out _)) return false;
        if (MouseButtonDown()) return false;
        StartReachUp(now);
        return true;
    }

    /// <summary>
    /// Bras levés vers le pointeur (en mode calme comme en mode normal). Pointeur immobile contre ses mains :
    /// il s'y accroche. En mode normal, il finit par sauter l'attraper.
    /// </summary>
    private void UpdateReachUp(double dt, double now)
    {
        if (!CheckSupport()) return;
        bool calm = app.Cfg.Calm && !Angry;
        double dx;
        bool near = calm ? CursorAboveNear(out dx) : CursorInGrabZone(out dx);
        if (!near || homeBound || app.FeedingActive || Attentive)
        {
            Enter(St.Idle, Rng.Range(0.5, 1.2));
            return;
        }
        if (Math.Abs(dx) > BodyW * 0.4) Facing = Math.Sign(dx);
        TrackCursorWithEyes(true);
        var c = app.Cursor;
        HeadPoint(out double hx, out double hy);
        double rx = X, ry = hy - HH * 0.3;
        bool still = now - c.LastMoveTime > 0.3;
        bool canHang = CanHangFrom(c.X, c.Y); // pointeur trop bas : il lève les bras sans s'y pendre
        if (canHang && MathX.Hypot(c.X - rx, c.Y - ry) < BodyW * 0.8 && still && !MouseButtonDown()) { Grab(calm); return; }
        if (!calm && canHang && now >= reachJumpAt && now >= grabCooldown && !MouseButtonDown()) GrabJump(now);
    }

    /// <summary>
    /// Les deux bras tendus vers le pointeur, chacun de son côté : un bras ne passe jamais devant le corps ;
    /// quand le pointeur est en haut ou de l'autre côté, il monte le long du flanc.
    /// </summary>
    private void AimArms(int wiggle, out Arm left, out Arm right)
    {
        left = AimArm(false, 7 + wiggle, ref aimCrossL);
        right = AimArm(true, 8 - wiggle, ref aimCrossR);
    }

    /// <summary>
    /// Un bras vers le pointeur. Pointeur au-dessus de lui : le bras se lève de son côté. Pointeur sur le côté :
    /// il le pointe, quitte à passer devant le corps (depuis une épaule plus basse, sous l'œil).
    /// </summary>
    private Arm AimArm(bool right, double len, ref bool crossing)
    {
        var (vx, vy) = AimVector(right, false);
        crossing = PetPainter.AimCrosses(vx, vy, right, crossing);
        if (crossing) (vx, vy) = AimVector(right, true);
        return PetPainter.AimArm(vx, vy, len, right, crossing);
    }

    /// <summary>Du point d'épaule au pointeur, dans le dessin (retourné si Pet regarde à gauche).</summary>
    private (double vx, double vy) AimVector(bool right, bool cross)
    {
        var c = app.Cursor;
        var (lx, ly) = PetPainter.AimShoulder(PetPose.Stand, right, cross);   // dans le dessin debout
        double sx = X + Facing * lx * RenderScale, sy = Y + ly * RenderScale;  // à l'écran
        return ((c.X - sx) * Facing, c.Y - sy);
    }

    // ------------------------------------------------------------------ dormir

    /// <summary>Bulle « Dormir » : il dort jusqu'à ce qu'Ixorak clique sur lui.</summary>
    public void SleepUntilClicked()
    {
        if (!Physical || State == St.Air) return;
        StartSleep(0);
        manualSleep = true;
    }

    private void StartSleep(double duration)
    {
        double now = Clock.Now;
        manualSleep = false;
        idleSleep = false;
        nextZ = now + 0.9;
        nextSnore = now + 2.8;
        zVariant = 0;
        app.Play(Snd.Yawn, 0.45);
        Enter(St.Sleep, duration);
    }

    /// <summary>Ixorak ne touche plus au PC depuis une minute : inutile d'animer ce que personne ne voit.</summary>
    private static bool UserAway => SysInfo.UserIdleSeconds > 60;

    private void UpdateSleep(double dt, double now)
    {
        if (!CheckSupport()) return;
        bool away = UserAway;
        if (away) { nextZ = Math.Max(nextZ, now + 0.5); nextSnore = Math.Max(nextSnore, now + 1.0); }
        if (T > 0.7 && now >= nextZ)
        {
            nextZ = now + 1.15;
            HeadPoint(out double hx, out double hy);
            app.Fx.Add(FxKind.Zed, hx + Facing * BodyW * 0.32, hy - Scale * 2, Facing * 22 * K, -42 * K, 2.4, zVariant % 3);
            zVariant++;
        }
        if (T > 1.5 && now >= nextSnore)
        {
            nextSnore = now + Rng.Range(5.0, 7.0);
            app.Play(Snd.Snore, 0.3);
        }
        if (idleSleep && SysInfo.UserIdleSeconds < 1.5) { WakeUp(); return; } // Ixorak est revenu
        if (!manualSleep && !idleSleep && Dur > 0 && T >= Dur) WakeUp();
    }

    /// <summary>Ixorak ne touche plus à rien depuis 3 minutes : Pet s'endort, et se réveille à son retour.</summary>
    private void CheckUserIdle(double now)
    {
        if (now < nextIdleCheck) return;
        nextIdleCheck = now + 1.0;
        if (State is not (St.Idle or St.Sit or St.Look) || Angry || homeBound || Attentive || app.FeedingActive || pressing) return;
        if (SysInfo.UserIdleSeconds < 180) return;
        Log.Write("Ixorak est absent : Pet s'endort");
        StartSleep(0);
        idleSleep = true;
    }

    /// <summary>Petite sieste toute seule.</summary>
    private void Nap() => StartSleep(Rng.Range(25, 70));

    public void WakeUp()
    {
        if (State != St.Sleep) return;
        manualSleep = false;
        idleSleep = false;
        app.Play(Snd.Yawn, 0.5);
        Enter(St.WakeUp, 1.4);
    }

    public bool Sleeping => State == St.Sleep;

    // ------------------------------------------------------------------ poses

    private PetPose MousePose()
    {
        switch (State)
        {
            case St.Tapped:
            {
                if (T < 0.12)
                    return PetPose.Stand with { BodyW = 27, BodyH = 13, BodyBottom = -5, Legs = Legs.Spread, L = Arm.Out, R = Arm.Out, Eyes = Eyes.Squeeze, Mouth = Mouth.Open };
                int f = (int)(T * 6) & 3;
                return PetPose.Sitting with { DX = f == 1 ? 1 : f == 3 ? -1 : 0, Eyes = Eyes.Dizzy, Mouth = Mouth.Frown };
            }
            case St.HangCursor:
            {
                // le balancement est fait par la rotation du dessin autour de la main
                double sp = SwingSpeed;
                bool swinging = sp > 1.6;
                int kick = swinging ? ((int)(T * 8) & 1) : ((int)(T * 2) & 1);
                bool happy = !Angry && T > 0.4;
                return HangBasePose with
                {
                    R = swinging ? Arm.Out : Arm.Raised,
                    Kick = kick,
                    Eyes = happy ? Eyes.Happy : Eyes.Wide,
                    Mouth = swinging && happy ? Mouth.Open : happy ? Mouth.Grin : Mouth.SmallO,
                };
            }
            case St.ClimbCursor:
            {
                int f = (int)(T * 8) & 1;
                return PetPose.Stand with
                {
                    Legs = Legs.Dangle, Kick = f, L = f == 0 ? Arm.Up : Arm.Reach, R = f == 0 ? Arm.Reach : Arm.Up,
                    Eyes = Eyes.Squeeze, Mouth = Mouth.Open,
                };
            }
            case St.SitCursor:
            {
                int f = (int)(T * 1.6) & 1;
                return PetPose.Stand with
                {
                    Legs = Legs.Dangle, Kick = f, L = Arm.Down, R = Arm.Down, LookX = lookX, LookY = lookY,
                    Eyes = Eyes.Open, Mouth = ((int)(T * 0.3) % 3 == 0) ? Mouth.Smile : Mouth.None,
                };
            }
            case St.ReachUp:
            {
                // les bras pointent vers le pointeur, et s'agitent un peu ; il piétine d'impatience
                int f = (int)(T * 3) & 1;
                AimArms(f, out Arm l, out Arm r);
                return PetPose.Stand with
                {
                    L = l, R = r, LookY = lookY, LookX = lookX,
                    Eyes = Eyes.Wide, Mouth = Mouth.Smile, LiftA = f, LiftB = 1 - f,
                };
            }
            case St.Sleep:
            {
                if (T < 0.7) return PetPose.Sitting with { Eyes = Eyes.Closed, Mouth = Mouth.Yawn, L = Arm.Raised, R = Arm.Raised };
                bool inhale = ((int)(T / 1.6) & 1) == 0;
                return PetPose.Sitting with
                {
                    BodyW = inhale ? 24 : 25, BodyH = inhale ? 16 : 15, Eyes = Eyes.Closed, LookY = 1,
                    Mouth = inhale ? Mouth.None : Mouth.SmallO,
                };
            }
            case St.WakeUp:
            {
                if (T < 0.25) return PetPose.Sitting with { Eyes = Eyes.Half };
                if (T < 1.1) return PetPose.Stand with { BodyW = 22, BodyH = 18, L = Arm.Up, R = Arm.Up, Eyes = Eyes.Closed, Mouth = Mouth.Yawn };
                return PetPose.Stand with { Eyes = Eyes.Half };
            }
        }
        return PetPose.Stand;
    }

    /// <summary>Teinte et visage d'énervement, appliqués par-dessus n'importe quelle pose.</summary>
    private PetPose ApplyMood(PetPose p)
    {
        double red = AngryRed();
        if (red <= 0) return p;
        p.Red = (float)red;
        if (p.Eyes is Eyes.Open or Eyes.Wide or Eyes.Half) p.Eyes = Eyes.Angry;
        p.Brows = true;
        if (p.Mouth is Mouth.None or Mouth.Smile or Mouth.Grin) p.Mouth = Mouth.Frown;
        return p;
    }
}

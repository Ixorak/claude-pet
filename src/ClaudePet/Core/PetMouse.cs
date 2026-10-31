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
    private double swing, swingV;

    // Sommeil
    private bool manualSleep;
    private double nextZ, nextSnore;
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

    private bool TryGrabCursor(double now)
    {
        var c = app.Cursor;
        double above = Y - c.Y, dx = c.X - X;
        double maxUp = Angry ? 4.5 : 3.5;
        bool inZone = above > HH * 0.8 && above < HH * maxUp && Math.Abs(dx) < BodyW * (Angry ? 4.5 : 2.6)
                      && Desk.At(c.X, c.Y) is Screen s && !s.Fullscreen;
        if (!inZone) { cursorWasInZone = false; return false; }
        if (cursorWasInZone || now < grabCooldown || MouseButtonDown()) return false;
        cursorWasInZone = true;
        if (!Angry && !Rng.Chance(0.6)) { grabCooldown = now + 3; return false; }
        GrabJump(now);
        return true;
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
        swing = 0; swingV = 0;
        VX = 0; VY = 0;
        Enter(St.HangCursor);
        FollowHang();
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
        var (vx, vy) = app.Cursor.Velocity(now);
        double max = 5000 * K, sp = MathX.Hypot(vx, vy);
        if (sp > max) { vx *= max / sp; vy *= max / sp; }
        Resolve();
        if (fling) app.Play(Snd.Fall, 0.45);
        StartFall(fling ? vx * 0.8 : 0, fling ? vy * 0.8 : 0, fling ? AirKind.Thrown : AirKind.Fall);
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

        var (vx, _) = c.Velocity(now, 0.08);
        double target = MathX.Clamp(-vx / (1100 * K), -1, 1);
        swingV += ((target - swing) * 70 - swingV * 9) * dt;
        swing = MathX.Clamp(swing + swingV * dt, -1.3, 1.3);
        FollowHang();
        if (!hangCalm && now > hangUntil)
        {
            if (Angry) { DropFromCursor(true, now); return; }
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
        Enter(St.ReachUp);
        app.Play(Snd.Happy, 0.25);
        return true;
    }

    private void UpdateReachUp(double dt, double now)
    {
        if (!CheckSupport()) return;
        if (!app.Cfg.Calm || Angry || !CursorAboveNear(out double dx) || homeBound || app.FeedingActive)
        {
            Enter(St.Idle, Rng.Range(0.5, 1.2));
            return;
        }
        if (Math.Abs(dx) > BodyW * 0.4) Facing = Math.Sign(dx);
        var c = app.Cursor;
        HeadPoint(out double hx, out double hy);
        double rx = X, ry = hy - HH * 0.3;
        bool still = now - c.LastMoveTime > 0.3;
        if (MathX.Hypot(c.X - rx, c.Y - ry) < BodyW * 0.8 && still && !MouseButtonDown()) Grab(true);
    }

    // ------------------------------------------------------------------ dormir

    /// <summary>Bulle « Dormir » : il dort jusqu'à ce qu'Ixorak clique sur lui.</summary>
    public void SleepUntilClicked()
    {
        if (!Physical || State == St.Air) return;
        manualSleep = true;
        StartSleep(0);
    }

    private void StartSleep(double duration)
    {
        double now = Clock.Now;
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
        if (!manualSleep && Dur > 0 && T >= Dur) WakeUp();
    }

    public void WakeUp()
    {
        if (State != St.Sleep) return;
        manualSleep = false;
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
                int d = (int)Math.Round(swing * 2);
                double sp = Math.Abs(swingV);
                int kick = sp > 2 ? ((int)(T * 8) & 1) : ((int)(T * 2) & 1);
                bool happy = !Angry && T > 0.4;
                return HangBasePose with
                {
                    DX = d, L = Arm.Hold.Shift(-d, 0), R = sp > 2 ? Arm.Out : Arm.Raised,
                    Kick = kick, Eyes = happy ? Eyes.Happy : Eyes.Wide, Mouth = happy ? Mouth.Grin : Mouth.SmallO,
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
                int f = (int)(T * 3) & 1;
                return PetPose.Stand with
                {
                    L = f == 0 ? Arm.Reach : Arm.Up, R = f == 0 ? Arm.Up : Arm.Reach, LookY = -1, LookX = 0,
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

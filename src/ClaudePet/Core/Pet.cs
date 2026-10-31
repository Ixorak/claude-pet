using ClaudePet.Art;
using ClaudePet.Util;

namespace ClaudePet.Core;

/// <summary>Ce que fait Pet en ce moment.</summary>
internal enum St : byte
{
    Hidden,      // invisible (pas encore sorti, ou tous les écrans en plein écran)
    Idle,        // debout, entre deux actions
    Walk,        // marche ou court vers une cible
    Sit,         // assis
    Stretch,     // s'étire
    Look,        // regarde autour de lui
    JumpPrep,    // s'accroupit avant de sauter
    Air,         // en l'air (chute, lancer, saut)
    Land,        // atterrit (écrasé, glisse)
    Stunned,     // sonné (étoiles)
    GetUp,       // se relève
    Dragged,     // tenu par la souris
    ExitDoor,    // sort par la porte de la maison (tout petit, puis grandit)
    HomeJump,    // saute vers la porte de la maison en rétrécissant
    EnterDoor,   // rentre dans la porte
    Dig,         // creuse dans le sol
    Underground, // sous terre (invisible), en chemin
    Emerge,      // sort du sol
}

internal enum AirKind : byte { Fall, Thrown, Jump, Hop }

internal enum Contact : byte { None, Floor, Ceiling, Platform }

internal enum Snd : byte
{
    Exit, Enter, Step, Jump, Bounce, Tap, Angry, Stars, Grab, Fall, MouthOpen, Eat, Bubble, Snore, Dig, Emerge,
    Click, Land, Yawn, Happy, Whoosh,
}

/// <summary>Pet : position, physique, états. Le cerveau est dans PetBrain.cs, les poses dans PetPoses.cs.</summary>
internal sealed partial class Pet
{
    private readonly App app;
    private Desk Desk => app.Desk;

    // Position des pieds (milieu, au sol), en pixels écran ; vitesse en px/s.
    public double X, Y, VX, VY;
    public int Facing = 1;
    public St State = St.Hidden;
    public double T;      // temps passé dans l'état
    public double Dur;    // durée prévue de l'état
    public double Scale = 7;    // pixels écran par pixel art
    public double SizeMul = 1;  // facteur de taille (porte de la maison)
    public Screen Scr;

    public double RenderScale => Scale * SizeMul;
    public double HW => PetPainter.HalfWidth * RenderScale;
    public double HH => PetPainter.Height * RenderScale;
    /// <summary>Largeur du corps à la taille normale (sert d'unité pour les vitesses).</summary>
    public double BodyW => 24 * Scale;
    private double K => Scr.Scale;
    private double G => 3600 * K;

    // En l'air
    private AirKind air;
    private double peakY, firstDrop;
    private int bounces;
    private double jumpVX, jumpVY;
    private AirKind jumpKind;

    // Marche
    private double targetX, speed;
    private bool running;
    private double walkDist;

    // Sonné
    private double stunDur;
    private bool stunAfterLand;
    public bool ShowStars;
    public double ExclaimUntil;
    public bool ExclaimQuestion;

    // Regard et clignements
    private double nextBlink, blinkEnd;
    private int lookX, lookY;
    private double nextLookChange;

    // Souris (clic maintenu, glisser)
    private bool pressing, dragging;
    private double pressT, pressX, pressY, grabDX, grabDY;

    private PetPose lastPose = PetPose.Stand;

    public Pet(App app)
    {
        this.app = app;
        Scr = app.Desk.Primary;
        Scale = TargetScale(Scr);
    }

    private double TargetScale(Screen s) => Math.Max(2, Math.Round(app.Cfg.Size * s.Scale));

    // ------------------------------------------------------------------ boucle

    public void Update(double dt, double now)
    {
        UpdateScale(dt);
        T += dt;
        if (pressing && !dragging && now - pressT > 0.2) StartDrag();
        if (homeBound) UpdateHomeBound(now);

        switch (State)
        {
            case St.Hidden: break;
            case St.ExitDoor: UpdateExitDoor(dt); break;
            case St.HomeJump: UpdateHomeJump(dt); break;
            case St.EnterDoor: UpdateEnterDoor(dt); break;
            case St.Dig: UpdateDig(dt); break;
            case St.Underground: UpdateUnderground(dt); break;
            case St.Emerge: UpdateEmerge(dt); break;
            case St.Idle: UpdateIdle(dt); break;
            case St.Walk: UpdateWalk(dt); break;
            case St.Sit:
                if (!CheckSupport()) break;
                TrackCursorWithEyes();
                if (T >= Dur) Enter(St.Idle, Rng.Range(0.3, 1.0));
                break;
            case St.Stretch:
                if (!CheckSupport()) break;
                if (T >= Dur) Enter(St.Idle, Rng.Range(0.4, 1.2));
                break;
            case St.Look:
                if (!CheckSupport()) break;
                UpdateLook();
                if (T >= Dur) Enter(St.Idle, Rng.Range(0.3, 1.0));
                break;
            case St.JumpPrep:
                if (!CheckSupport()) break;
                if (T >= Dur) Launch();
                break;
            case St.Air: UpdateAir(dt); break;
            case St.Land: UpdateLand(dt); break;
            case St.Stunned:
                if (!CheckSupport()) break;
                if (T >= Dur) Enter(St.GetUp, 0.65);
                break;
            case St.GetUp:
                if (!CheckSupport()) break;
                if (T >= Dur) Enter(St.Idle, Rng.Range(0.5, 1.4));
                break;
            case St.Dragged: UpdateDragged(dt, now); break;
        }

        ShowStars = State == St.Stunned;
        UpdateBlink(now);
    }

    /// <summary>Intervalle conseillé avant la prochaine mise à jour (économie de processeur).</summary>
    public double DesiredInterval()
    {
        switch (State)
        {
            case St.Hidden: return 1.0;
            case St.Underground: return 1.0 / 20;
            case St.Idle:
            case St.Sit:
            case St.Look:
                return 1.0 / 20;
            case St.Stunned: return 1.0 / 20;
            default: return 1.0 / 60;
        }
    }

    public void Enter(St s, double dur = 0)
    {
        State = s;
        T = 0;
        Dur = dur;
    }

    // ------------------------------------------------------------------ taille

    private void UpdateScale(double dt)
    {
        if (State == St.Underground) return;
        var s = Desk.At(X, Y - HH * 0.5) ?? Desk.Nearest(X, Y - HH * 0.5);
        Scr = s;
        double target = TargetScale(s);
        if (Math.Abs(Scale - target) < 0.04) Scale = target;
        else
        {
            Scale += (target - Scale) * Math.Min(1, dt * 9);
            if (Physical && !FreeAt(X, Y)) Resolve();
        }
    }

    /// <summary>Taille choisie dans les réglages : on recalcule tout de suite.</summary>
    public void OnSizeChanged()
    {
        Scale = TargetScale(Scr);
        if (Physical) Resolve();
    }

    /// <summary>Vrai quand Pet est soumis aux collisions (pas tenu, pas caché, pas dans une animation de porte ou de terrier).</summary>
    public bool Physical => State switch
    {
        St.Hidden or St.Dragged or St.ExitDoor or St.HomeJump or St.EnterDoor or St.Dig or St.Underground or St.Emerge => false,
        _ => true,
    };

    // ------------------------------------------------------------------ collisions

    private Screen? AllowScreen => Scr.Fullscreen ? Scr : null;

    public bool FreeAt(double x, double y) => Desk.IsFree(x - HW, y - HH, x + HW, y, AllowScreen);

    /// <summary>Déplace horizontalement ; renvoie vrai si un mur a arrêté le mouvement.</summary>
    private bool MoveX(double dx)
    {
        if (dx == 0) return false;
        if (FreeAt(X + dx, Y)) { X += dx; return false; }
        if (!FreeAt(X, Y)) { X += dx; return false; } // déjà coincé : on laisse passer, Resolve s'en chargera
        double lo = 0, hi = 1;
        for (int i = 0; i < 14; i++)
        {
            double m = (lo + hi) * 0.5;
            if (FreeAt(X + dx * m, Y)) lo = m; else hi = m;
        }
        X += dx * lo;
        return true;
    }

    /// <summary>Déplace verticalement : sol, plafond, et (plus tard) le haut des fenêtres.</summary>
    private Contact MoveY(double dy)
    {
        if (dy == 0) return Contact.None;
        double ny = Y + dy;
        if (dy > 0 && PlatformCross(Y, ny, out double top))
        {
            Y = top;
            return Contact.Platform;
        }
        if (FreeAt(X, ny)) { Y = ny; return Contact.None; }
        if (!FreeAt(X, Y)) { Y = ny; return Contact.None; }
        double lo = 0, hi = 1;
        for (int i = 0; i < 16; i++)
        {
            double m = (lo + hi) * 0.5;
            if (FreeAt(X, Y + dy * m)) lo = m; else hi = m;
        }
        Y += dy * lo;
        if (dy > 0)
        {
            double r = Math.Round(Y);
            if (Math.Abs(r - Y) < 0.6 && FreeAt(X, r)) Y = r;
            return Contact.Floor;
        }
        return Contact.Ceiling;
    }

    /// <summary>Le haut d'une fenêtre traversé en descendant (étape 5).</summary>
    private partial bool PlatformCross(double fromY, double toY, out double top);

    /// <summary>Sur quelque chose de solide (sol) ou sur une fenêtre ?</summary>
    private bool Supported() => !FreeAt(X, Y + 1.5) || OnPlatform();

    private partial bool OnPlatform();

    /// <summary>Vérifie qu'il reste un appui ; sinon Pet tombe. Renvoie faux s'il tombe.</summary>
    private bool CheckSupport()
    {
        if (Supported()) return true;
        StartFall(0, 0);
        return false;
    }

    /// <summary>Remet Pet dans l'espace libre (après un changement d'écran, un lâcher...).</summary>
    public void Resolve()
    {
        if (FreeAt(X, Y)) return;
        var s = Desk.NearestFree(X, Y - HH * 0.5) ?? Scr;
        var w = s.Work;
        X = Math.Clamp(X, w.Left + HW + 1, Math.Max(w.Left + HW + 1, w.Right - HW - 1));
        Y = Math.Clamp(Y, w.Top + HH + 1, w.Bottom);
        if (!FreeAt(X, Y))
        {
            // la boîte déborde encore (zone morte) : on la recentre sur l'écran
            X = s.CenterX;
            Y = Math.Min(Y, w.Bottom);
        }
    }

    // ------------------------------------------------------------------ en l'air

    public void StartFall(double vx, double vy, AirKind kind = AirKind.Fall)
    {
        VX = vx; VY = vy;
        air = kind;
        peakY = Y;
        firstDrop = 0;
        bounces = 0;
        LeaveSurface();
        Enter(St.Air);
    }

    private partial void LeaveSurface();

    private void PrepareJump(double vx, double vy, AirKind kind, double crouch = 0.14)
    {
        jumpVX = vx; jumpVY = vy; jumpKind = kind;
        if (vx != 0) Facing = Math.Sign(vx);
        Enter(St.JumpPrep, crouch);
    }

    private void Launch()
    {
        app.Play(Snd.Jump, 0.8);
        StartFall(jumpVX, jumpVY, jumpKind);
    }

    private void UpdateAir(double dt)
    {
        VY += G * dt;
        double maxFall = 5200 * K;
        if (VY > maxFall) VY = maxFall;
        VX *= Math.Max(0, 1 - 0.12 * dt);

        if (MoveX(VX * dt))
        {
            if (Math.Abs(VX) > 260 * K)
            {
                app.Play(Snd.Bounce, MathX.Clamp(Math.Abs(VX) / (3000 * K), 0.15, 0.7));
                WallDust(Math.Sign(VX));
            }
            VX = -VX * 0.6;
        }
        var c = MoveY(VY * dt);
        if (Y < peakY) peakY = Y;
        if (c == Contact.Ceiling)
        {
            if (VY < -250 * K) app.Play(Snd.Bounce, MathX.Clamp(-VY / (3000 * K), 0.15, 0.6));
            VY = -VY * 0.45;
        }
        else if (c == Contact.Floor || c == Contact.Platform)
        {
            OnTouchDown(c);
        }
    }

    private void OnTouchDown(Contact c)
    {
        double impact = VY;
        double drop = Y - peakY;
        if (firstDrop == 0) firstDrop = drop;
        bool wild = air == AirKind.Thrown || air == AirKind.Fall;
        double bounceMin = 430 * K;

        if (wild && impact > bounceMin && bounces < 7)
        {
            app.Play(Snd.Bounce, MathX.Clamp(impact / (3400 * K), 0.12, 1.0));
            LandDust(impact);
            VY = -impact * 0.5;
            VX *= 0.82;
            bounces++;
            peakY = Y;
            if (c == Contact.Platform) AttachPlatformFromLanding();
            return;
        }

        VY = 0;
        if (c == Contact.Platform) AttachPlatformFromLanding();
        else AttachFloor();
        if (impact > 200 * K) LandDust(impact * 0.6);

        double h = Math.Max(firstDrop, drop);
        bool hard = air == AirKind.Thrown || (air == AirKind.Fall && h > 1.3 * HH);
        stunAfterLand = hard;
        stunDur = MathX.Clamp(0.8 + 2.6 * h / (950 * K), 0.8, 4.0);
        if (impact > 140 * K) app.Play(Snd.Land, MathX.Clamp(impact / (3000 * K), 0.1, 0.5));
        Enter(St.Land);
        OnLanded();
    }

    private partial void AttachPlatformFromLanding();
    private partial void AttachFloor();
    private partial void OnLanded();

    private void UpdateLand(double dt)
    {
        if (Math.Abs(VX) > 1)
        {
            if (MoveX(VX * dt)) VX = -VX * 0.3;
            VX = MathX.Approach(VX, 0, 2600 * K * dt);
            if (!CheckSupport()) return;
        }
        else if (!CheckSupport()) return;

        if (T >= 0.16 && Math.Abs(VX) < 30 * K)
        {
            VX = 0;
            if (stunAfterLand)
            {
                app.Play(Snd.Stars, 0.6);
                Enter(St.Stunned, stunDur);
            }
            else Enter(St.Idle, Rng.Range(0.3, 0.9));
        }
    }

    private void LandDust(double impact)
    {
        int n = (int)MathX.Clamp(impact / (500 * K), 2, 8);
        for (int i = 0; i < n; i++)
        {
            int side = i % 2 == 0 ? -1 : 1;
            app.Fx.Add(FxKind.Dust, X + side * HW * Rng.Range(0.3, 0.9), Y - Scale * Rng.Range(1, 3),
                side * Rng.Range(60, 220) * K, -Rng.Range(20, 90) * K, Rng.Range(0.35, 0.6), Rng.Int(0, 2), drag: 3);
        }
    }

    private void WallDust(int side)
    {
        for (int i = 0; i < 3; i++)
            app.Fx.Add(FxKind.Dust, X + side * HW, Y - HH * Rng.Range(0.2, 0.8),
                -side * Rng.Range(40, 140) * K, Rng.Range(-60, 60) * K, Rng.Range(0.3, 0.5), 1, drag: 3);
    }

    // ------------------------------------------------------------------ au sol

    private void UpdateIdle(double dt)
    {
        if (!CheckSupport()) return;
        TrackCursorWithEyes();
        if (T >= Dur) Decide();
    }

    private void UpdateWalk(double dt)
    {
        double sp = speed * SpeedFactor();
        double d = targetX - X;
        if (Math.Abs(d) < 1.5 || sp <= 1)
        {
            ArriveWalk();
            return;
        }
        int dir = Math.Sign(d);
        Facing = dir;
        double step = dir * Math.Min(sp * dt, Math.Abs(d));
        double before = X;
        bool blocked = MoveX(step);
        double moved = Math.Abs(X - before);
        double prevDist = walkDist;
        walkDist += moved;
        // bruit de pas discret, deux par cycle
        double stepLen = BodyW * (running ? 0.42 : 0.3);
        if ((int)(walkDist / stepLen) != (int)(prevDist / stepLen)) app.Play(Snd.Step, running ? 0.35 : 0.22);
        if (!CheckSupport()) return;
        if (blocked) { OnWalkBlocked(); return; }
        if (Math.Abs(targetX - X) < 1.5) ArriveWalk();
    }

    private void UpdateLook()
    {
        if (Clock.Now >= nextLookChange)
        {
            nextLookChange = Clock.Now + Rng.Range(0.5, 1.2);
            if (CursorNear(BodyW * 7)) { TrackCursorWithEyes(true); return; }
            lookX = Rng.Int(-1, 2);
            lookY = Rng.Chance(0.3) ? -1 : 0;
            if (lookX != 0 && Rng.Chance(0.4)) Facing = lookX;
        }
    }

    /// <summary>Les yeux suivent le pointeur s'il est proche.</summary>
    private void TrackCursorWithEyes(bool force = false)
    {
        var c = app.Cursor;
        HeadPoint(out double hx, out double hy);
        double dx = c.X - hx, dy = c.Y - (hy + HH * 0.25);
        double dist = MathX.Hypot(dx, dy);
        if (!force && dist > BodyW * 6) { if (Clock.Now > nextLookChange) { lookX = 0; lookY = 0; } return; }
        int lx = Math.Abs(dx) > BodyW * 0.25 ? Math.Sign(dx) : 0;
        lookY = dy < -HH * 0.6 ? -1 : dy > HH * 0.8 ? 1 : 0;
        if (lx != 0 && State != St.Walk && Math.Abs(dx) > BodyW * 0.8) Facing = lx;
        lookX = lx * Facing; // regard exprimé dans le repère de Pet (+1 = devant)
        nextLookChange = Clock.Now + 0.8;
    }

    public bool CursorNear(double radius)
    {
        HeadPoint(out double hx, out double hy);
        return MathX.Hypot(app.Cursor.X - hx, app.Cursor.Y - (hy + HH * 0.3)) < radius;
    }

    private void UpdateBlink(double now)
    {
        if (now >= nextBlink)
        {
            blinkEnd = now + 0.11;
            nextBlink = now + Rng.Range(2.0, 6.0);
            if (Rng.Chance(0.15)) nextBlink = now + 0.25; // double clignement
        }
    }

    private bool Blinking => Clock.Now < blinkEnd;

    // ------------------------------------------------------------------ souris

    /// <summary>Clic gauche enfoncé sur un pixel visible de Pet.</summary>
    public void OnMouseDown(double cx, double cy, double now)
    {
        if (State == St.Hidden) return;
        pressing = true;
        dragging = false;
        pressT = now;
        pressX = cx; pressY = cy;
        grabDX = X - cx;
        grabDY = Y - cy;
    }

    public void OnMouseMove(double cx, double cy)
    {
        if (!pressing || dragging) return;
        double thr = 5 * K;
        if (Math.Abs(cx - pressX) > thr || Math.Abs(cy - pressY) > thr) StartDrag();
    }

    public void OnMouseUp(double cx, double cy, double now)
    {
        if (!pressing) return;
        pressing = false;
        if (dragging)
        {
            dragging = false;
            Release(now);
        }
        else
        {
            OnClick(cx, cy, now);
        }
    }

    /// <summary>La capture de la souris a été perdue (autre fenêtre, Échap...) : on lâche Pet.</summary>
    public void OnCaptureLost(double now)
    {
        if (!pressing) return;
        pressing = false;
        if (dragging) { dragging = false; Release(now); }
    }

    public bool IsPressing => pressing;

    private void StartDrag()
    {
        if (!CanBeGrabbed()) { pressing = false; return; }
        dragging = true;
        LeaveSurface();
        app.Play(Snd.Grab, 0.5);
        Enter(St.Dragged);
        // Pet pend sous le point attrapé : on garde l'écart, mais pas plus bas que les pieds
        grabDY = Math.Max(grabDY, HH * 0.15);
    }

    private void UpdateDragged(double dt, double now)
    {
        var c = app.Cursor;
        double tx = c.X + grabDX, ty = c.Y + grabDY;
        X = tx; Y = ty;
        var (vx, _) = c.Velocity(now);
        if (Math.Abs(vx) > 200 * K) Facing = Math.Sign(vx);
    }

    private void Release(double now)
    {
        var (vx, vy) = app.Cursor.Velocity(now);
        double max = 7500 * K;
        double sp = MathX.Hypot(vx, vy);
        if (sp > max) { vx *= max / sp; vy *= max / sp; }
        Resolve();
        OnReleased();
        StartFall(vx, vy, sp > 350 * K ? AirKind.Thrown : AirKind.Fall);
    }

    private partial bool CanBeGrabbed();
    private partial void OnClick(double cx, double cy, double now);
    private partial void OnReleased();

    // ------------------------------------------------------------------ outils

    /// <summary>Milieu du haut de la tête, en pixels écran.</summary>
    public void HeadPoint(out double hx, out double hy)
    {
        var p = lastPose;
        hx = X + p.DX * RenderScale * Facing;
        hy = Y + (p.BodyBottom - p.BodyH) * RenderScale;
    }

    /// <summary>Position de la bouche, en pixels écran.</summary>
    public void MouthPoint(out double mx, out double my)
    {
        var p = lastPose;
        mx = X + p.DX * RenderScale * Facing;
        my = Y + (p.BodyBottom - p.BodyH + 1 + 9.5) * RenderScale;
    }
}

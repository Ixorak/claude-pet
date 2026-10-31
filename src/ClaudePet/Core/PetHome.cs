using ClaudePet.Art;
using ClaudePet.Util;

namespace ClaudePet.Core;

// La maison : sortir par la porte de l'icône, y rentrer pour quitter ; et creuser / ressortir du sol.
internal sealed partial class Pet
{
    private bool homeBound;           // Pet rentre à la maison
    private int homeStep;              // 0 : décider, 1 : marcher, 2 : sauter, 3 : rentrer
    private double homeDeadline;
    private double doorX, doorY, doorMul;
    private double flyX0, flyY0, flyVY0, flyDur, flyX1, flyY1;
    private bool quitAfterDig;
    private double digFloor;
    private Screen? emergeScreen;
    private double emergeX;

    // ------------------------------------------------------------------ apparition

    /// <summary>Sort par la porte de l'icône de la maison.</summary>
    public void SpawnAtDoor(DesktopIcons.Icon icon)
    {
        var (cx, bottom, height) = HouseArt.Door(icon.Size);
        doorX = icon.Rect.Left + cx * icon.Size;
        doorY = icon.Rect.Top + bottom * icon.Size;
        double doorH = Math.Max(2, height * icon.Size);
        Scr = Desk.At(doorX, doorY - 1) ?? Desk.Nearest(doorX, doorY);
        Scale = TargetScale(Scr);
        doorMul = MathX.Clamp(doorH / (PetPainter.Height * Scale), 0.02, 1);
        X = doorX; Y = doorY;
        SizeMul = 0.001;
        Facing = Rng.Sign();
        Enter(St.ExitDoor, 1.25);
        app.Play(Snd.Exit, 0.8);
        DoorSparkles(doorX, doorY - doorH * 0.5, doorH);
        Log.Write($"Apparition par la porte ({doorX:F0}, {doorY:F0}), porte {doorH:F0} px");
    }

    /// <summary>Sort du sol (si l'icône est introuvable : en bas du Dell, l'écran principal).</summary>
    public void EmergeAt(Screen s, double x)
    {
        Scr = s;
        Scale = TargetScale(s);
        SizeMul = 1;
        X = Math.Clamp(x, s.Work.Left + HW + 2, s.Work.Right - HW - 2);
        digFloor = s.Floor;
        Y = digFloor + HH;
        Facing = Rng.Sign();
        Enter(St.Emerge, 1.0);
        app.Play(Snd.Emerge, 0.7);
        app.Mounds.Add(X, digFloor, MoundPixel, 9);   // la motte de la sortie du trou
        Log.Write($"Sortie du sol à x={X:F0} sur l'écran {s.Index}");
    }

    private void UpdateExitDoor(double dt)
    {
        X = doorX; Y = doorY;
        const double pop = 0.32, grow = 1.15;
        if (T < pop) SizeMul = Math.Max(0.001, doorMul * MathX.SmoothStep(T / pop));
        else if (T < grow) SizeMul = doorMul + (1 - doorMul) * MathX.SmoothStep((T - pop) / (grow - pop));
        else SizeMul = 1;
        if (T >= Dur)
        {
            SizeMul = 1;
            if (!FreeAt(X, Y)) Resolve();
            StartFall(Facing * BodyW * 0.7, -Math.Sqrt(2 * G * HH * 0.45), AirKind.Jump);
        }
    }

    private void DoorSparkles(double x, double y, double size)
    {
        for (int i = 0; i < 6; i++)
        {
            double a = i * Math.PI / 3 + Rng.Range(-0.3, 0.3);
            double sp = Rng.Range(60, 160) * K;
            app.Fx.Add(FxKind.Spark, x, y, Math.Cos(a) * sp, Math.Sin(a) * sp - 40 * K, Rng.Range(0.4, 0.7), Rng.Int(0, 2), drag: 2.5);
        }
    }

    // ------------------------------------------------------------------ rentrer

    /// <summary>Rentrer à la maison, puis quitter l'appli.</summary>
    public void GoHome()
    {
        if (homeBound) return;
        homeBound = true;
        homeStep = 0;
        homeDeadline = Clock.Now + 25;
        Log.Write("Retour à la maison demandé");
        if (State == St.Hidden || State == St.Underground) { app.Quit(); return; }
        if (State == St.Dragged || pressing)
        {
            pressing = false;
            dragging = false;
            Resolve();
            StartFall(0, 0);
        }
        OnGoHomeStarted();
    }

    private partial void OnGoHomeStarted();

    private bool HomeReady => State switch
    {
        St.Idle or St.Walk or St.Sit or St.Look or St.Stretch or St.Stunned or St.GetUp or St.JumpPrep => true,
        St.Land => T > 0.1,
        _ => false,
    };

    private void UpdateHomeBound(double now)
    {
        if (now > homeDeadline) { Log.Write("Retour à la maison trop long : on quitte"); app.Quit(); return; }
        if (homeStep == 0 && HomeReady)
        {
            if (State == St.Land) VX = 0;
            if (DesktopIcons.Find(App.HouseName, out var icon) != DesktopIcons.Result.Found)
            {
                StartDig(quit: true);
                homeStep = 3;
                return;
            }
            var (cx, bottom, height) = HouseArt.Door(icon.Size);
            doorX = icon.Rect.Left + cx * icon.Size;
            doorY = icon.Rect.Top + bottom * icon.Size;
            double doorH = Math.Max(2, height * icon.Size);
            var houseScreen = Desk.At(doorX, doorY - 1);
            if (houseScreen == null || houseScreen.Fullscreen)
            {
                StartDig(quit: true);
                homeStep = 3;
                return;
            }
            doorMul = MathX.Clamp(doorH / (PetPainter.Height * TargetScale(houseScreen)), 0.02, 1);
            FloorRange(BodyW * 40, out double minX, out double maxX);
            double tx = Math.Clamp(doorX, minX, maxX);
            homeStep = 1;
            if (Math.Abs(tx - X) > BodyW * 0.3)
            {
                WalkTo(tx, run: true);
                speed = BodyW * 2.6;
            }
            else StartHomeJump();
        }
        else if (homeStep == 1 && State != St.Walk && State != St.Air && State != St.JumpPrep && State != St.Land)
        {
            if (HomeReady) StartHomeJump();
        }
    }

    /// <summary>Arrivée de la marche quand Pet rentre : il saute vers la porte.</summary>
    private bool HomeArrived()
    {
        if (!homeBound || homeStep != 1) return false;
        StartHomeJump();
        return true;
    }

    private void StartHomeJump()
    {
        homeStep = 2;
        flyX0 = X; flyY0 = Y; flyX1 = doorX; flyY1 = doorY;
        double apex = Math.Min(flyY0, flyY1) - Math.Max(HH * 0.6, Math.Abs(flyX1 - flyX0) * 0.25);
        double g = G;
        flyVY0 = -Math.Sqrt(2 * g * Math.Max(1, flyY0 - apex));
        double disc = flyVY0 * flyVY0 - 2 * g * (flyY0 - flyY1);
        flyDur = (-flyVY0 + Math.Sqrt(Math.Max(0, disc))) / g;
        if (flyDur < 0.25) flyDur = 0.25;
        if (Math.Abs(flyX1 - flyX0) > 2) Facing = Math.Sign(flyX1 - flyX0);
        app.Play(Snd.Jump, 0.9);
        LeaveSurface();
        Enter(St.HomeJump, flyDur);
    }

    private void UpdateHomeJump(double dt)
    {
        double t = Math.Min(T, flyDur);
        double u = t / flyDur;
        X = flyX0 + (flyX1 - flyX0) * u;
        Y = flyY0 + flyVY0 * t + 0.5 * G * t * t;
        VY = flyVY0 + G * t;
        SizeMul = 1 + (doorMul - 1) * MathX.SmoothStep(u);
        if (T >= flyDur)
        {
            X = flyX1; Y = flyY1;
            SizeMul = doorMul;
            homeStep = 3;
            app.Play(Snd.Enter, 0.85);
            Enter(St.EnterDoor, 0.45);
        }
    }

    private void UpdateEnterDoor(double dt)
    {
        X = doorX; Y = doorY;
        SizeMul = Math.Max(0.001, doorMul * (1 - MathX.SmoothStep(T / Dur)));
        if (T >= Dur)
        {
            DoorSparkles(doorX, doorY - doorMul * HH / Math.Max(0.001, SizeMul) * 0.5, 8);
            State = St.Hidden;
            app.QuitSoon(0.35);
        }
    }

    // ------------------------------------------------------------------ creuser / sortir du sol

    public void StartDig(bool quit)
    {
        quitAfterDig = quit;
        VX = 0; VY = 0;
        digFloor = Y;
        LeaveSurface();
        app.Play(Snd.Dig, 0.7);
        Enter(St.Dig, 1.25);
        // la motte du début du trou (sur un écran en plein écran, elle part avec lui)
        app.Mounds.Add(X, digFloor, MoundPixel, Scr.Fullscreen ? 1.9 : 9);
    }

    /// <summary>Taille d'un pixel des mottes de terre.</summary>
    private int MoundPixel => Math.Max(2, (int)Math.Round(Scale * 0.75));

    private void UpdateDig(double dt)
    {
        double sink = MathX.SmoothStep((T - 0.15) / (Dur - 0.15));
        Y = digFloor + HH * 1.05 * sink;
        if (Rng.Chance(dt * 28)) DirtBurst(1);
        if (((int)(T * 6)) != ((int)((T - dt) * 6))) app.Play(Snd.Dig, 0.35);
        if (T >= Dur)
        {
            if (quitAfterDig) { State = St.Hidden; app.QuitSoon(0.2); return; }
            // le trajet sous terre dure d'autant plus longtemps que la sortie est loin
            double travel = emergeScreen != null ? Math.Abs(emergeX - X) : 0;
            Enter(St.Underground, MathX.Clamp(0.7 + travel / (BodyW * 14), 0.7, 2.5));
            OnUnderground();
        }
    }

    private partial void OnUnderground();

    private void UpdateUnderground(double dt)
    {
        if (T < Dur) return;
        var s = emergeScreen ?? Desk.NearestFree(X, Y) ?? Desk.Primary;
        if (s.Fullscreen)
        {
            s = Desk.NearestFree(X, Y);
            if (s == null) { State = St.Hidden; OnHiddenAll(); return; }
        }
        EmergeAt(s, emergeScreen != null ? emergeX : s.CenterX);
        emergeScreen = null;
    }

    private partial void OnHiddenAll();

    /// <summary>Se cache complètement (tous les écrans en plein écran).</summary>
    public void HideAll()
    {
        State = St.Hidden;
        T = 0;
        OnHiddenAll();
    }

    private void UpdateEmerge(double dt)
    {
        double rise = MathX.SmoothStep(T / 0.85);
        Y = digFloor + HH * (1 - rise);
        if (T < 0.85 && Rng.Chance(dt * 22)) DirtBurst(1);
        if (T >= Dur)
        {
            Y = digFloor;
            AttachFloor();
            if (!FreeAt(X, Y)) Resolve();
            Enter(St.Idle, Rng.Range(0.6, 1.2));
        }
    }

    private void DirtBurst(int n)
    {
        for (int i = 0; i < n; i++)
        {
            int side = Rng.Sign();
            uint col = Rng.Int(0, 3) switch { 0 => Col.Rgb(0x6B, 0x44, 0x23), 1 => Col.Rgb(0x8B, 0x5A, 0x2B), _ => Col.Rgb(0x4E, 0x32, 0x18) };
            app.Fx.Add(FxKind.Dirt, X + side * HW * Rng.Range(0.1, 0.8), digFloor - Scale,
                side * Rng.Range(80, 320) * K, -Rng.Range(250, 650) * K, Rng.Range(0.5, 0.9), Rng.Int(0, 2),
                gravity: G * 0.8, color: col);
        }
    }

    // ------------------------------------------------------------------ poses

    private PetPose HomePose(out double clipY)
    {
        clipY = double.NaN;
        switch (State)
        {
            case St.ExitDoor:
            {
                int f = (int)(T * 6) & 1;
                if (T < 0.32) return PetPose.Stand with { Eyes = Eyes.Wide, Mouth = Mouth.SmallO };
                return PetPose.Stand with { L = f == 0 ? Arm.Up : Arm.Raised, R = Arm.Side, Eyes = Eyes.Happy, Mouth = Mouth.Grin };
            }
            case St.HomeJump:
                return PetPose.Stand with { BodyW = 22, BodyH = 18, Legs = Legs.Tuck, L = Arm.Raised, R = Arm.Raised, Eyes = Eyes.Happy, Mouth = Mouth.Smile };
            case St.EnterDoor:
            {
                int f = (int)(T * 8) & 1;
                return PetPose.Stand with { L = f == 0 ? Arm.Up : Arm.Raised, R = Arm.Side, Eyes = Eyes.Happy, Mouth = Mouth.Smile };
            }
            case St.Dig:
            {
                clipY = digFloor;
                int f = (int)(T * 8) & 1;
                return PetPose.Stand with
                {
                    BodyBottom = -5, Legs = Legs.Kneel,
                    L = new Arm(ArmK.Dig, 0, (sbyte)(f == 0 ? -4 : 0)), R = new Arm(ArmK.Dig, 0, (sbyte)(f == 0 ? 0 : -4)),
                    Eyes = Eyes.Squeeze, LookY = 1, DX = f == 0 ? 1 : 0,
                };
            }
            case St.Emerge:
            {
                clipY = digFloor;
                if (T < 0.85) return PetPose.Stand with { L = Arm.Up, R = Arm.Up, Eyes = Eyes.Squeeze, Mouth = Mouth.Open };
                int f = (int)(T * 14) & 1;
                return PetPose.Stand with { DX = f == 0 ? -1 : 1, Eyes = Eyes.Happy, Mouth = Mouth.Smile };
            }
        }
        return PetPose.Stand;
    }
}

using ClaudePet.Util;

namespace ClaudePet.Core;

// Le cerveau de Pet : ce qu'il décide de faire tout seul.
internal sealed partial class Pet
{
    /// <summary>Apparition (étape 1 : il tombe du haut de l'écran principal).</summary>
    public void Spawn()
    {
        var s = Desk.NearestFree(Desk.Primary.CenterX, Desk.Primary.Work.Top) ?? Desk.Primary;
        Scr = s;
        Scale = TargetScale(s);
        X = s.CenterX;
        Y = s.Work.Top + HH + 4;
        Facing = Rng.Sign();
        StartFall(Rng.Range(-200, 200) * K, 0, AirKind.Fall);
    }

    /// <summary>Facteur de vitesse : la faim ralentit Pet (mais une baie en vue le motive un peu).</summary>
    private double SpeedFactor()
    {
        double f = app.HungerSpeed;
        if (app.FeedingActive) f = Math.Max(f, 0.35);
        return f;
    }

    /// <summary>Choisit la prochaine activité.</summary>
    private void Decide()
    {
        if (DemoHold) { Enter(St.Idle, 0.8); return; }
        if (Angry) { AngryDecide(); return; }
        double hunger = app.HungerSpeed;
        if (hunger < 0.12)
        {
            // très faim depuis longtemps : il ne bouge plus et reste assis (rien de grave, une baie et tout repart)
            Enter(St.Sit, Rng.Range(20, 40));
            return;
        }
        bool calm = app.Cfg.Calm, hungry = app.Hungry, lively = !calm && !hungry;
        Span<double> w = stackalloc double[18];
        w[0] = (calm ? 26 : 28) * (hungry ? hunger : 1);  // marcher
        w[1] = lively ? 11 : 0;   // courir
        w[2] = (calm ? 38 : 11) + (hungry ? 30 : 0);  // s'asseoir
        w[3] = calm ? 12 : 9;     // regarder autour
        w[4] = calm ? 5 : 5;      // s'étirer
        w[5] = lively ? 8 : 0;    // petit saut sur place
        w[6] = lively ? 14 : 0;   // sauter sur une fenêtre
        w[7] = OnWindow ? 5 : 0;  // redescendre de la fenêtre
        w[8] = lively && !OnWindow ? 12 : 0;                      // grimper à un mur, ou au côté d'une page agrandie
        w[9] = !hungry && TunnelCandidate(out _) != null ? 5 : 0; // tunnel sous un écran en plein écran
        w[10] = calm ? 4 : hungry ? 5 : 1.5;                      // petite sieste
        w[11] = CursorNear(BodyW * 9) ? 10 : 0;                   // regarder la souris
        w[12] = lively ? 12 : 0;                                  // escalader le côté d'une fenêtre pour se mettre dessus
        w[13] = hungry ? 2 : calm ? 9 : 6;                        // boire un café
        w[14] = hungry ? 2 : calm ? 9 : 7;                        // taper sur son ordinateur
        w[15] = hungry ? 1 : calm ? 7 : 6;                        // écouter de la musique (et danser)
        w[16] = hungry ? 2 : calm ? 9 : 5;                        // lire un livre
        w[17] = hungry ? 1 : calm ? 5 : 6;                        // souffler des bulles de savon
        switch (Rng.Pick(w))
        {
            case 0: if (!StartWander(false)) Enter(St.Sit, Rng.Range(3, 8)); break;
            case 1: if (!StartWander(true)) Enter(St.Look, Rng.Range(1.5, 3)); break;
            case 2: Enter(St.Sit, calm ? Rng.Range(8, 25) : Rng.Range(3, 9)); break;
            case 3: Enter(St.Look, Rng.Range(1.5, 3.5)); break;
            case 4: app.Play(Snd.Yawn, 0.4); Enter(St.Stretch, 1.7); break;
            case 5: Hop(); break;
            // grimper : s'il n'y a pas de fenêtre à portée, il essaie autre chose en hauteur
            case 6: if (!TryJumpToWindow() && !TryClimbWindow() && !GoClimb()) Enter(St.Look, Rng.Range(1.0, 2.5)); break;
            case 7: if (!TryHopDown()) Enter(St.Sit, Rng.Range(3, 8)); break;
            case 8: if (!GoClimb() && !TryJumpToWindow()) Enter(St.Look, Rng.Range(1.0, 2.5)); break;
            case 9: if (!StartTunnel()) Enter(St.Idle, 1); break;
            case 10: Nap(); break;
            case 11:
                watchUntil = Clock.Now + Rng.Range(2.5, 5.0);
                Enter(St.Look, watchUntil - Clock.Now);
                break;
            case 12: if (!TryClimbWindow() && !TryJumpToWindow() && !GoClimb()) Enter(St.Look, Rng.Range(1.0, 2.5)); break;
            case 13: StartCoffee(); break;
            case 14: StartLaptop(); break;
            case 15: StartMusic(); break;
            case 16: StartRead(); break;
            case 17: StartBubbles(); break;
            default: Enter(St.Idle, 1); break;
        }
    }

    private void Hop()
    {
        double h = HH * Rng.Range(0.45, 0.8);
        double vy = -Math.Sqrt(2 * G * h);
        double vx = Rng.Chance(0.5) ? 0 : Facing * BodyW * Rng.Range(0.3, 0.8);
        PrepareJump(vx, vy, AirKind.Hop, 0.12);
    }

    /// <summary>Part se balader sur le sol où il est. Renvoie faux s'il n'y a pas la place.</summary>
    private bool StartWander(bool run)
    {
        double limit = run ? BodyW * 14 : BodyW * 7;
        FloorRange(limit, out double minX, out double maxX);
        int dir = Rng.Sign();
        double dist = run ? Rng.Range(BodyW * 3, BodyW * 12) : Rng.Range(BodyW * 0.8, BodyW * 6);
        double target = Math.Clamp(X + dir * dist, minX, maxX);
        if (Math.Abs(target - X) < BodyW * 0.5)
        {
            target = Math.Clamp(X - dir * dist, minX, maxX);
            if (Math.Abs(target - X) < BodyW * 0.5) return false;
        }
        WalkTo(target, run);
        return true;
    }

    private void WalkTo(double x, bool run)
    {
        wantPageClimb = false;
        targetX = x;
        running = run;
        bool calm = app.Cfg.Calm;
        speed = run ? BodyW * 2.2 : calm ? BodyW * 0.25 : BodyW * 0.55;
        Facing = Math.Sign(x - X) == 0 ? Facing : Math.Sign(x - X);
        Enter(St.Walk);
    }

    /// <summary>Jusqu'où Pet peut marcher sur le sol actuel sans tomber ni se cogner.</summary>
    private void FloorRange(double limit, out double minX, out double maxX)
    {
        double step = Math.Max(4, HW * 0.5);
        minX = X;
        for (double x = X - step; x > X - limit; x -= step)
        {
            if (!FreeAt(x, Y) || !SupportedAt(x)) break;
            minX = x;
        }
        maxX = X;
        for (double x = X + step; x < X + limit; x += step)
        {
            if (!FreeAt(x, Y) || !SupportedAt(x)) break;
            maxX = x;
        }
    }

    private bool SupportedAt(double x) => !FreeAt(x, Y + 1.5) || PlatformSupportsAt(x);

    private partial bool PlatformSupportsAt(double x);

    private void ArriveWalk()
    {
        if (HomeArrived()) return;
        if (TunnelArrived()) return;
        if (ClimbWindowArrived()) return;
        if (PageClimbArrived()) return;
        wantClimb = false;
        Enter(St.Idle, Angry ? Rng.Range(0.05, 0.3) : app.Cfg.Calm ? Rng.Range(1.5, 4.0) : Rng.Range(0.4, 2.0));
    }

    private void OnWalkBlocked()
    {
        if (app.FeedingActive)
        {
            // la baie est derrière un mur : il attend là en la regardant
            feedWaitUntil = Clock.Now + 1.0;
            Enter(St.Idle, Rng.Range(0.6, 1.2));
            return;
        }
        if (TunnelArrived()) return;
        climbWinWalk = false;
        wantPageClimb = false;
        // un mur : il grimpe (s'il était venu pour ça, ou parfois), sinon demi-tour
        bool climb = wantClimb || Rng.Chance(Angry ? 0.6 : 0.35);
        wantClimb = false;
        if (climb && !homeBound && TryStartClimb()) return;
        Facing = -Facing;
        Enter(St.Idle, Rng.Range(0.4, 1.0));
    }
}

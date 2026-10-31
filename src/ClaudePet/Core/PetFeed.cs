using ClaudePet.Art;
using ClaudePet.Util;

namespace ClaudePet.Core;

// Nourrir et faim : Pet court vers la baie, ouvre grand la bouche, la mange ; affamé, il ralentit et le dit.
internal sealed partial class Pet
{
    // Nourrir
    private double feedRetarget, feedWaitUntil, jumpCooldown, mouthSoundAt = -10;
    private bool chewAfterLanding;
    private double nextMunch, nextCrumb;

    // Bulle de parole (« J'ai faim ! »)
    private double sayUntil, nextHungrySay = 60;
    private string sayText = "";
    private bool sayVisible;
    private int sayU, sayX0, sayY0, sayLW, sayLH;
    private uint[] sayBuf = new uint[16];
    private string sayBufText = "";

    private const string HungryText = "J'ai faim !";

    // ------------------------------------------------------------------ événements

    public void OnFeedingStarted()
    {
        double now = Clock.Now;
        if (State == St.Sleep) WakeUp();
        sayUntil = 0;
        feedRetarget = 0;
        ExclaimUntil = now + 0.6;
        ExclaimQuestion = false;
        if (State is St.Sit or St.Look or St.Stretch or St.ReachUp || BusyWithActivity) Enter(St.Idle, 0.2);
    }

    public void OnFeedingCancelled()
    {
        ExclaimUntil = Clock.Now + 0.8;
        ExclaimQuestion = true;
        if (State == St.MouthOpen) Enter(St.Idle, 0.9);
    }

    public void OnBecameHungry()
    {
        if (app.FeedingActive) return;
        Say(HungryText, 4.5);
        nextHungrySay = Clock.Now + Rng.Range(45, 90);
    }

    public void Say(string text, double seconds)
    {
        sayText = text;
        sayUntil = Clock.Now + seconds;
        app.Play(Snd.Bubble, 0.3);
    }

    // ------------------------------------------------------------------ cerveau

    /// <summary>Appelé à chaque mise à jour pendant que la baie est là.</summary>
    private void FeedThink(double now)
    {
        var b = app.Berry;
        if (!b.Visible || homeBound || pressing || Attentive) return;
        if (State == St.Air)
        {
            if (!chewAfterLanding && InBiteReach(b)) Bite();
            return;
        }
        if (State is not (St.Idle or St.Walk or St.Sit or St.Look or St.Stretch or St.ReachUp or St.WakeUp or St.MouthOpen)) return;
        if (InBiteReach(b)) { Bite(); return; }
        if (State == St.MouthOpen) return; // UpdateMouthOpen s'en occupe
        if (BerryNear(b, 1.3)) { OpenMouth(now); return; }

        if (now < feedWaitUntil) { if (Math.Abs(b.X - X) > BodyW * 0.2) Facing = Math.Sign(b.X - X); return; }
        if (State == St.Walk && now < feedRetarget) return;
        feedRetarget = now + 0.25;
        double dx = b.X - X, above = Y - b.Y;
        if (CanJumpFor(b, now)) { JumpForBerry(b, now); return; }
        if (Math.Abs(dx) < BodyW * 0.4 && above > HH) { OpenMouth(now); return; } // juste dessous, hors d'atteinte
        ChaseTo(b.X);
    }

    /// <summary>La baie est assez près de la bouche pour être croquée.</summary>
    private bool InBiteReach(Berry b)
    {
        MouthPoint(out double mx, out double my);
        if (MathX.Hypot(b.X - mx, b.Y - my) < BodyW * 0.3 + b.Pixel * 3) return true;
        // posée par terre contre ses pieds : il se penche
        return b.State == Berry.Mode.Lying && Math.Abs(b.X - X) < HW * 0.9 && Math.Abs(b.Y - (Y - HH * 0.15)) < HH * 0.45;
    }

    private bool BerryNear(Berry b, double factor)
    {
        MouthPoint(out double mx, out double my);
        if (MathX.Hypot(b.X - mx, b.Y - my) < BodyW * factor) return true;
        return b.State == Berry.Mode.Lying && Math.Abs(b.X - X) < BodyW * factor * 0.6 && Math.Abs(b.Y - Y) < HH;
    }

    private bool BerryAbove(Berry b) => Y - b.Y > HH * 0.95;

    private bool CanJumpFor(Berry b, double now)
    {
        if (app.Cfg.Calm || now < jumpCooldown || app.HungerSpeed < 0.3) return false;
        double above = Y - b.Y;
        return Math.Abs(b.X - X) < BodyW * 1.1 && above > HH * 1.0 && above < HH * 4.2 && Supported();
    }

    private void ChaseTo(double x)
    {
        bool calm = app.Cfg.Calm;
        targetX = x;
        running = !calm;
        speed = calm ? BodyW * 0.4 : BodyW * 2.2;
        if (Math.Abs(x - X) > 1) Facing = Math.Sign(x - X);
        if (State != St.Walk) Enter(St.Walk);
    }

    private void JumpForBerry(Berry b, double now)
    {
        MouthPoint(out _, out double my);
        double mouthAbove = Y - my;
        double rise = Math.Max(HH * 0.4, (Y - b.Y) - mouthAbove + b.Pixel * 2);
        double vy0 = -Math.Sqrt(2 * G * rise);
        double tUp = -vy0 / G;
        double vx = MathX.Clamp((b.X - X) / tUp, -BodyW * 4, BodyW * 4);
        jumpCooldown = now + 1.3;
        if (now - mouthSoundAt > 1.2) { app.Play(Snd.MouthOpen, 0.45); mouthSoundAt = now; }
        Log.Write($"Baie : saut (Pet {X:F0},{Y:F0}, baie {b.X:F0},{b.Y:F0} {b.State}, montée {rise:F0})");
        PrepareJump(vx, vy0, AirKind.Jump, 0.1);
    }

    private void OpenMouth(double now)
    {
        if (State == St.MouthOpen) return;
        if (now - mouthSoundAt > 1.2) { app.Play(Snd.MouthOpen, 0.55); mouthSoundAt = now; }
        VX = 0;
        Enter(St.MouthOpen);
    }

    private void UpdateMouthOpen(double dt, double now)
    {
        if (!CheckSupport()) return;
        var b = app.Berry;
        if (!b.Visible) { Enter(St.Idle, 0.6); return; }
        double dx = b.X - X;
        if (Math.Abs(dx) > BodyW * 0.12) Facing = Math.Sign(dx);
        bool under = Math.Abs(dx) < BodyW * 0.6 && BerryAbove(b);
        if (!BerryNear(b, 1.9) && !under) { Enter(St.Idle, 0.05); return; } // trop loin : il recourt
        // petits pas vers la baie
        if (Math.Abs(dx) > HW * 0.35)
        {
            double sp = BodyW * 0.9 * SpeedFactor();
            MoveX(Math.Sign(dx) * Math.Min(Math.Abs(dx), sp * dt));
            if (!CheckSupport()) return;
        }
        if (T > 0.5 && CanJumpFor(b, now)) JumpForBerry(b, now);
    }

    /// <summary>Croque la baie : la jauge remonte, puis il mâche.</summary>
    private void Bite()
    {
        Log.Write($"Baie : croquée (Pet {X:F0},{Y:F0} {State}, baie {app.Berry.X:F0},{app.Berry.Y:F0} {app.Berry.State})");
        app.Berry.Eaten();
        app.FeedPet();
        CalmDown("il a mangé");
        app.Play(Snd.Eat, 0.65);
        Crumbs(3);
        if (State == St.Air) { chewAfterLanding = true; return; }
        StartChewing();
    }

    private void StartChewing()
    {
        chewAfterLanding = false;
        VX = 0;
        double now = Clock.Now;
        nextMunch = now + 0.3;
        nextCrumb = now + 0.15;
        Enter(St.Eat, 2.4);
    }

    private void UpdateEat(double dt, double now)
    {
        if (!CheckSupport()) return;
        if (now >= nextMunch && T < Dur - 0.6)
        {
            nextMunch = now + 0.3;
            app.Play(Snd.Eat, 0.5);
        }
        if (now >= nextCrumb && T < Dur - 0.7)
        {
            nextCrumb = now + Rng.Range(0.18, 0.35);
            Crumbs(1);
        }
        if (T >= Dur)
        {
            HeadPoint(out double hx, out double hy);
            for (int i = 0; i < 3; i++)
                app.Fx.Add(FxKind.Heart, hx + (i - 1) * BodyW * 0.3, hy - Scale * 2,
                    (i - 1) * 25 * K, -Rng.Range(60, 90) * K, Rng.Range(1.2, 1.6));
            app.Play(Snd.Happy, 0.5);
            Enter(St.Idle, Rng.Range(0.8, 1.5));
        }
    }

    private void Crumbs(int n)
    {
        MouthPoint(out double mx, out double my);
        for (int i = 0; i < n; i++)
        {
            uint col = Rng.Chance(0.5) ? Berry.Juice : Col.Rgb(0xFF, 0x9C, 0xAE);
            app.Fx.Add(FxKind.Crumb, mx + Facing * Scale * Rng.Range(1, 4), my + Scale,
                Facing * Rng.Range(20, 110) * K, -Rng.Range(40, 160) * K, Rng.Range(0.5, 0.8), Rng.Int(0, 2),
                gravity: G * 0.8, color: col);
        }
    }

    /// <summary>Petits rappels quand il a faim (sans la baie) ; mâcher après avoir attrapé la baie en l'air.</summary>
    private void FeedMoodTick(double now)
    {
        if (chewAfterLanding && State == St.Idle && Supported())
        {
            StartChewing();
            return;
        }
        if (!app.Hungry || app.FeedingActive || now < nextHungrySay) return;
        if (State is St.Hidden or St.Underground or St.HomeJump or St.EnterDoor or St.Dig or St.ExitDoor or St.Emerge or St.Sleep) return;
        nextHungrySay = now + Rng.Range(40, 80);
        Say(HungryText, 4);
    }

    // ------------------------------------------------------------------ poses

    private PetPose FeedPose()
    {
        switch (State)
        {
            case St.MouthOpen:
            {
                var b = app.Berry;
                int f = (int)(T * 4) & 1;
                if (b.Active && b.State == Berry.Mode.Lying && Math.Abs(b.Y - Y) < HH * 0.6)
                    return PetPose.Stand with
                    {
                        BodyBottom = -4, Legs = Legs.Kneel, L = Arm.Down, R = Arm.Down,
                        Mouth = Mouth.Wide, Eyes = Eyes.Wide, LookY = 1, LookX = 1,
                    };
                bool up = b.Active && BerryAbove(b);
                return PetPose.Stand with
                {
                    L = up ? (f == 0 ? Arm.Up : Arm.Reach) : Arm.Raised,
                    R = up ? (f == 0 ? Arm.Reach : Arm.Up) : Arm.Raised,
                    Mouth = Mouth.Wide, Eyes = Eyes.Wide, LookY = up ? -1 : 0, LookX = 1,
                    LiftA = up ? f : 0, LiftB = up ? 1 - f : 0,
                };
            }
            case St.Eat:
            {
                if (T >= Dur - 0.5) return PetPose.Sitting with { Eyes = Eyes.Happy, Mouth = Mouth.Grin, Blush = true };
                int f = (int)(T * 6.5) & 1;
                return PetPose.Sitting with
                {
                    BodyW = f == 0 ? 25 : 24, BodyH = f == 0 ? 15 : 16,
                    Mouth = f == 0 ? Mouth.Chew : Mouth.None, Eyes = Eyes.Happy, Blush = true,
                };
            }
        }
        return PetPose.Stand;
    }

    /// <summary>Retouches de la pose : bouche ouverte en sautant vers la baie, mine triste quand il a faim.</summary>
    private PetPose FeedRetouch(PetPose p)
    {
        if (State == St.Air)
        {
            if (chewAfterLanding) { p.Mouth = Mouth.Chew; p.Eyes = Eyes.Happy; }
            else if (app.FeedingActive && air == AirKind.Jump) { p.Mouth = Mouth.Wide; p.Eyes = Eyes.Wide; }
            return p;
        }
        if (app.Hungry && State is St.Idle or St.Sit or St.Walk or St.Look)
        {
            if (p.Mouth is Mouth.None or Mouth.Smile) p.Mouth = Mouth.Frown;
            if (app.HungerSpeed < 0.35 && p.Eyes == Eyes.Open) p.Eyes = Eyes.Half;
        }
        return p;
    }

    // ------------------------------------------------------------------ bulle de parole (dans la fenêtre d'effets)

    /// <summary>Surcouches supplémentaires dans la fenêtre d'effets : la bulle « J'ai faim ! ».</summary>
    public void ExtraOverlays(Fx fx, int k)
    {
        sayVisible = false;
        if (Clock.Now >= sayUntil || sayText.Length == 0) return;
        if (State is St.Hidden or St.Underground or St.HomeJump or St.EnterDoor or St.Dig or St.ExitDoor or St.Emerge) return;
        if (sayBufText != sayText) BuildSayBubble();
        int u = Math.Clamp((int)Math.Round(Scale * 0.42), 2, 6);
        HeadPoint(out double hx, out double hy);
        int w = sayLW * u, h = sayLH * u;
        double cx = hx + Facing * BodyW * 0.1;
        int x0 = (int)Math.Round(cx - w / 2.0);
        int y0 = (int)Math.Round(hy - Scale * 2 - h);
        var wr = Scr.Work;
        x0 = Math.Clamp(x0, wr.Left + 2, Math.Max(wr.Left + 2, wr.Right - w - 2));
        y0 = Math.Max(y0, wr.Top + 2);
        sayU = u; sayX0 = x0; sayY0 = y0;
        fx.Reserve(x0, y0, x0 + w, y0 + h);
        sayVisible = true;
    }

    public unsafe void ExtraDraw(uint* bits, int stride, int dw, int dh, int left, int top)
    {
        if (!sayVisible) return;
        int u = sayU, ox = sayX0 - left, oy = sayY0 - top;
        for (int ly = 0; ly < sayLH; ly++)
            for (int lx = 0; lx < sayLW; lx++)
            {
                uint c = sayBuf[ly * sayLW + lx];
                if ((c >> 24) == 0) continue;
                for (int yy = 0; yy < u; yy++)
                {
                    int y = oy + ly * u + yy;
                    if ((uint)y >= (uint)dh) continue;
                    uint* row = bits + y * stride;
                    for (int xx = 0; xx < u; xx++)
                    {
                        int x = ox + lx * u + xx;
                        if ((uint)x < (uint)dw) row[x] = c;
                    }
                }
            }
    }

    /// <summary>Dessine la bulle (contour, coins arrondis, petite queue vers Pet) en pixels logiques opaques.</summary>
    private void BuildSayBubble()
    {
        sayBufText = sayText;
        uint ink = Col.Rgb(0x3A, 0x2A, 0x22), fill = Col.Rgb(0xFF, 0xF9, 0xF0);
        int tw = PixelFont.Width(sayText);
        int bw = tw + 8, bh = 13;
        sayLW = bw;
        sayLH = bh + 3;
        if (sayBuf.Length < sayLW * sayLH) sayBuf = new uint[sayLW * sayLH];
        Array.Clear(sayBuf, 0, sayLW * sayLH);
        var buf = sayBuf;
        int lw = sayLW;
        void Put(int x, int y, uint c) { if (x >= 0 && y >= 0 && x < lw && y < sayLH) buf[y * lw + x] = c; }
        for (int y = 1; y < bh - 1; y++)
            for (int x = 1; x < bw - 1; x++) Put(x, y, fill);
        for (int x = 2; x < bw - 2; x++) { Put(x, 0, ink); Put(x, bh - 1, ink); }
        for (int y = 2; y < bh - 2; y++) { Put(0, y, ink); Put(bw - 1, y, ink); }
        Put(1, 1, ink); Put(bw - 2, 1, ink); Put(1, bh - 2, ink); Put(bw - 2, bh - 2, ink);
        // queue
        int tx = bw / 2 - 2;
        Put(tx + 1, bh - 1, fill); Put(tx + 2, bh - 1, fill);
        Put(tx, bh, ink); Put(tx + 1, bh, fill); Put(tx + 2, bh, fill); Put(tx + 3, bh, ink);
        Put(tx + 1, bh + 1, ink); Put(tx + 2, bh + 1, fill); Put(tx + 3, bh + 1, ink);
        Put(tx + 2, bh + 2, ink);
        // texte : la ligne 2 de la police (haut des capitales) sur la ligne 3 de la bulle
        PixelFont.Draw(sayText, 4, 1, (px, py) => Put(px, py, ink));
    }
}

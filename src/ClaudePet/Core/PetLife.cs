using ClaudePet.Art;
using ClaudePet.Util;

namespace ClaudePet.Core;

// Petites occupations de la mascotte (une tasse de café, son ordinateur portable) et son humeur, qui dépend
// des caresses d'Ixorak : content juste après, moyen ensuite, triste quand ça fait longtemps.
internal sealed partial class Pet
{
    // ------------------------------------------------------------------ humeur

    /// <summary>Content pendant 15 min après une caresse, moyen jusqu'à 1 h 30, triste ensuite.</summary>
    public const double HappyFor = 15 * 60, SadAfter = 90 * 60;

    /// <summary>La bouche de son humeur, quand rien d'autre ne lui en donne une.</summary>
    private Mouth MoodMouth => app.LonelySeconds < HappyFor ? Mouth.Smile
        : app.LonelySeconds < SadAfter ? Mouth.Flat : Mouth.Frown;

    public string MoodName => app.LonelySeconds < HappyFor ? "content" : app.LonelySeconds < SadAfter ? "moyen" : "triste";

    /// <summary>Il est occupé à une de ses petites activités (café, ordinateur, musique, lecture, bulles).</summary>
    private bool BusyWithActivity => State is St.Coffee or St.Laptop or St.Music or St.Read or St.Bubbles;

    // ------------------------------------------------------------------ café

    private const double SipLen = 1.5;      // lever la tasse (0,3 s), boire (0,9 s), la reposer (0,3 s)
    private double coffeeNextSip, coffeeSipAt = -10, coffeeNextSteam;
    private bool coffeeSlurp;

    /// <summary>Il s'assoit et sort une tasse de café fumante ; il en boit quelques gorgées.</summary>
    private bool StartCoffee()
    {
        double now = Clock.Now;
        coffeeNextSip = now + Rng.Range(1.0, 2.0);
        coffeeSipAt = -10;
        coffeeSlurp = false;
        coffeeNextSteam = now + 0.3;
        lookX = 0; lookY = 0;
        VX = 0;
        app.Play(Snd.Click, 0.3);
        Enter(St.Coffee, Rng.Range(10, 18));
        return true;
    }

    private void UpdateCoffee(double dt, double now)
    {
        if (!CheckSupport()) return;
        if (T >= Dur)
        {
            app.Play(Snd.Click, 0.25); // il range sa tasse
            Enter(St.Idle, Rng.Range(0.5, 1.2));
            return;
        }
        bool sipping = now - coffeeSipAt < SipLen;
        if (!sipping && now >= coffeeNextSip && T < Dur - SipLen - 0.5)
        {
            coffeeSipAt = now;
            coffeeSlurp = true;
            coffeeNextSip = now + SipLen + Rng.Range(1.8, 3.4);
            sipping = true;
        }
        if (coffeeSlurp && now - coffeeSipAt >= 0.3)
        {
            coffeeSlurp = false;
            app.Play(Snd.Sip, 0.4); // la tasse arrive à la bouche
        }
        // de la vapeur monte de la tasse tant qu'elle est sur ses genoux
        if (!sipping && now >= coffeeNextSteam)
        {
            coffeeNextSteam = now + Rng.Range(0.35, 0.6);
            if (lastPose.Prop == Prop.Mug)
            {
                var (mx, my) = PetPainter.MugTop(lastPose);
                double sx = X + Facing * (mx + 0.5) * RenderScale, sy = Y + my * RenderScale;
                app.Fx.Add(FxKind.Steam, sx + Rng.Range(-1.2, 1.2) * RenderScale, sy - RenderScale,
                    Rng.Range(-6, 6) * K, -Rng.Range(26, 38) * K, Rng.Range(1.1, 1.6));
            }
        }
        if (!sipping && now >= nextLookChange)
        {
            nextLookChange = now + Rng.Range(1.5, 3.5);
            lookX = Rng.Int(-1, 2);
        }
    }

    // ------------------------------------------------------------------ ordinateur

    private double lapBurstEnd, lapNextKey, lapPauseEnd, lapNextSpark;
    private int lapKey;
    private bool lapTyping;

    /// <summary>Il sort son petit ordinateur et « claude » dessus : des rafales de frappe, des pauses pour réfléchir.</summary>
    private bool StartLaptop()
    {
        double now = Clock.Now;
        lapTyping = false;
        lapPauseEnd = now + 0.9;              // le temps d'ouvrir l'écran
        lapNextSpark = now + Rng.Range(1.5, 3.5);
        lapKey = 1;
        lookX = 0; lookY = 0;
        VX = 0;
        app.Play(Snd.Click, 0.3);
        Enter(St.Laptop, Rng.Range(14, 26));
        return true;
    }

    /// <summary>Écran : 0 fermé, 1 à moitié, 2 ouvert (il s'ouvre au début et se referme à la fin).</summary>
    private int LaptopLid => T < 0.3 ? 0 : T < 0.55 ? 1 : T < Dur - 0.5 ? 2 : T < Dur - 0.25 ? 1 : 0;

    private void UpdateLaptop(double dt, double now)
    {
        if (!CheckSupport()) return;
        if (T >= Dur) { Enter(St.Idle, Rng.Range(0.5, 1.2)); return; }
        if (T >= Dur - 0.5 && T - dt < Dur - 0.5) app.Play(Snd.Click, 0.25); // il referme l'écran
        if (LaptopLid < 2) { lapTyping = false; return; }
        if (lapTyping)
        {
            if (now >= lapNextKey)
            {
                lapNextKey = now + Rng.Range(0.09, 0.16);
                lapKey = lapKey == 1 ? 2 : 1;
                if (Rng.Chance(0.75)) app.Play(Snd.Type, 0.18);
            }
            if (now >= lapBurstEnd)
            {
                // une pause pour réfléchir : il lève les yeux ; parfois une petite étoile monte de l'écran
                lapTyping = false;
                lapPauseEnd = now + Rng.Range(0.7, 2.0);
                lookX = Rng.Int(-1, 2);
                lookY = Rng.Chance(0.5) ? -1 : 0;
                if (now >= lapNextSpark)
                {
                    lapNextSpark = now + Rng.Range(2.5, 5.0);
                    app.Fx.Add(FxKind.Claude, X + Rng.Range(-3, 3) * RenderScale, Y - 10 * RenderScale,
                        Rng.Range(-10, 10) * K, -Rng.Range(40, 55) * K, Rng.Range(1.4, 1.9));
                }
            }
        }
        else if (now >= lapPauseEnd)
        {
            lapTyping = true;
            lapBurstEnd = now + Rng.Range(1.2, 3.2);
            lapNextKey = now;
            lookX = 0; lookY = 0;
        }
    }

    // ------------------------------------------------------------------ musique

    private const double Beat = 0.5;        // 120 battements par minute
    private double musicStart, musicNextNote;

    /// <summary>Il met son casque et écoute de la musique : il bat la mesure, danse un peu, des notes s'envolent.</summary>
    private bool StartMusic()
    {
        double now = Clock.Now;
        musicStart = now;
        musicNextNote = now + 0.6;
        lookX = 0; lookY = 0;
        VX = 0;
        app.Play(Snd.Click, 0.25);
        Enter(St.Music, Rng.Range(10, 18));
        return true;
    }

    private void UpdateMusic(double dt, double now)
    {
        if (!CheckSupport()) return;
        if (T >= Dur) { Enter(St.Idle, Rng.Range(0.5, 1.2)); return; }
        if (now >= musicNextNote)
        {
            musicNextNote = now + Rng.Range(0.6, 1.2);
            HeadPoint(out double hx, out double hy);
            int side = Rng.Sign();
            app.Fx.Add(FxKind.Note, hx + side * BodyW * Rng.Range(0.35, 0.55), hy - Scale * 2,
                side * Rng.Range(10, 30) * K, -Rng.Range(45, 70) * K, Rng.Range(1.2, 1.7));
        }
    }

    // ------------------------------------------------------------------ lecture

    private double readNextTurn, readTurnAt = -10, readNextLine, readLaughUntil;

    /// <summary>Il s'assoit avec un livre : ses yeux suivent les lignes, il tourne les pages, parfois il rit.</summary>
    private bool StartRead()
    {
        double now = Clock.Now;
        readNextTurn = now + Rng.Range(2.5, 4.5);
        readTurnAt = -10;
        readNextLine = now + 0.6;
        readLaughUntil = 0;
        lookX = -1; lookY = 1;
        VX = 0;
        app.Play(Snd.Click, 0.25);
        Enter(St.Read, Rng.Range(12, 22));
        return true;
    }

    private void UpdateRead(double dt, double now)
    {
        if (!CheckSupport()) return;
        if (T >= Dur) { app.Play(Snd.Click, 0.25); Enter(St.Idle, Rng.Range(0.5, 1.2)); return; }
        if (now >= readNextTurn && T < Dur - 1)
        {
            readTurnAt = now;
            readNextTurn = now + Rng.Range(2.8, 5.0);
            app.Play(Snd.Page, 0.35);
            lookX = -1; // il reprend en haut de la page de gauche
            if (Rng.Chance(0.25)) readLaughUntil = now + 1.0; // un passage drôle
        }
        // ses yeux suivent les lignes : de gauche à droite, puis retour à la ligne
        if (now >= readNextLine)
        {
            readNextLine = now + Rng.Range(0.5, 0.9);
            lookX = lookX >= 1 ? -1 : lookX + 1;
        }
    }

    // ------------------------------------------------------------------ bulles de savon

    private double bubNextBlow, bubBlowAt = -10;

    /// <summary>Il souffle des bulles de savon avec une petite baguette.</summary>
    private bool StartBubbles()
    {
        double now = Clock.Now;
        bubNextBlow = now + 0.9;
        bubBlowAt = -10;
        lookX = 0; lookY = 0;
        VX = 0;
        app.Play(Snd.Click, 0.25);
        Enter(St.Bubbles, Rng.Range(8, 14));
        return true;
    }

    private void UpdateBubbles(double dt, double now)
    {
        if (!CheckSupport()) return;
        if (T >= Dur) { Enter(St.Idle, Rng.Range(0.5, 1.2)); return; }
        if (now >= bubNextBlow && T < Dur - 0.6)
        {
            bubBlowAt = now;
            bubNextBlow = now + Rng.Range(1.4, 2.6);
            app.Play(Snd.Whoosh, 0.2);
            var (rx, ry) = PetPainter.WandRing(lastPose);
            double sx = X + Facing * (rx + 1) * RenderScale, sy = Y + (ry + 0.5) * RenderScale;
            int n = Rng.Int(2, 6);
            for (int i = 0; i < n; i++)
                app.Fx.Add(FxKind.Bubble, sx, sy, Facing * Rng.Range(35, 120) * K, -Rng.Range(10, 70) * K, Rng.Range(1.6, 3.2),
                    variant: Rng.Int(0, 2), gravity: -18 * K, drag: 0.9);
        }
    }

    // ------------------------------------------------------------------ poses

    private PetPose ActivityPose()
    {
        double now = Clock.Now;
        switch (State)
        {
            case St.Music:
            {
                double b = (now - musicStart) / Beat;
                int beat = (int)Math.Floor(b);
                bool down = b - beat < 0.35;                 // sur le temps, il s'écrase un peu
                bool dance = ((int)(T / 4)) % 2 == 1;        // il bat la mesure, puis il danse, tour à tour
                bool bliss = (beat / 4) % 2 == 1;            // les yeux fermés de bonheur, quatre temps sur huit
                bool even = (beat & 1) == 0;
                return PetPose.Stand with
                {
                    Prop = Prop.Headphones,
                    BodyBottom = down ? -5 : -6, BodyH = down ? 15 : 16,
                    LiftA = even && down ? 1 : 0, LiftB = !even && down ? 1 : 0,
                    DX = dance ? (even ? 1 : -1) : 0,
                    L = dance ? (even ? Arm.Raised : Arm.SideUp) : Arm.Side,
                    R = dance ? (even ? Arm.SideUp : Arm.Raised) : Arm.Side,
                    Eyes = bliss ? Eyes.Happy : Eyes.Open,
                    Mouth = bliss ? Mouth.Smile : Mouth.None,
                };
            }
            case St.Read:
            {
                bool turning = now - readTurnAt < 0.3, laugh = now < readLaughUntil;
                return PetPose.Sitting with
                {
                    Prop = Prop.Book, PropF = turning ? 1 : 0,
                    LookX = laugh ? 0 : lookX, LookY = 1,
                    Eyes = laugh ? Eyes.Happy : Eyes.Open,
                    Mouth = laugh ? Mouth.Grin : Mouth.None,
                    L = Arm.Hidden, R = Arm.Hidden,
                };
            }
            case St.Bubbles:
            {
                double since = now - bubBlowAt, until = bubNextBlow - now;
                bool blowing = since < 0.4, film = !blowing && until > 0 && until < 0.6;
                bool watching = since >= 0.4 && since < 1.4;  // il regarde ses bulles s'envoler
                return PetPose.Stand with
                {
                    Prop = Prop.Wand, PropF = film ? 1 : 0,
                    Mouth = blowing || film ? Mouth.SmallO : Mouth.None,
                    LookX = watching ? 1 : 0, LookY = watching ? -1 : 0,
                    R = Arm.Hidden,
                };
            }
        }
        if (State == St.Coffee)
        {
            double s = Clock.Now - coffeeSipAt;
            int f = 0;
            Eyes eyes = Eyes.Open;
            Mouth mouth = Mouth.None; // la bouche de son humeur
            if (s >= 0 && s < SipLen)
            {
                f = s < 0.3 ? 1 : s < 1.2 ? 2 : 1;
                if (f == 2) eyes = Eyes.Happy;
            }
            else if (s >= SipLen && s < SipLen + 0.6) { eyes = Eyes.Happy; mouth = Mouth.Smile; } // « aaah »
            return PetPose.Sitting with
            {
                Prop = Prop.Mug, PropF = f, Eyes = eyes, Mouth = mouth, LookX = f == 0 ? lookX : 0,
                L = Arm.Hidden, R = Arm.Hidden,
            };
        }
        // l'ordinateur
        int lid = LaptopLid;
        bool open = lid == 2;
        bool thinking = open && !lapTyping;
        return PetPose.Stand with
        {
            BodyBottom = -4, Legs = Legs.Kneel,
            L = lid > 0 ? Arm.Hidden : Arm.Down, R = lid > 0 ? Arm.Hidden : Arm.Down,
            Prop = Prop.Laptop, PropF = lid,
            PropG = lapTyping ? lapKey : thinking && lookY < 0 ? 3 : 0,
            LookX = open ? lookX : 0, LookY = open ? Math.Min(0, lookY) : 0,
            Mouth = thinking && lookY < 0 ? Mouth.SmallO : Mouth.None,
        };
    }
}

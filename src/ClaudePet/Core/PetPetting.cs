using ClaudePet.Art;
using ClaudePet.Util;

namespace ClaudePet.Core;

// Caresses : Ixorak maintient le clic droit sur Pet et fait des va-et-vient dessus.
// Pet ferme les yeux de bonheur, rougit, se penche vers la main, ronronne et fait des cœurs.
internal sealed partial class Pet
{
    private bool petting;
    private double petLastX, petLastY, petTravel, petSquishAt = -10, nextPetHeart, nextPurr, petTestUntil;
    private int petDirX, petDirY, petStrokes, petCalmStrokes;

    public bool IsPetting => petting;

    /// <summary>Clic droit enfoncé sur un pixel visible de Pet : il se laisse caresser.</summary>
    public bool StartPetting(double now)
    {
        if (homeBound || pressing || OnCursor) return false;
        if (State is St.Hidden or St.Air or St.Dragged or St.HangCursor or St.ClimbCursor or St.SitCursor or St.JumpPrep
            or St.HomeJump or St.EnterDoor or St.ExitDoor or St.Dig or St.Underground or St.Emerge or St.Climb or St.Eat) return false;
        if (State == St.Sleep) WakeUp();
        petting = true;
        var c = app.Cursor;
        petLastX = c.X; petLastY = c.Y;
        petTravel = 0;
        petDirX = petDirY = 0;
        petStrokes = 0;
        clickPending = false;
        VX = 0;
        Enter(St.Petted);
        return true;
    }

    /// <summary>Le pointeur bouge pendant la caresse : chaque aller ou retour sur son corps est une caresse.</summary>
    public void OnPettingMove(double x, double y, double now)
    {
        if (!petting || State != St.Petted) return;
        double dx = x - petLastX, dy = y - petLastY;
        petLastX = x; petLastY = y;
        if (Math.Abs(dx) + Math.Abs(dy) < 1) return;
        if (!OverBody(x, y)) return;
        petTravel += MathX.Hypot(dx, dy);
        // un changement de sens (de gauche à droite, ou de haut en bas) après un petit trajet = une caresse
        int sx = Math.Abs(dx) >= 1 ? Math.Sign(dx) : 0, sy = Math.Abs(dy) >= 1 ? Math.Sign(dy) : 0;
        bool turned = (sx != 0 && petDirX != 0 && sx != petDirX) || (sy != 0 && petDirY != 0 && sy != petDirY);
        if (sx != 0) petDirX = sx;
        if (sy != 0) petDirY = sy;
        if ((turned && petTravel > BodyW * 0.12) || petTravel > BodyW * 0.7)
        {
            petTravel = 0;
            Stroke(now);
        }
    }

    /// <summary>Le pointeur passe sur son corps (un peu de marge autour).</summary>
    private bool OverBody(double x, double y)
        => Math.Abs(x - X) < HW * 1.25 && y > Y - HH * 1.15 && y < Y + 4;

    private void Stroke(double now)
    {
        petStrokes++;
        petSquishAt = now;
        app.PettedNow(); // son humeur : content
        if (now >= nextPetHeart)
        {
            nextPetHeart = now + Rng.Range(0.4, 0.75);
            HeadPoint(out double hx, out double hy);
            app.Fx.Add(FxKind.Heart, hx + Rng.Range(-0.3, 0.3) * BodyW, hy - Scale * 2,
                Rng.Range(-25, 25) * K, -Rng.Range(55, 85) * K, Rng.Range(1.1, 1.5));
        }
        if (now >= nextPurr)
        {
            nextPurr = now + 0.95;
            app.Play(Snd.Purr, 0.45);
        }
        // énervé, il se calme au bout de quelques caresses
        if (Angry && ++petCalmStrokes >= 8) { petCalmStrokes = 0; CalmDown("caresses"); }
    }

    /// <summary>Clic droit relâché : un petit air comblé, puis il reprend sa vie.</summary>
    public void StopPetting(double now)
    {
        if (!petting) return;
        petting = false;
        if (State != St.Petted) return;
        if (petStrokes > 0)
        {
            app.Play(Snd.Happy, 0.45);
            HeadPoint(out double hx, out double hy);
            app.Fx.Add(FxKind.Heart, hx, hy - Scale * 2, 0, -70 * K, 1.4);
            Enter(St.Petted, 1.3);
        }
        else Enter(St.Idle, Rng.Range(0.4, 0.9)); // juste un clic droit, sans caresse
    }

    private void UpdatePetted(double dt, double now)
    {
        if (!CheckSupport()) { petting = false; return; }
        if (petTestUntil > 0)
        {
            // outil de test : le pointeur imaginaire caresse Pet
            if (now >= petTestUntil) { petTestUntil = 0; StopPetting(now); return; }
            OnPettingMove(app.Cursor.X, app.Cursor.Y, now);
        }
        if (petting)
        {
            // il se tourne vers la main qui le caresse
            double dx = app.Cursor.X - X;
            if (Math.Abs(dx) > HW * 0.6) Facing = Math.Sign(dx);
            return;
        }
        if (T >= Dur) Enter(St.Idle, Rng.Range(0.6, 1.4));
    }

    private PetPose PettedPose()
    {
        double now = Clock.Now;
        bool squish = now - petSquishAt < 0.14;
        if (!petting)
            return PetPose.Sitting with { Eyes = Eyes.Happy, Mouth = Mouth.Smile, Blush = true };
        // penché vers la main
        double dx = (app.Cursor.X - X) * Facing;
        int lean = dx > HW * 0.35 ? 1 : dx < -HW * 0.35 ? -1 : 0;
        int f = petStrokes & 1;
        return PetPose.Sitting with
        {
            DX = lean,
            BodyW = squish ? 26 : 24, BodyH = squish ? 14 : 16, BodyBottom = squish ? -2 : -3,
            Eyes = Eyes.Happy, Blush = true, Mouth = f == 0 ? Mouth.Grin : Mouth.Smile,
            L = Arm.Down.Shift(0, squish ? 1 : 0), R = Arm.Down.Shift(0, squish ? 1 : 0),
        };
    }
}

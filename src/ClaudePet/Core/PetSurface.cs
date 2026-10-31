using ClaudePet.Art;
using ClaudePet.Native;
using ClaudePet.Util;

namespace ClaudePet.Core;

// Surfaces sur lesquelles Pet se tient : le sol des écrans et le haut des fenêtres (étape 5),
// et les écrans en plein écran : creuser, ressortir sur un écran libre, ou se cacher.
internal sealed partial class Pet
{
    private nint supportHwnd;           // fenêtre sous ses pieds (0 : le sol)
    private RECT supportRect;
    private Platform pendingPlatform;
    private double supportMovedAt = -10;
    private bool hiddenForFullscreen;

    // saut vers une fenêtre : sert à savoir s'il l'a ratée
    private bool windowJump;
    private double windowJumpY;

    private WindowScanner Wins => app.Windows;

    public bool OnWindow => supportHwnd != 0;

    private bool SupportMovedRecently => Clock.Now - supportMovedAt < 0.6;

    // ------------------------------------------------------------------ plates-formes

    /// <summary>En descendant, Pet traverse-t-il le haut visible d'une fenêtre ? (on ne s'y pose que par le dessus)</summary>
    private partial bool PlatformCross(double fromY, double toY, out double top)
    {
        top = 0;
        var list = Wins.Platforms;
        double best = double.MaxValue;
        int bi = -1;
        for (int i = 0; i < list.Count; i++)
        {
            var p = list[i];
            if (X < p.X0 + 2 || X > p.X1 - 2) continue;
            if (fromY > p.Y + 0.5 || toY < p.Y) continue;
            if (p.Y >= best) continue;
            if (!FreeAt(X, p.Y)) continue; // pas la place pour la tête
            best = p.Y;
            bi = i;
        }
        if (bi < 0) return false;
        pendingPlatform = list[bi];
        top = best;
        return true;
    }

    /// <summary>Le morceau visible de la fenêtre sous ses pieds.</summary>
    private bool CurrentSegment(out double x0, out double x1)
    {
        x0 = x1 = 0;
        if (supportHwnd == 0) return false;
        foreach (var p in Wins.Platforms)
            if (p.Hwnd == supportHwnd && Math.Abs(p.Y - Y) < 3 && X >= p.X0 && X <= p.X1)
            {
                x0 = p.X0; x1 = p.X1;
                return true;
            }
        return false;
    }

    private partial bool OnPlatform() => CurrentSegment(out _, out _);

    private partial bool PlatformSupportsAt(double x)
        => CurrentSegment(out double x0, out double x1) && x >= x0 + 1 && x <= x1 - 1;

    private partial void LeaveSurface() { supportHwnd = 0; }

    private partial void AttachPlatformFromLanding()
    {
        supportHwnd = pendingPlatform.Hwnd;
        windowJump = false;
        if (!WindowScanner.Frame(supportHwnd, out supportRect)) { supportHwnd = 0; return; }
        if (Math.Abs(supportRect.Top - Y) > 1 && FreeAt(X, supportRect.Top)) Y = supportRect.Top;
    }

    private partial void AttachFloor()
    {
        supportHwnd = 0;
        windowJump = false;
    }

    private partial void OnLanded() { }

    /// <summary>La fenêtre sous ses pieds : Pet bouge avec elle, et tombe si elle est fermée, réduite ou agrandie.</summary>
    private void TrackSupport(double now)
    {
        if (supportHwnd == 0 || !Physical || State is St.Air or St.Climb) return;
        if (!WindowScanner.Frame(supportHwnd, out RECT r))
        {
            Log.Write("La fenêtre sous Pet a été fermée, réduite ou agrandie : il tombe");
            supportHwnd = 0;
            StartFall(0, 0);
            return;
        }
        if (r.Left == supportRect.Left && r.Top == supportRect.Top && r.Right == supportRect.Right) return;
        // déplacée (les deux bords bougent ensemble) ou seulement redimensionnée
        bool moved = r.Left - supportRect.Left == r.Right - supportRect.Right;
        double dx = moved ? r.Left - supportRect.Left : 0;
        Wins.Shift(supportHwnd, dx, r);
        supportRect = r;
        supportMovedAt = now;
        X += dx;
        targetX += dx;
        Y = r.Top;
        if (!FreeAt(X, Y))
        {
            // poussé contre le plafond ou hors des écrans : il tombe
            supportHwnd = 0;
            Resolve();
            StartFall(0, 0);
        }
    }

    // ------------------------------------------------------------------ sauts ratés

    private partial void AirTick(double dt)
    {
        if (windowJump && VY > 0 && Y > windowJumpY + HH * 0.5)
        {
            // raté : il passe sous le bord visé et tombe pour de bon (et sera sonné si c'est haut)
            windowJump = false;
            air = AirKind.Fall;
            app.Play(Snd.Fall, 0.4);
        }
    }

    // ------------------------------------------------------------------ plein écran

    /// <summary>L'écran de Pet est en plein écran : dès qu'il a les pieds au sol, il creuse et ressort ailleurs.</summary>
    private void CheckFullscreen()
    {
        if (!Scr.Fullscreen || homeBound) return;
        switch (State)
        {
            case St.Hidden: case St.Underground: case St.Dig: case St.Emerge:
            case St.ExitDoor: case St.HomeJump: case St.EnterDoor:
            case St.Air: case St.Dragged: case St.JumpPrep:
            case St.HangCursor: case St.ClimbCursor: case St.SitCursor:
                return; // il attend d'avoir les pieds au sol
            case St.Climb:
                StartFall(0, 0);
                return;
        }
        if (!Supported()) { StartFall(0, 0); return; }
        var dest = Desk.NearestFree(X, Y - HH * 0.5);
        emergeScreen = dest;
        emergeX = dest == null ? X : EmergeXToward(dest);
        if (dest == null && app.FeedingActive) app.CancelFeeding();
        Log.Write(dest == null ? "Tous les écrans sont en plein écran : Pet creuse et se cache"
                               : $"Plein écran : Pet creuse et ressort sur l'écran {dest.Index}");
        StartDig(quit: false);
    }

    /// <summary>Où ressortir sur un écran : près du bord le plus proche de Pet.</summary>
    private double EmergeXToward(Screen dest)
    {
        var w = dest.Work;
        if (dest.CenterX < X) return w.Right - BodyW * 1.5;
        if (dest.CenterX > X) return w.Left + BodyW * 1.5;
        return dest.CenterX;
    }

    /// <summary>Un écran a changé d'état : s'il se cachait parce que tout était en plein écran, il ressort.</summary>
    public void OnFullscreenChanged()
    {
        if (State != St.Hidden || !hiddenForFullscreen || homeBound) return;
        var s = Desk.NearestFree(X, Y - HH * 0.5);
        if (s == null) return;
        hiddenForFullscreen = false;
        EmergeAt(s, EmergeXToward(s));
    }

    // ------------------------------------------------------------------ divers

    private partial bool CanBeGrabbed() => (Physical || State == St.Dragged) && !homeBound;

    private partial void OnReleased() { }

    private partial void OnGoHomeStarted()
    {
        tunnelWalk = false;
        wantClimb = false;
        if (State == St.Climb) StartFall(0, 0);
    }

    private partial void OnUnderground() { }

    private partial void OnHiddenAll() { hiddenForFullscreen = true; }

    private partial PetPose ExtraPose(PetPose p, ref double clipY) => p;
}

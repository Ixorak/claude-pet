using ClaudePet.Art;
using ClaudePet.Gfx;
using ClaudePet.Native;
using ClaudePet.Util;

namespace ClaudePet.Core;

/// <summary>
/// La baie à donner à Pet (bulle « Nourrir »).
/// <list type="bullet">
/// <item>Tenue : elle pend au bout du pointeur et le suit. Sa fenêtre entoure le bout du pointeur d'une zone presque
/// invisible (alpha 1/255) qui reçoit les clics : le clic droit l'annule sans qu'aucun menu ne s'ouvre en dessous.</item>
/// <item>Clic gauche : Ixorak la pose, elle tombe au sol (Pet ira la chercher) et le pointeur est libre.</item>
/// <item>Posée, seuls ses pixels sont cliquables : clic gauche pour la reprendre, clic droit pour l'annuler.</item>
/// </list>
/// Aucun crochet global : tout passe par les messages de sa propre fenêtre.
/// </summary>
internal sealed unsafe class Berry
{
    public enum Mode : byte { None, Held, Falling, Lying }

    private readonly App app;
    public readonly LayeredWindow Win;
    public Mode State { get; private set; }

    /// <summary>Centre de la baie, en pixels écran.</summary>
    public double X, Y;
    private double vx, vy;
    private int k = 4, half = 72;
    private bool dirty, rightDown, hiddenOverFullscreen;
    private double lyingSince;

    // 9 x 10 : tige, feuille, baie ronde avec un reflet
    private static readonly string[] Art =
    {
        "....s.LL.",
        "....sLL..",
        "..KKsKK..",
        ".KRRRRRK.",
        "KRWRRRRRK",
        "KRWRRRRRK",
        "KRRRRRRRK",
        "KRRRRRDRK",
        ".KRRRDRK.",
        "..KKKKK..",
    };
    private const int ArtW = 9, ArtH = 10;
    // (déclarée avant Px : les champs statiques s'initialisent dans l'ordre du fichier)
    public static readonly uint Juice = Col.Rgb(0xE0, 0x40, 0x5A);
    private static readonly uint[] Px = BuildArt();

    private static uint[] BuildArt()
    {
        var px = new uint[ArtW * ArtH];
        for (int y = 0; y < ArtH; y++)
            for (int x = 0; x < ArtW; x++)
                px[y * ArtW + x] = Art[y][x] switch
                {
                    'K' => Col.Rgb(0x4A, 0x10, 0x20),
                    'R' => Juice,
                    'W' => Col.Rgb(0xFF, 0xC4, 0xCC),
                    'D' => Col.Rgb(0xB0, 0x2A, 0x45),
                    's' => Col.Rgb(0x5A, 0x6A, 0x2A),
                    'L' => Col.Rgb(0x6C, 0xC0, 0x4A),
                    _ => 0u,
                };
        return px;
    }

    public Berry(App app)
    {
        this.app = app;
        Win = new LayeredWindow(App.BerryClass, clickThrough: false);
    }

    public bool Active => State != Mode.None;
    public bool Held => State == Mode.Held;

    /// <summary>Visible pour Pet (pas cachée au-dessus d'un écran en plein écran).</summary>
    public bool Visible => Active && !hiddenOverFullscreen;

    /// <summary>Taille d'un pixel de la baie, en pixels écran.</summary>
    public int Pixel => k;

    // ------------------------------------------------------------------ cycle de vie

    public void Start()
    {
        State = Mode.Held;
        hiddenOverFullscreen = false;
        rightDown = false;
        Follow();
        dirty = true;
        app.Play(Snd.Bubble, 0.5);
        Log.Write("Baie : tenue au bout du pointeur");
    }

    /// <summary>Outil de test : la baie tombe d'un point de l'écran (sans prendre le pointeur).</summary>
    public void DropAt(double x, double y)
    {
        State = Mode.Falling;
        hiddenOverFullscreen = false;
        X = x; Y = y; vx = 0; vy = 0;
        dirty = true;
    }

    /// <summary>Pet l'a mangée.</summary>
    public void Eaten() => Stop();

    /// <summary>Annulée (clic droit) : petit nuage à sa place.</summary>
    public void Poof()
    {
        if (!Active) return;
        for (int i = 0; i < 5; i++)
            app.Fx.Add(FxKind.Puff, X + Rng.Range(-1, 1) * k * 3, Y + Rng.Range(-1, 1) * k * 3,
                Rng.Range(-60, 60) * k / 4.0, -Rng.Range(20, 80) * k / 4.0, Rng.Range(0.3, 0.5), 0, drag: 3);
        app.Play(Snd.Whoosh, 0.35);
        Stop();
    }

    private void Stop()
    {
        if (rightDown) { rightDown = false; User32.ReleaseCapture(); }
        State = Mode.None;
        Win.Hide();
    }

    // ------------------------------------------------------------------ boucle

    public double DesiredInterval => State is Mode.Held or Mode.Falling ? 1.0 / 60 : double.MaxValue;

    public void Update(double dt, double now)
    {
        if (!Active) return;
        int nk = Math.Max(2, (int)Math.Round(app.Pet.Scale * 0.6));
        if (nk != k) { k = nk; dirty = true; }
        switch (State)
        {
            case Mode.Held:
                Follow();
                break;
            case Mode.Falling:
                Fall(dt);
                break;
            case Mode.Lying:
                // une baie que Pet ne peut pas atteindre ne reste pas là pour toujours
                if (now - lyingSince > 150) { Log.Write("Baie : oubliée au sol, elle disparaît"); Poof(); return; }
                break;
        }
        Render();
    }

    /// <summary>Tenue : la baie pend au bout du pointeur (la tige juste sous la pointe de la flèche).</summary>
    private void Follow()
    {
        var c = app.Cursor;
        var s = app.Desk.At(c.X, c.Y);
        bool over = s != null && s.Fullscreen;
        if (over != hiddenOverFullscreen)
        {
            hiddenOverFullscreen = over;
            dirty = true;
        }
        half = (int)Math.Round(72 * (s?.Scale ?? 1));
        X = c.X - 1.0 * k;
        Y = c.Y + 5.0 * k;
    }

    private void Fall(double dt)
    {
        var s = app.Desk.At(X, Y) ?? app.Desk.Nearest(X, Y);
        double K = s.Scale;
        vy += 3600 * K * dt;
        vy = Math.Min(vy, 5200 * K);
        vx *= Math.Max(0, 1 - 0.5 * dt);
        double r = 4.5 * k;
        double nx = X + vx * dt;
        if (app.Desk.IsFree(nx - r, Y - r, nx + r, Y + r)) X = nx;
        else vx = -vx * 0.3;
        double floor = s.Work.Bottom;
        Y += vy * dt;
        if (Y + r >= floor)
        {
            Y = floor - r;
            if (vy > 380 * K)
            {
                vy = -vy * 0.3;
                vx *= 0.6;
                app.Play(Snd.Land, 0.15);
            }
            else
            {
                vy = 0; vx = 0;
                State = Mode.Lying;
                lyingSince = Clock.Now;
                dirty = true;
            }
        }
        if (Y - r < s.Work.Top) { Y = s.Work.Top + r; vy = Math.Abs(vy) * 0.3; }
    }

    // ------------------------------------------------------------------ dessin

    private void Render()
    {
        if (hiddenOverFullscreen) { Win.Hide(); return; }
        int sw = ArtW * k, sh = ArtH * k;
        int sx0 = (int)Math.Round(X - 4.5 * k), sy0 = (int)Math.Round(Y - 5.5 * k);
        if (State == Mode.Held)
        {
            var c = app.Cursor;
            int wx = (int)Math.Round(c.X) - half, wy = (int)Math.Round(c.Y) - half;
            int size = half * 2;
            if (dirty || !Win.Shown || Win.W != size)
            {
                dirty = false;
                uint* bits = Win.Begin(size, size);
                int stride = Win.Stride;
                // zone de clic presque invisible autour du bout du pointeur
                for (int y = 0; y < size; y++)
                {
                    uint* row = bits + y * stride;
                    for (int x = 0; x < size; x++) row[x] = 0x01000000u;
                }
                Blit.Sprite(Px, ArtW, ArtH, k, 1, bits, stride, size, size, sx0 - wx, sy0 - wy);
                Win.Commit(wx, wy, size, size);
            }
            else Win.Move(wx, wy);
            return;
        }
        // posée ou en chute : seuls les pixels de la baie
        if (dirty || !Win.Shown || Win.W != sw + 2)
        {
            dirty = false;
            uint* bits = Win.Begin(sw + 2, sh + 2);
            Blit.Sprite(Px, ArtW, ArtH, k, 1, bits, Win.Stride, sw + 2, sh + 2, 1, 1);
            Win.Commit(sx0 - 1, sy0 - 1, sw + 2, sh + 2);
        }
        else Win.Move(sx0 - 1, sy0 - 1);
    }

    // ------------------------------------------------------------------ souris (messages de sa fenêtre)

    public bool HandleMessage(nint hwnd, uint msg, nuint wp, nint lp, ref nint result)
    {
        if (hwnd != Win.Hwnd) return false;
        double now = Clock.Now;
        switch (msg)
        {
            case C.WM_MOUSEACTIVATE:
                result = C.MA_NOACTIVATE;
                return true;
            case C.WM_SETCURSOR:
                User32.SetCursor(User32.LoadCursor(0, State == Mode.Held ? C.IDC_ARROW : C.IDC_HAND));
                result = 1;
                return true;
            case C.WM_MOUSEMOVE:
                app.Cursor.Update(now);
                if (State == Mode.Held) { Follow(); Render(); }
                app.Wake();
                return true;
            case C.WM_LBUTTONDOWN:
                app.Cursor.Update(now);
                if (State == Mode.Held) Drop(now);
                else if (State is Mode.Lying or Mode.Falling) PickUp();
                app.Wake();
                return true;
            case C.WM_RBUTTONDOWN:
                rightDown = true;
                User32.SetCapture(Win.Hwnd);
                return true;
            case C.WM_RBUTTONUP:
                // le menu s'ouvre d'habitude au relâchement : on le garde pour nous, puis on annule
                if (rightDown)
                {
                    rightDown = false;
                    User32.ReleaseCapture();
                    app.CancelFeeding();
                }
                return true;
            case C.WM_CAPTURECHANGED:
                rightDown = false;
                return true;
            case C.WM_LBUTTONUP:
            case C.WM_MBUTTONDOWN:
            case C.WM_MBUTTONUP:
            case C.WM_CONTEXTMENU:
                return true;
        }
        return false;
    }

    /// <summary>Clic gauche : la baie est posée, elle tombe avec l'élan du pointeur.</summary>
    private void Drop(double now)
    {
        var (cvx, cvy) = app.Cursor.Velocity(now);
        double K = (app.Desk.At(X, Y) ?? app.Desk.Primary).Scale;
        double max = 2500 * K, sp = MathX.Hypot(cvx, cvy);
        if (sp > max) { cvx *= max / sp; cvy *= max / sp; }
        vx = cvx * 0.6;
        vy = cvy * 0.6;
        State = Mode.Falling;
        dirty = true;
        app.Play(Snd.Whoosh, 0.25);
        Log.Write("Baie : posée");
    }

    private void PickUp()
    {
        State = Mode.Held;
        Follow();
        dirty = true;
        app.Play(Snd.Click, 0.4);
    }
}

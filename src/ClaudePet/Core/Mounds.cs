using ClaudePet.Art;
using ClaudePet.Gfx;
using ClaudePet.Util;

namespace ClaudePet.Core;

/// <summary>
/// Les mottes de terre laissées quand Pet creuse : une au début du trou, une là où il ressort du sol.
/// Elles sortent du sol, restent quelques secondes, puis s'effacent. Chacune a sa petite fenêtre transparente
/// (les clics passent au travers), redessinée seulement pendant qu'elle pousse ou s'efface.
/// </summary>
internal sealed unsafe class Mounds
{
    private sealed class Mound
    {
        public LayeredWindow Win = null!;
        public double X, Floor, Born, Life;
        public int K, LastRows = -1, LastAlpha = -1;
        public bool Active;
    }

    private const int Max = 4;
    private const double Grow = 0.35, Fade = 1.5;
    private readonly List<Mound> pool = new();

    // un seul tas de terre irrégulier, plus haut à gauche, avec une petite motte qui a roulé à droite (24 x 8)
    private static readonly string[] Art =
    {
        "..........KKK...........",
        "........KKLLmKK.........",
        "......KKLLmmmmmK........",
        "....KKLmmmmdmmmmKK......",
        "...KLLmmmmmmmmdmmmKK.KK.",
        "..KLmmdmmmmmmmmmmmmmKKdK",
        ".KLmmmmmmmdmmmmmdmmmmmmK",
        "KKKKKKKKKKKKKKKKKKKKKKKK",
    };
    private const int ArtW = 24, ArtH = 8;
    private static readonly uint[] Px = Build();

    private static uint[] Build()
    {
        var px = new uint[ArtW * ArtH];
        for (int y = 0; y < ArtH; y++)
            for (int x = 0; x < ArtW; x++)
                px[y * ArtW + x] = Art[y][x] switch
                {
                    'K' => Col.Rgb(0x3A, 0x22, 0x10),
                    'm' => Col.Rgb(0x7A, 0x4A, 0x22),
                    'L' => Col.Rgb(0x9B, 0x63, 0x34),
                    'd' => Col.Rgb(0x5A, 0x35, 0x18),
                    'H' => Col.Rgb(0x1E, 0x12, 0x08),
                    _ => 0u,
                };
        return px;
    }

    /// <summary>Une motte posée sur le sol, centrée en x. <paramref name="life"/> : durée totale, effacement compris.</summary>
    public void Add(double x, double floor, int k, double life)
    {
        Mound? m = null;
        foreach (var p in pool) if (!p.Active) { m = p; break; }
        if (m == null)
        {
            if (pool.Count < Max) { m = new Mound { Win = new LayeredWindow(App.FxClass, clickThrough: true) }; pool.Add(m); }
            else foreach (var p in pool) if (m == null || p.Born < m.Born) m = p; // la plus ancienne laisse sa place
        }
        m!.X = x; m.Floor = floor; m.K = Math.Max(2, k);
        m.Born = Clock.Now; m.Life = Math.Max(Grow + Fade, life);
        m.Active = true; m.LastRows = -1; m.LastAlpha = -1;
        Render(m, m.Born);
    }

    /// <summary>Rapide pendant qu'une motte pousse ou s'efface ; sinon on attend le début de l'effacement.</summary>
    public double DesiredInterval
    {
        get
        {
            double d = double.MaxValue, now = Clock.Now;
            foreach (var m in pool)
            {
                if (!m.Active) continue;
                double age = now - m.Born;
                if (age < Grow) d = Math.Min(d, 1.0 / 30);
                else if (age > m.Life - Fade) d = Math.Min(d, 1.0 / 15);
                else d = Math.Min(d, Math.Max(0.05, m.Life - Fade - age));
            }
            return d;
        }
    }

    public void Update(double now)
    {
        foreach (var m in pool)
        {
            if (!m.Active) continue;
            if (now - m.Born >= m.Life) { m.Active = false; m.Win.Hide(); continue; }
            Render(m, now);
        }
    }

    private static void Render(Mound m, double now)
    {
        double age = now - m.Born;
        int rows = Math.Clamp((int)Math.Ceiling(ArtH * Math.Min(1, age / Grow)), 1, ArtH); // elle sort du sol
        int alpha = (int)Math.Round(255 * MathX.Clamp((m.Life - age) / Fade, 0, 1));
        if (rows == m.LastRows && Math.Abs(alpha - m.LastAlpha) < 8) return;
        m.LastRows = rows; m.LastAlpha = alpha;
        int k = m.K, w = ArtW * k, h = rows * k;
        uint* bits = m.Win.Begin(w, h);
        Blit.Sprite(Px, ArtW, ArtH, k, alpha / 255.0, bits, m.Win.Stride, w, h, 0, 0);
        m.Win.Commit((int)Math.Round(m.X - w / 2.0), (int)Math.Round(m.Floor) - h, w, h);
    }

    public void BringToTop()
    {
        foreach (var m in pool) if (m.Active) m.Win.BringToTop();
    }

    public void Shutdown()
    {
        foreach (var m in pool) { m.Win.Hide(); Native.User32.DestroyWindow(m.Win.Hwnd); }
        pool.Clear();
    }
}

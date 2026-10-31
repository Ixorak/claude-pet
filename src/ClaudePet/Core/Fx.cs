using ClaudePet.Art;
using ClaudePet.Gfx;
using ClaudePet.Util;

namespace ClaudePet.Core;

internal enum FxKind : byte { Spark, Dust, Zed, Smoke, Dirt, Crumb, Heart, Sweat, Puff, Note }

internal struct Particle
{
    public FxKind Kind;
    public double X, Y, VX, VY, T, Life, Gravity, Drag;
    public int Variant;
    public uint Color;
    public double Wobble;
}

/// <summary>Petits sprites d'effets (pixels « art »), avec liseré calculé.</summary>
internal static class FxSprites
{
    public sealed class Sprite
    {
        public readonly int W, H;
        public readonly uint[] Px;
        public Sprite(int w, int h, uint[] px) { W = w; H = h; Px = px; }
    }

    private static Sprite Make(string[] rows, Func<char, uint> pal, uint outline)
    {
        int w = rows[0].Length + 2, h = rows.Length + 2;
        var c = new ArtCanvas(w, h, 0, 0);
        for (int y = 0; y < rows.Length; y++)
            for (int x = 0; x < rows[y].Length; x++)
            {
                uint col = pal(rows[y][x]);
                if ((col >> 24) != 0) c.Set(x + 1, y + 1, col);
            }
        if ((outline >> 24) != 0) c.Outline(outline);
        return new Sprite(w, h, c.Px);
    }

    private static uint P(char ch) => ch switch
    {
        'W' => Col.Rgb(255, 255, 255),
        'Y' => Col.Rgb(0xFF, 0xD2, 0x3F),
        'L' => Col.Rgb(0xFF, 0xF4, 0xB0),
        'R' => Col.Rgb(0xFF, 0x5C, 0x7A),
        'P' => Col.Rgb(0xFF, 0xB3, 0xC4),
        'B' => Col.Rgb(0x7F, 0xD3, 0xFF),
        'b' => Col.Rgb(0xD8, 0xF2, 0xFF),
        'G' => Col.Rgb(0xEC, 0xE6, 0xDE),
        'g' => Col.Rgb(0xC4, 0xBB, 0xAE),
        'S' => Col.Rgb(0x8E, 0x8E, 0x92),
        's' => Col.Rgb(0xB8, 0xB8, 0xBC),
        'K' => Col.Rgb(0x30, 0x2A, 0x3A),
        _ => 0,
    };

    public static readonly Sprite Star = Make(new[] { "..Y..", ".YLY.", "YLLLY", ".YLY.", "..Y.." }, P, Col.Rgb(0x7A, 0x4F, 0x0A));
    public static readonly Sprite StarSmall = Make(new[] { ".Y.", "YLY", ".Y." }, P, Col.Rgb(0x7A, 0x4F, 0x0A));
    public static readonly Sprite ZedBig = Make(new[] { "WWWWW", "...W.", "..W..", ".W...", "WWWWW" }, P, Col.Rgb(0x3B, 0x4A, 0x66));
    public static readonly Sprite ZedMid = Make(new[] { "WWWW", "..W.", ".W..", "WWWW" }, P, Col.Rgb(0x3B, 0x4A, 0x66));
    public static readonly Sprite ZedSmall = Make(new[] { "WWW", ".W.", "WWW" }, P, Col.Rgb(0x3B, 0x4A, 0x66));
    public static readonly Sprite Heart = Make(new[] { ".R.R.", "RPRRR", "RRRRR", ".RRR.", "..R.." }, P, Col.Rgb(0x7A, 0x1A, 0x2C));
    public static readonly Sprite Sweat = Make(new[] { ".B.", "BbB", "BBB", ".B." }, P, Col.Rgb(0x2D, 0x6A, 0x94));
    public static readonly Sprite Dust = Make(new[] { ".gg.", "gGGg", "gGGg", ".gg." }, P, 0);
    public static readonly Sprite DustSmall = Make(new[] { "gG", "Gg" }, P, 0);
    public static readonly Sprite SmokeBig = Make(new[] { ".sss.", "sSSSs", "sSSSs", "sSSSs", ".sss." }, P, 0);
    public static readonly Sprite SmokeMid = Make(new[] { ".ss.", "sSSs", "sSSs", ".ss." }, P, 0);
    public static readonly Sprite Exclaim = Make(new[] { "YY", "YY", "YY", "..", "YY" }, P, Col.Rgb(0x8A, 0x1E, 0x14));
    public static readonly Sprite Question = Make(new[] { ".YYY.", "Y...Y", "...Y.", "..Y..", ".....", "..Y.." }, P, Col.Rgb(0x8A, 0x1E, 0x14));
    public static readonly Sprite Note = Make(new[] { "..KK", "..K.", "..K.", "KKK.", "KK.." }, P, Col.Rgb(255, 255, 255));
}

/// <summary>Les effets autour de Pet (étoiles, poussière, zZz, fumée, terre...), dans une fenêtre qui laisse passer les clics.</summary>
internal sealed unsafe class Fx
{
    private readonly List<Particle> ps = new();
    private readonly LayeredWindow win;
    public bool Active => ps.Count > 0 || overlays;

    /// <summary>Cadence utile : rapide pour les particules qui volent, lente pour les étoiles qui tournent.</summary>
    public double DesiredInterval => ps.Count > 0 ? 1.0 / 60 : overlays ? 1.0 / 24 : double.MaxValue;
    private bool overlays;
    private bool wasShown;

    public Fx(LayeredWindow window) { win = window; }

    public void Clear() { ps.Clear(); }

    public void Add(FxKind kind, double x, double y, double vx, double vy, double life, int variant = 0,
        double gravity = 0, double drag = 0, uint color = 0)
    {
        if (ps.Count > 220) ps.RemoveAt(0);
        ps.Add(new Particle
        {
            Kind = kind, X = x, Y = y, VX = vx, VY = vy, Life = life, Variant = variant,
            Gravity = gravity, Drag = drag, Color = color, Wobble = Rng.Range(0, 6.28),
        });
    }

    public void Update(double dt)
    {
        for (int i = ps.Count - 1; i >= 0; i--)
        {
            var p = ps[i];
            p.T += dt;
            if (p.T >= p.Life) { ps.RemoveAt(i); continue; }
            p.VY += p.Gravity * dt;
            if (p.Drag > 0) { double f = Math.Max(0, 1 - p.Drag * dt); p.VX *= f; p.VY *= f; }
            p.X += p.VX * dt;
            p.Y += p.VY * dt;
            ps[i] = p;
        }
    }

    // --- Rendu ---

    private struct Item { public FxSprites.Sprite? S; public double X, Y; public double A; public int K; public uint Solid; public int SW, SH; }
    private readonly List<Item> items = new();

    /// <summary>Dessine les particules et les surcouches (étoiles de Pet sonné...) dans la fenêtre d'effets.</summary>
    public void Render(Pet pet)
    {
        items.Clear();
        int k = Math.Max(2, (int)Math.Round(pet.RenderScale));
        int kSmall = Math.Max(2, (int)Math.Round(pet.RenderScale * 0.75));

        foreach (var p in ps)
        {
            double a = 1;
            double life = p.T / p.Life;
            switch (p.Kind)
            {
                case FxKind.Spark:
                    a = life > 0.7 ? (1 - life) / 0.3 : 1;
                    Add(p.Variant == 0 ? FxSprites.Star : FxSprites.StarSmall, p.X, p.Y, a, kSmall);
                    break;
                case FxKind.Dust:
                    a = 1 - life;
                    Add(p.Variant == 0 ? FxSprites.Dust : FxSprites.DustSmall, p.X, p.Y, a * 0.9, kSmall);
                    break;
                case FxKind.Puff:
                    a = 1 - life;
                    Add(FxSprites.Dust, p.X, p.Y, a * 0.8, k);
                    break;
                case FxKind.Zed:
                    a = life < 0.15 ? life / 0.15 : life > 0.7 ? (1 - life) / 0.3 : 1;
                    var zs = p.Variant switch { 0 => FxSprites.ZedSmall, 1 => FxSprites.ZedMid, _ => FxSprites.ZedBig };
                    Add(zs, p.X + Math.Sin(p.T * 3 + p.Wobble) * k * 1.5, p.Y, a, kSmall);
                    break;
                case FxKind.Smoke:
                    a = (1 - life) * 0.85;
                    Add(life < 0.5 ? FxSprites.SmokeMid : FxSprites.SmokeBig, p.X + Math.Sin(p.T * 4 + p.Wobble) * k, p.Y, a, kSmall);
                    break;
                case FxKind.Heart:
                    a = life > 0.7 ? (1 - life) / 0.3 : 1;
                    Add(FxSprites.Heart, p.X + Math.Sin(p.T * 5 + p.Wobble) * k, p.Y, a, kSmall);
                    break;
                case FxKind.Sweat:
                    a = 1 - life;
                    Add(FxSprites.Sweat, p.X, p.Y, a, kSmall);
                    break;
                case FxKind.Note:
                    a = life > 0.6 ? (1 - life) / 0.4 : 1;
                    Add(FxSprites.Note, p.X + Math.Sin(p.T * 4 + p.Wobble) * k, p.Y, a, kSmall);
                    break;
                case FxKind.Dirt:
                case FxKind.Crumb:
                    a = life > 0.75 ? (1 - life) / 0.25 : 1;
                    int sz = p.Variant == 0 ? k : Math.Max(2, k * 2 / 3);
                    items.Add(new Item { S = null, X = p.X, Y = p.Y, A = a, Solid = p.Color, SW = sz, SH = sz });
                    break;
            }
        }

        // Surcouches liées à Pet
        overlays = false;
        if (pet.ShowStars && pet.State != St.Hidden)
        {
            overlays = true;
            pet.HeadPoint(out double hx, out double hy);
            double rx = pet.RenderScale * 13, ry = pet.RenderScale * 3.5;
            double t = Clock.Now * 4.2;
            for (int i = 0; i < 3; i++)
            {
                double ang = t + i * Math.PI * 2 / 3;
                double sx = hx + Math.Cos(ang) * rx, sy = hy - pet.RenderScale * 4 + Math.Sin(ang) * ry;
                bool small = ((int)(Clock.Now * 8) + i) % 3 == 0;
                Add(small ? FxSprites.StarSmall : FxSprites.Star, sx, sy, 1, kSmall);
            }
        }
        if (pet.ExclaimUntil > Clock.Now && pet.State != St.Hidden)
        {
            overlays = true;
            pet.HeadPoint(out double hx, out double hy);
            var spr = pet.ExclaimQuestion ? FxSprites.Question : FxSprites.Exclaim;
            double bob = Math.Sin(Clock.Now * 14) * k * 0.5;
            Add(spr, hx + pet.Facing * pet.RenderScale * 9, hy - pet.RenderScale * 9 + bob, 1, k);
        }
        pet.ExtraOverlays(this, k);

        if (items.Count == 0)
        {
            if (wasShown) { win.Hide(); wasShown = false; }
            return;
        }

        // boîte englobante
        double x0 = double.MaxValue, y0 = double.MaxValue, x1 = double.MinValue, y1 = double.MinValue;
        foreach (var it in items)
        {
            double w = it.S != null ? it.S.W * it.K : it.SW, h = it.S != null ? it.S.H * it.K : it.SH;
            double ix = it.X - w / 2, iy = it.Y - h / 2;
            x0 = Math.Min(x0, ix); y0 = Math.Min(y0, iy);
            x1 = Math.Max(x1, ix + w); y1 = Math.Max(y1, iy + h);
        }
        int left = (int)Math.Floor(x0) - 2, top = (int)Math.Floor(y0) - 2;
        int dw = Math.Min(4000, (int)Math.Ceiling(x1) - left + 2), dh = Math.Min(3000, (int)Math.Ceiling(y1) - top + 2);
        if (dw <= 0 || dh <= 0) { win.Hide(); wasShown = false; return; }
        uint* bits = win.Begin(dw, dh);
        int stride = win.Stride;
        foreach (var it in items)
        {
            if (it.S != null)
            {
                int w = it.S.W * it.K, h = it.S.H * it.K;
                int ix = (int)Math.Round(it.X - w / 2.0) - left, iy = (int)Math.Round(it.Y - h / 2.0) - top;
                Blit.Sprite(it.S.Px, it.S.W, it.S.H, it.K, it.A, bits, stride, dw, dh, ix, iy);
            }
            else
            {
                int ix = (int)Math.Round(it.X - it.SW / 2.0) - left, iy = (int)Math.Round(it.Y - it.SH / 2.0) - top;
                Blit.FillRect(bits, stride, dw, dh, ix, iy, it.SW, it.SH, Col.WithAlpha(it.Solid, it.A));
            }
        }
        // bulles éventuelles (texte), dessinées par Pet
        pet.ExtraDraw(bits, stride, dw, dh, left, top);
        win.Commit(left, top, dw, dh);
        wasShown = true;
    }

    public void Add(FxSprites.Sprite s, double x, double y, double a, int k)
    {
        if (a <= 0.02) return;
        items.Add(new Item { S = s, X = x, Y = y, A = Math.Min(1, a), K = k });
    }

    /// <summary>Réserve une zone (pour un dessin libre fait par Pet dans ExtraDraw).</summary>
    public void Reserve(double x0, double y0, double x1, double y1)
    {
        items.Add(new Item { S = null, X = (x0 + x1) / 2, Y = (y0 + y1) / 2, A = 0, Solid = 0, SW = (int)(x1 - x0), SH = (int)(y1 - y0) });
    }
}

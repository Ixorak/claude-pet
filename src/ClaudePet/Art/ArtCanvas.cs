namespace ClaudePet.Art;

/// <summary>
/// Petite toile de pixels ARGB (couleurs non prémultipliées), avec une origine :
/// les coordonnées de dessin sont relatives à l'origine (les pieds de Pet, par exemple).
/// </summary>
internal sealed class ArtCanvas
{
    public readonly int W, H, OX, OY;
    public readonly uint[] Px;
    private readonly byte[] mask;

    public ArtCanvas(int w, int h, int ox, int oy)
    {
        W = w; H = h; OX = ox; OY = oy;
        Px = new uint[w * h];
        mask = new byte[w * h];
    }

    public void Clear() => Array.Clear(Px);

    public void Set(int x, int y, uint c)
    {
        int ix = x + OX, iy = y + OY;
        if ((uint)ix < (uint)W && (uint)iy < (uint)H) Px[iy * W + ix] = c;
    }

    /// <summary>Pose la couleur seulement là où il y a déjà quelque chose d'opaque.</summary>
    public void SetIfOpaque(int x, int y, uint c)
    {
        int ix = x + OX, iy = y + OY;
        if ((uint)ix < (uint)W && (uint)iy < (uint)H && (Px[iy * W + ix] >> 24) != 0) Px[iy * W + ix] = c;
    }

    public uint Get(int x, int y)
    {
        int ix = x + OX, iy = y + OY;
        return (uint)ix < (uint)W && (uint)iy < (uint)H ? Px[iy * W + ix] : 0;
    }

    public void Rect(int x0, int y0, int x1, int y1, uint c)
    {
        if (x1 < x0) (x0, x1) = (x1, x0);
        if (y1 < y0) (y0, y1) = (y1, y0);
        for (int y = y0; y <= y1; y++)
            for (int x = x0; x <= x1; x++)
                Set(x, y, c);
    }

    /// <summary>Trait épais (pinceau carré) de (x0,y0) à (x1,y1). Le pinceau couvre [p-1, p+size-2].</summary>
    public void Stroke(int x0, int y0, int x1, int y1, int size, uint c)
    {
        int dx = Math.Abs(x1 - x0), sx = x0 < x1 ? 1 : -1;
        int dy = -Math.Abs(y1 - y0), sy = y0 < y1 ? 1 : -1;
        int err = dx + dy;
        int lo = size >= 2 ? -1 : 0;
        int hi = lo + size - 1;
        while (true)
        {
            for (int yy = lo; yy <= hi; yy++)
                for (int xx = lo; xx <= hi; xx++)
                    Set(x0 + xx, y0 + yy, c);
            if (x0 == x1 && y0 == y1) break;
            int e2 = 2 * err;
            if (e2 >= dy) { err += dy; x0 += sx; }
            if (e2 <= dx) { err += dx; y0 += sy; }
        }
    }

    /// <summary>Entoure d'un liseré tout ce qui est opaque (voisinage à 4 : coins arrondis).</summary>
    public void Outline(uint color)
    {
        for (int i = 0; i < Px.Length; i++) mask[i] = (byte)((Px[i] >> 24) != 0 ? 1 : 0);
        for (int y = 0; y < H; y++)
        {
            int row = y * W;
            for (int x = 0; x < W; x++)
            {
                int i = row + x;
                if (mask[i] != 0) continue;
                if ((x > 0 && mask[i - 1] != 0) || (x < W - 1 && mask[i + 1] != 0) ||
                    (y > 0 && mask[i - W] != 0) || (y < H - 1 && mask[i + W] != 0))
                    Px[i] = color;
            }
        }
    }

    /// <summary>Miroir horizontal autour de l'axe de l'origine (x ↔ -1-x).</summary>
    public void MirrorX()
    {
        for (int y = 0; y < H; y++)
        {
            int row = y * W;
            for (int x = 0; x < OX; x++)
            {
                int a = row + x, b = row + (2 * OX - 1 - x);
                if ((uint)(2 * OX - 1 - x) >= (uint)W) continue;
                (Px[a], Px[b]) = (Px[b], Px[a]);
            }
        }
    }

    /// <summary>Boîte englobante des pixels non transparents, en indices de tableau. Faux si vide.</summary>
    public bool Bounds(out int x0, out int y0, out int x1, out int y1)
    {
        x0 = W; y0 = H; x1 = -1; y1 = -1;
        for (int y = 0; y < H; y++)
        {
            int row = y * W;
            for (int x = 0; x < W; x++)
            {
                if ((Px[row + x] >> 24) == 0) continue;
                if (x < x0) x0 = x;
                if (x > x1) x1 = x;
                if (y < y0) y0 = y;
                y1 = y;
            }
        }
        return x1 >= 0;
    }

    /// <summary>Recopie une grille texte (un caractère = un pixel) avec une palette.</summary>
    public void Sprite(int x0, int y0, string[] rows, Func<char, uint> palette, bool mirror = false)
    {
        for (int r = 0; r < rows.Length; r++)
        {
            string row = rows[r];
            for (int i = 0; i < row.Length; i++)
            {
                uint c = palette(row[i]);
                if ((c >> 24) == 0) continue;
                int x = mirror ? x0 + row.Length - 1 - i : x0 + i;
                Set(x, y0 + r, c);
            }
        }
    }
}

internal static class Col
{
    public static uint Rgb(int r, int g, int b) => 0xFF000000u | ((uint)r << 16) | ((uint)g << 8) | (uint)b;

    public static uint Lerp(uint a, uint b, double t)
    {
        if (t <= 0) return a;
        if (t >= 1) return b;
        int Ch(int s) => (int)Math.Round(((a >> s) & 255) + ((((b >> s) & 255) - (double)((a >> s) & 255)) * t));
        return ((uint)Ch(24) << 24) | ((uint)Ch(16) << 16) | ((uint)Ch(8) << 8) | (uint)Ch(0);
    }

    public static uint WithAlpha(uint c, double a)
    {
        int al = (int)Math.Round(Math.Clamp(a, 0, 1) * ((c >> 24) & 255));
        return ((uint)al << 24) | (c & 0xFFFFFF);
    }

    /// <summary>Couleur prémultipliée pour UpdateLayeredWindow.</summary>
    public static uint Premul(uint c)
    {
        uint a = c >> 24;
        if (a == 255) return c;
        if (a == 0) return 0;
        uint r = ((c >> 16) & 255) * a / 255, g = ((c >> 8) & 255) * a / 255, b = (c & 255) * a / 255;
        return (a << 24) | (r << 16) | (g << 8) | b;
    }
}

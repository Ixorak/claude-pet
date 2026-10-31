using ClaudePet.Art;
using ClaudePet.Native;

namespace ClaudePet.Gfx;

/// <summary>
/// Fenêtre « layered » à transparence par pixel : toujours au premier plan, invisible dans la barre des tâches,
/// ne prend jamais le focus. Les pixels transparents laissent passer les clics.
/// </summary>
internal sealed unsafe class LayeredWindow
{
    public readonly nint Hwnd;
    private readonly nint memDC;
    private nint dib, oldBmp;
    private uint* bits;
    private int capW, capH;
    public int X, Y, W, H;
    public bool Shown;
    private bool clickThrough;

    /// <param name="activatable">Vrai pour une fenêtre qui peut prendre le focus (les bulles, comme un menu).</param>
    public LayeredWindow(string className, bool clickThrough, bool activatable = false)
    {
        uint ex = C.WS_EX_LAYERED | C.WS_EX_TOPMOST | C.WS_EX_TOOLWINDOW;
        if (!activatable) ex |= C.WS_EX_NOACTIVATE;
        if (clickThrough) ex |= C.WS_EX_TRANSPARENT;
        this.clickThrough = clickThrough;
        Hwnd = User32.CreateWindowEx(ex, className, "Claude Pet", C.WS_POPUP, 0, 0, 1, 1, 0, 0, Kernel32.GetModuleHandle(null), 0);
        memDC = Gdi32.CreateCompatibleDC(0);
    }

    public int Stride => capW;

    /// <summary>Prépare une surface d'au moins w x h pixels (effacée) et renvoie son pointeur.</summary>
    public uint* Begin(int w, int h)
    {
        if (w > capW || h > capH)
        {
            int nw = Math.Max(capW, (w + 63) & ~63), nh = Math.Max(capH, (h + 63) & ~63);
            BITMAPINFOHEADER bi = new()
            {
                biSize = (uint)sizeof(BITMAPINFOHEADER), biWidth = nw, biHeight = -nh,
                biPlanes = 1, biBitCount = 32, biCompression = C.BI_RGB,
            };
            void* p;
            nint nd = Gdi32.CreateDIBSection(0, &bi, C.DIB_RGB_COLORS, &p, 0, 0);
            if (nd == 0) return bits;
            if (dib != 0)
            {
                Gdi32.SelectObject(memDC, oldBmp);
                Gdi32.DeleteObject(dib);
            }
            dib = nd;
            oldBmp = Gdi32.SelectObject(memDC, dib);
            bits = (uint*)p;
            capW = nw; capH = nh;
        }
        for (int y = 0; y < h; y++) new Span<uint>(bits + y * capW, w).Clear();
        return bits;
    }

    /// <summary>Affiche le contenu préparé (w x h) à la position (x, y).</summary>
    public void Commit(int x, int y, int w, int h)
    {
        if (w <= 0 || h <= 0) { Hide(); return; }
        POINT dst = new(x, y), src = new(0, 0);
        SIZE size = new(w, h);
        BLENDFUNCTION bf = new() { BlendOp = C.AC_SRC_OVER, SourceConstantAlpha = 255, AlphaFormat = C.AC_SRC_ALPHA };
        if (User32.UpdateLayeredWindow(Hwnd, 0, &dst, &size, memDC, &src, 0, &bf, C.ULW_ALPHA) == 0)
            ClaudePet.Util.Log.Write($"UpdateLayeredWindow a échoué ({w}x{h} à {x},{y}, capacité {capW}x{capH}, erreur {System.Runtime.InteropServices.Marshal.GetLastSystemError()})");
        X = x; Y = y; W = w; H = h;
        if (!Shown)
        {
            User32.SetWindowPos(Hwnd, C.HWND_TOPMOST, 0, 0, 0, 0, C.SWP_NOMOVE | C.SWP_NOSIZE | C.SWP_NOACTIVATE | C.SWP_SHOWWINDOW);
            Shown = true;
        }
    }

    public void Move(int x, int y)
    {
        if (x == X && y == Y) return;
        User32.SetWindowPos(Hwnd, 0, x, y, 0, 0, C.SWP_NOSIZE | C.SWP_NOZORDER | C.SWP_NOACTIVATE | C.SWP_NOOWNERZORDER);
        X = x; Y = y;
    }

    public void Hide()
    {
        if (!Shown) return;
        User32.ShowWindow(Hwnd, C.SW_HIDE);
        Shown = false;
    }

    public void BringToTop()
    {
        if (Shown) User32.SetWindowPos(Hwnd, C.HWND_TOPMOST, 0, 0, 0, 0, C.SWP_NOMOVE | C.SWP_NOSIZE | C.SWP_NOACTIVATE);
    }

    public void SetClickThrough(bool on)
    {
        if (on == clickThrough) return;
        clickThrough = on;
        nint ex = User32.GetWindowLongPtr(Hwnd, C.GWL_EXSTYLE);
        ex = on ? (ex | (nint)C.WS_EX_TRANSPARENT) : (ex & ~(nint)C.WS_EX_TRANSPARENT);
        User32.SetWindowLongPtr(Hwnd, C.GWL_EXSTYLE, ex);
    }

    public bool ContainsScreenPoint(int x, int y) => Shown && x >= X && y >= Y && x < X + W && y < Y + H;

    /// <summary>Vrai si le pixel affiché sous ce point est visible (alpha non nul).</summary>
    public bool HitPixel(int x, int y)
    {
        if (!ContainsScreenPoint(x, y) || bits == null) return false;
        return (bits[(y - Y) * capW + (x - X)] >> 24) > 8;
    }
}

internal static unsafe class Blit
{
    /// <summary>Agrandit une zone d'une toile art au plus proche voisin, en couleurs prémultipliées.</summary>
    public static void Art(ArtCanvas c, int bx0, int by0, int bw, int bh, double scale, uint* dst, int stride, int dw, int dh)
    {
        double inv = 1.0 / scale;
        int lastX = bx0 + bw - 1, lastY = by0 + bh - 1;
        Span<int> xs = dw <= 4096 ? stackalloc int[dw] : new int[dw];
        for (int dx = 0; dx < dw; dx++) xs[dx] = Math.Min(lastX, bx0 + (int)(dx * inv));
        uint[] px = c.Px;
        for (int dy = 0; dy < dh; dy++)
        {
            int sy = Math.Min(lastY, by0 + (int)(dy * inv));
            int row = sy * c.W;
            uint* d = dst + dy * stride;
            for (int dx = 0; dx < dw; dx++) d[dx] = Col.Premul(px[row + xs[dx]]);
        }
    }

    /// <summary>Pose un sprite (grille ARGB) agrandi d'un facteur entier, avec une opacité.</summary>
    public static void Sprite(uint[] src, int sw, int sh, int k, double alpha, uint* dst, int stride, int dw, int dh, int x0, int y0)
    {
        for (int sy = 0; sy < sh; sy++)
            for (int sx = 0; sx < sw; sx++)
            {
                uint c = src[sy * sw + sx];
                if ((c >> 24) == 0) continue;
                if (alpha < 1) c = Col.WithAlpha(c, alpha);
                uint pc = Col.Premul(c);
                for (int yy = 0; yy < k; yy++)
                {
                    int y = y0 + sy * k + yy;
                    if ((uint)y >= (uint)dh) continue;
                    uint* d = dst + y * stride;
                    for (int xx = 0; xx < k; xx++)
                    {
                        int x = x0 + sx * k + xx;
                        if ((uint)x >= (uint)dw) continue;
                        d[x] = Over(pc, d[x]);
                    }
                }
            }
    }

    public static void FillRect(uint* dst, int stride, int dw, int dh, int x0, int y0, int w, int h, uint color)
    {
        uint pc = Col.Premul(color);
        int xa = Math.Max(0, x0), ya = Math.Max(0, y0), xb = Math.Min(dw, x0 + w), yb = Math.Min(dh, y0 + h);
        for (int y = ya; y < yb; y++)
        {
            uint* d = dst + y * stride;
            for (int x = xa; x < xb; x++) d[x] = Over(pc, d[x]);
        }
    }

    /// <summary>Composition « par-dessus » de deux couleurs prémultipliées.</summary>
    public static uint Over(uint s, uint d)
    {
        uint sa = s >> 24;
        if (sa == 255 || d == 0) return s;
        if (sa == 0) return d;
        uint inv = 255 - sa;
        uint a = sa + ((d >> 24) * inv + 127) / 255;
        uint r = ((s >> 16) & 255) + (((d >> 16) & 255) * inv + 127) / 255;
        uint g = ((s >> 8) & 255) + (((d >> 8) & 255) * inv + 127) / 255;
        uint b = (s & 255) + ((d & 255) * inv + 127) / 255;
        return (Math.Min(a, 255u) << 24) | (Math.Min(r, 255u) << 16) | (Math.Min(g, 255u) << 8) | Math.Min(b, 255u);
    }
}

internal static unsafe class IconFactory
{
    /// <summary>Crée une HICON 32 bits (alpha) à partir de pixels ARGB non prémultipliés.</summary>
    public static nint Create(uint[] px, int size)
    {
        BITMAPINFOHEADER bi = new()
        {
            biSize = (uint)sizeof(BITMAPINFOHEADER), biWidth = size, biHeight = -size,
            biPlanes = 1, biBitCount = 32, biCompression = C.BI_RGB,
        };
        void* bits;
        nint color = Gdi32.CreateDIBSection(0, &bi, C.DIB_RGB_COLORS, &bits, 0, 0);
        if (color == 0) return 0;
        fixed (uint* s = px) Buffer.MemoryCopy(s, bits, size * size * 4, size * size * 4);
        int maskStride = ((size + 15) / 16) * 2;
        var zeros = new byte[maskStride * size];
        nint mask;
        fixed (byte* z = zeros) mask = Gdi32.CreateBitmap(size, size, 1, 1, z);
        ICONINFO ii = new() { fIcon = 1, hbmMask = mask, hbmColor = color };
        nint icon = User32.CreateIconIndirect(&ii);
        Gdi32.DeleteObject(color);
        Gdi32.DeleteObject(mask);
        return icon;
    }
}

using ClaudePet.Art;
using ClaudePet.Gfx;
using ClaudePet.Native;
using ClaudePet.Util;

namespace ClaudePet.Core;

/// <summary>
/// Les bulles d'options au-dessus de Pet (double-clic) : Calme, Nourrir, Muet, Dormir, Rentrer à la maison,
/// Réglages (volume, taille), et la jauge de faim. Elles se ferment quand Ixorak clique ailleurs :
/// la fenêtre des bulles prend le focus à l'ouverture (comme un menu) et se ferme quand elle le perd.
/// Si Windows refuse de lui donner le focus, on surveille simplement l'état des boutons de la souris.
/// </summary>
internal sealed unsafe class Bubbles
{
    private enum Kind { Gauge, Calm, Feed, Mute, Sleep, Home, Settings, Panel, VolSlider, SizeSlider }

    private struct Item
    {
        public Kind K;
        public int X, Y, W, H, Order;
        public bool Clickable => K is not (Kind.Gauge or Kind.Panel);
    }

    private readonly App app;
    public readonly LayeredWindow Win;
    public bool IsOpen { get; private set; }
    private bool settings;
    private double openT;
    private int hover = -1, pressed = -1;
    private Kind? drag;
    private int u = 3;
    private readonly List<Item> items = new();
    private int lw, lh;
    private uint[] buf = new uint[16];
    private bool dirty;
    private int sliderX0, sliderW;   // piste des curseurs, en pixels UI (relatif à l'élément)
    private bool focusPending, pollClicks, buttonsWereDown;

    private static readonly uint Ink = Col.Rgb(0x3A, 0x2A, 0x22);
    private static readonly uint Fill = Col.Rgb(0xFF, 0xF9, 0xF0);
    private static readonly uint FillHover = Col.Rgb(0xFF, 0xEC, 0xD0);
    private static readonly uint FillPress = Col.Rgb(0xFF, 0xDD, 0xAE);
    private static readonly uint Shadow = 0x40000000;
    private static readonly uint On = Col.Rgb(0x58, 0xB8, 0x5C);
    private static readonly uint Off = Col.Rgb(0xC9, 0xBF, 0xB5);
    private static readonly uint Orange = Col.Rgb(0xD9, 0x77, 0x57);
    private static readonly uint Track = Col.Rgb(0xE6, 0xD8, 0xC4);

    public Bubbles(App app)
    {
        this.app = app;
        Win = new LayeredWindow(App.BubbleClass, clickThrough: false, activatable: true);
    }

    // ------------------------------------------------------------------ ouverture / fermeture

    public void Open()
    {
        settings = false;
        IsOpen = true;
        openT = Clock.Now;
        hover = pressed = -1;
        drag = null;
        Layout();
        dirty = true;
        focusPending = true;
        pollClicks = false;
        buttonsWereDown = ButtonsDown();
        app.Play(Snd.Bubble, 0.6);
        app.Pet.Attentive = true;
    }

    public void Close()
    {
        if (!IsOpen) return;
        IsOpen = false;
        if (drag != null) { User32.ReleaseCapture(); drag = null; }
        Win.Hide();
        app.Pet.Attentive = false;
    }

    /// <summary>La fenêtre des bulles a perdu le focus : Ixorak a cliqué ailleurs.</summary>
    public void OnDeactivated()
    {
        if (IsOpen && !focusPending) User32.PostMessage(Win.Hwnd, App.MsgCloseBubbles, 0, 0);
    }

    private static bool ButtonsDown()
        => (User32.GetAsyncKeyState(C.VK_LBUTTON) & 0x8000) != 0
        || (User32.GetAsyncKeyState(C.VK_RBUTTON) & 0x8000) != 0
        || (User32.GetAsyncKeyState(C.VK_MBUTTON) & 0x8000) != 0;

    public bool Animating => IsOpen && Clock.Now - openT < 0.5;

    // ------------------------------------------------------------------ mise en page (pixels UI)

    private static string Label(Kind k) => k switch
    {
        Kind.Calm => "Calme",
        Kind.Feed => "Nourrir",
        Kind.Mute => "Muet",
        Kind.Sleep => "Dormir",
        Kind.Home => "Rentrer à la maison",
        Kind.Settings => "Réglages",
        Kind.Gauge => "Faim",
        _ => "",
    };

    private const int H = 18, Gap = 4, Pad = 4, IconW = 12, ToggleW = 13, BarW = 46;

    private static int BubbleWidth(Kind k)
    {
        int w = 1 + Pad + IconW + 4 + PixelFont.Width(Label(k)) + Pad + 1;
        if (k is Kind.Calm or Kind.Mute) w += ToggleW + 4;
        if (k == Kind.Gauge) w += 6 + BarW;
        return w;
    }

    private void Layout()
    {
        items.Clear();
        Kind[] left = { Kind.Calm, Kind.Mute, Kind.Sleep };
        Kind[] right = { Kind.Feed, Kind.Settings, Kind.Home };
        int wl = left.Max(BubbleWidth), wr = right.Max(BubbleWidth);
        int width = wl + Gap + wr;
        int y = 0, order = 0;

        if (settings)
        {
            int panelH = 1 + 3 + 16 + 3 + 16 + 3 + 1;
            items.Add(new Item { K = Kind.Panel, X = 0, Y = y, W = width, H = panelH, Order = order++ });
            sliderX0 = 1 + Pad + PixelFont.Width("Volume") + 6;
            sliderW = Math.Max(40, width - sliderX0 - Pad - 1 - PixelFont.Width("100") - 6);
            items.Add(new Item { K = Kind.VolSlider, X = 0, Y = y + 4, W = width, H = 16, Order = order });
            items.Add(new Item { K = Kind.SizeSlider, X = 0, Y = y + 4 + 16 + 3, W = width, H = 16, Order = order++ });
            y += panelH + 1 + Gap;
        }

        int gw = BubbleWidth(Kind.Gauge);
        items.Add(new Item { K = Kind.Gauge, X = (width - gw) / 2, Y = y, W = gw, H = H, Order = order++ });
        y += H + 1 + Gap;
        for (int r = 0; r < 3; r++)
        {
            items.Add(new Item { K = left[r], X = 0, Y = y, W = wl, H = H, Order = order++ });
            items.Add(new Item { K = right[r], X = wl + Gap, Y = y, W = wr, H = H, Order = order++ });
            y += H + 1 + Gap;
        }
        // petites bulles de pensée vers Pet
        lw = width;
        lh = y + 9;
        if (buf.Length < lw * lh) buf = new uint[lw * lh];
    }

    // ------------------------------------------------------------------ boucle

    public void Update(double now)
    {
        if (!IsOpen) return;
        var pet = app.Pet;
        if (pet.State is St.Hidden or St.Air or St.Dragged or St.HangCursor or St.SitCursor or St.ClimbCursor or St.HomeJump or St.EnterDoor or St.Dig)
        {
            Close();
            return;
        }
        int h = -1;
        var c = app.Cursor;
        if (pollClicks)
        {
            // Secours (focus refusé par Windows) : un bouton enfoncé hors des bulles et hors de Pet les ferme.
            bool down = ButtonsDown();
            if (down && !buttonsWereDown && !Win.ContainsScreenPoint((int)c.X, (int)c.Y) && !app.PetWin.HitPixel((int)c.X, (int)c.Y))
            {
                Close();
                return;
            }
            buttonsWereDown = down;
        }
        if (Win.ContainsScreenPoint((int)c.X, (int)c.Y)) h = HitTest((int)c.X, (int)c.Y);
        if (h != hover) { hover = h; dirty = true; }
        if (Animating) dirty = true;
        if (dirty) Render(now);
    }

    public void MarkDirty() => dirty = true;

    private int HitTest(int sx, int sy)
    {
        int x = (sx - Win.X) / u, y = (sy - Win.Y) / u;
        for (int i = items.Count - 1; i >= 0; i--)
        {
            var it = items[i];
            if (!it.Clickable) continue;
            if (x >= it.X && x < it.X + it.W && y >= it.Y && y < it.Y + it.H) return i;
        }
        return -1;
    }

    // ------------------------------------------------------------------ souris

    public void OnMouseDown(int sx, int sy)
    {
        int i = HitTest(sx, sy);
        if (i < 0) return;
        var it = items[i];
        if (it.K is Kind.VolSlider or Kind.SizeSlider)
        {
            drag = it.K;
            User32.SetCapture(Win.Hwnd);
            DragTo(sx);
            return;
        }
        pressed = i;
        dirty = true;
    }

    public void OnMouseMove(int sx, int sy)
    {
        if (drag != null) DragTo(sx);
    }

    public void OnMouseUp(int sx, int sy)
    {
        if (drag != null)
        {
            if (drag == Kind.VolSlider) app.Play(Snd.Bubble, 0.8);
            drag = null;
            User32.ReleaseCapture();
            dirty = true;
            return;
        }
        int i = HitTest(sx, sy);
        int p = pressed;
        pressed = -1;
        dirty = true;
        if (i < 0 || i != p) return;
        Activate(items[i].K);
    }

    public void OnCaptureLost() { drag = null; dirty = true; }

    private void DragTo(int sx)
    {
        double x = (sx - Win.X) / (double)u - sliderX0;
        double f = MathX.Clamp(x / sliderW, 0, 1);
        if (drag == Kind.VolSlider)
        {
            int v = (int)Math.Round(f * 20) * 5;
            if (v != app.Cfg.Volume) { app.Cfg.Volume = v; app.ApplyVolume(); dirty = true; }
        }
        else if (drag == Kind.SizeSlider)
        {
            int v = Config.MinSize + (int)Math.Round(f * (Config.MaxSize - Config.MinSize));
            if (v != app.Cfg.Size) { app.Cfg.Size = v; app.Pet.OnSizeChanged(); dirty = true; }
        }
    }

    private void Activate(Kind k)
    {
        switch (k)
        {
            case Kind.Calm:
                app.SetCalm(!app.Cfg.Calm);
                app.Play(Snd.Click, 0.7);
                break;
            case Kind.Mute:
                app.SetMuted(!app.Cfg.Muted);
                app.Play(Snd.Click, 0.7);
                break;
            case Kind.Feed:
                Close();
                app.StartFeeding();
                break;
            case Kind.Sleep:
                Close();
                app.Pet.SleepUntilClicked();
                break;
            case Kind.Home:
                Close();
                app.GoHome();
                break;
            case Kind.Settings:
                settings = !settings;
                app.Play(Snd.Bubble, 0.5);
                openT = Clock.Now - 0.2;
                Layout();
                break;
        }
        dirty = true;
    }

    // ------------------------------------------------------------------ rendu

    private void Render(double now)
    {
        dirty = false;
        var pet = app.Pet;
        u = Math.Clamp((int)Math.Round(pet.Scale * 0.42), 2, 6);
        Array.Clear(buf, 0, lw * lh);

        for (int i = 0; i < items.Count; i++)
        {
            var it = items[i];
            double appear = openT + it.Order * 0.035;
            double prog = MathX.Clamp((now - appear) / 0.13, 0, 1);
            if (prog <= 0) continue;
            int lift = (int)Math.Round((1 - prog) * 4);
            double a = prog;
            DrawItem(it, i, it.Y + lift, a);
        }
        // bulles de pensée
        double pd = MathX.Clamp((now - openT) / 0.1, 0, 1);
        if (pd > 0)
        {
            int cx = lw / 2;
            Circle(cx - 4, lh - 8, 2, pd);
            Circle(cx - 7, lh - 3, 1, pd);
        }

        // fenêtre : centrée au-dessus de la tête de Pet, gardée dans l'écran
        pet.HeadPoint(out double hx, out double hy);
        int dw = lw * u, dh = lh * u;
        var s = pet.Scr.Work;
        int x = (int)Math.Round(hx - dw / 2.0);
        int y = (int)Math.Round(hy - 4 * u - dh);
        x = Math.Clamp(x, s.Left + 4, Math.Max(s.Left + 4, s.Right - dw - 4));
        y = Math.Clamp(y, s.Top + 4, Math.Max(s.Top + 4, s.Bottom - dh - 4));
        uint* bits = Win.Begin(dw, dh);
        int stride = Win.Stride;
        for (int py = 0; py < dh; py++)
        {
            uint* row = bits + py * stride;
            int srow = (py / u) * lw;
            for (int px = 0; px < dw; px++) row[px] = Col.Premul(buf[srow + px / u]);
        }
        Win.Commit(x, y, dw, dh);
        if (focusPending)
        {
            // Prendre le focus comme un menu : le premier clic ailleurs le lui retirera et fermera les bulles.
            focusPending = false;
            User32.SetForegroundWindow(Win.Hwnd);
            pollClicks = User32.GetForegroundWindow() != Win.Hwnd;
            buttonsWereDown = ButtonsDown();
            if (pollClicks) Log.Write("Bulles : focus refusé par Windows, surveillance des boutons de la souris");
        }
        if (!loggedOnce) { loggedOnce = true; Log.Write($"Bulles : {dw}x{dh} à ({x},{y}), u={u}"); }
    }

    private bool loggedOnce;

    /// <summary>Outil de test : dessine les bulles dans un fichier BMP (sans les afficher).</summary>
    public void DumpBmp(string path, bool withSettings)
    {
        bool wasSettings = settings;
        settings = withSettings;
        Layout();
        u = Math.Clamp((int)Math.Round(app.Pet.Scale * 0.42), 2, 6);
        Array.Clear(buf, 0, lw * lh);
        for (int i = 0; i < items.Count; i++) DrawItem(items[i], i, items[i].Y, 1);
        Circle(lw / 2 - 4, lh - 8, 2, 1);
        Circle(lw / 2 - 7, lh - 3, 1, 1);
        int w = lw * u, h = lh * u;
        using var f = File.Create(path);
        using var bw = new BinaryWriter(f);
        bw.Write((ushort)0x4D42); bw.Write(54 + w * h * 4); bw.Write(0); bw.Write(54);
        bw.Write(40); bw.Write(w); bw.Write(h); bw.Write((ushort)1); bw.Write((ushort)32); bw.Write(0); bw.Write(w * h * 4);
        bw.Write(2835); bw.Write(2835); bw.Write(0); bw.Write(0);
        for (int y = h - 1; y >= 0; y--)
            for (int x = 0; x < w; x++)
            {
                uint c = buf[(y / u) * lw + x / u];
                uint bg = ((x / 24 + y / 24) & 1) == 0 ? 0xFF5A7A95u : 0xFF46627Au;
                bw.Write((c >> 24) == 0 ? bg : Blend(c, bg));
            }
        settings = wasSettings;
        Layout();
    }

    private static uint Blend(uint c, uint bg)
    {
        double a = (c >> 24) / 255.0;
        uint Ch(int s) => (uint)(((c >> s) & 255) * a + ((bg >> s) & 255) * (1 - a));
        return 0xFF000000u | (Ch(16) << 16) | (Ch(8) << 8) | Ch(0);
    }

    private void Put(int x, int y, uint c, double a)
    {
        if ((uint)x >= (uint)lw || (uint)y >= (uint)lh) return;
        if (a < 1) c = Col.WithAlpha(c, a);
        int idx = y * lw + x;
        uint d = buf[idx];
        if ((c >> 24) == 255 || d == 0) { buf[idx] = c; return; }
        // composition simple (couleurs non prémultipliées)
        double sa = (c >> 24) / 255.0, da = (d >> 24) / 255.0;
        double oa = sa + da * (1 - sa);
        if (oa <= 0) { buf[idx] = 0; return; }
        uint r = (uint)((((c >> 16) & 255) * sa + ((d >> 16) & 255) * da * (1 - sa)) / oa);
        uint g = (uint)((((c >> 8) & 255) * sa + ((d >> 8) & 255) * da * (1 - sa)) / oa);
        uint b = (uint)(((c & 255) * sa + (d & 255) * da * (1 - sa)) / oa);
        buf[idx] = ((uint)(oa * 255) << 24) | (r << 16) | (g << 8) | b;
    }

    private void Rect(int x0, int y0, int w, int h, uint c, double a)
    {
        for (int y = y0; y < y0 + h; y++)
            for (int x = x0; x < x0 + w; x++) Put(x, y, c, a);
    }

    private void Bubble(int x, int y, int w, int h, uint fill, double a)
    {
        // ombre
        for (int i = x + 2; i < x + w - 2; i++) Put(i, y + h, Shadow, a);
        // fond
        Rect(x + 1, y + 1, w - 2, h - 2, fill, a);
        // contour, coins arrondis
        for (int i = x + 2; i < x + w - 2; i++) { Put(i, y, Ink, a); Put(i, y + h - 1, Ink, a); }
        for (int j = y + 2; j < y + h - 2; j++) { Put(x, j, Ink, a); Put(x + w - 1, j, Ink, a); }
        Put(x + 1, y + 1, Ink, a); Put(x + w - 2, y + 1, Ink, a);
        Put(x + 1, y + h - 2, Ink, a); Put(x + w - 2, y + h - 2, Ink, a);
    }

    private void Circle(int cx, int cy, int r, double a)
    {
        for (int y = -r - 1; y <= r + 1; y++)
            for (int x = -r - 1; x <= r + 1; x++)
            {
                double d = Math.Sqrt(x * x + y * y);
                if (d <= r + 0.2) Put(cx + x, cy + y, Fill, a);
                else if (d <= r + 1.2) Put(cx + x, cy + y, Ink, a);
            }
    }

    private void Icon(Icons.Icon ic, int x, int y, double a)
    {
        for (int j = 0; j < ic.H; j++)
            for (int i = 0; i < ic.W; i++)
            {
                uint c = ic.Px[j * ic.W + i];
                if ((c >> 24) != 0) Put(x + i, y + j, c, a);
            }
    }

    private void Text(string s, int x, int y, uint c, double a) => PixelFont.Draw(s, x, y, (px, py) => Put(px, py, c, a));

    private void Toggle(int x, int y, bool on, double a)
    {
        // pilule 13 x 7
        uint back = on ? On : Off;
        Rect(x + 1, y + 1, ToggleW - 2, 5, back, a);
        for (int i = x + 1; i < x + ToggleW - 1; i++) { Put(i, y, Ink, a); Put(i, y + 6, Ink, a); }
        for (int j = y + 1; j < y + 6; j++) { Put(x, j, Ink, a); Put(x + ToggleW - 1, j, Ink, a); }
        int kx = on ? x + ToggleW - 6 : x + 1;
        Rect(kx, y + 1, 5, 5, Col.Rgb(255, 255, 255), a);
        for (int j = y + 1; j < y + 6; j++) Put(on ? kx - 1 : kx + 5, j, Ink, a);
    }

    private void DrawItem(Item it, int index, int y, double a)
    {
        uint fill = index == pressed ? FillPress : index == hover ? FillHover : Fill;
        int textY = y + 3;      // ligne 0 de la police
        int iconY = y + 3;
        switch (it.K)
        {
            case Kind.Panel:
                Bubble(it.X, y, it.W, it.H, Fill, a);
                return;
            case Kind.VolSlider:
            case Kind.SizeSlider:
            {
                bool vol = it.K == Kind.VolSlider;
                int ty = y + 1;
                Text(vol ? "Volume" : "Taille", it.X + 1 + Pad, ty, Ink, a);
                double f = vol ? app.Cfg.Volume / 100.0 : (app.Cfg.Size - Config.MinSize) / (double)(Config.MaxSize - Config.MinSize);
                int tx = it.X + sliderX0, trackY = y + 6;
                Rect(tx, trackY, sliderW, 4, Ink, a);
                Rect(tx + 1, trackY + 1, sliderW - 2, 2, Track, a);
                int fw = (int)Math.Round((sliderW - 2) * f);
                Rect(tx + 1, trackY + 1, fw, 2, Orange, a);
                int kx = tx + (int)Math.Round((sliderW - 1) * f) - 2;
                Rect(kx, trackY - 3, 5, 10, Ink, a);
                Rect(kx + 1, trackY - 2, 3, 8, drag == it.K ? FillPress : Col.Rgb(255, 255, 255), a);
                string val = vol ? app.Cfg.Volume.ToString() : app.Cfg.Size.ToString();
                Text(val, tx + sliderW + 6, ty, Ink, a);
                return;
            }
            case Kind.Gauge:
            {
                Bubble(it.X, y, it.W, it.H, Fill, a);
                Icon(Icons.Berry, it.X + 1 + Pad, iconY, a);
                int x = it.X + 1 + Pad + IconW + 4;
                Text("Faim", x, textY, Ink, a);
                x += PixelFont.Width("Faim") + 6;
                double sat = app.Satiety;
                int segs = 10, sw = (BarW - (segs + 1)) / segs;
                int bx = x, by = y + 6;
                Rect(bx, by, segs * (sw + 1) + 1, 7, Ink, a);
                int filled = (int)Math.Ceiling(sat * segs - 0.001);
                uint col = sat > 0.5 ? On : sat > 0.25 ? Col.Rgb(0xFF, 0xC9, 0x3C) : Col.Rgb(0xF0, 0x5A, 0x4F);
                for (int sIdx = 0; sIdx < segs; sIdx++)
                    Rect(bx + 1 + sIdx * (sw + 1), by + 1, sw, 5, sIdx < filled ? col : Col.Rgb(0xE8, 0xDC, 0xCB), a);
                return;
            }
        }

        Bubble(it.X, y, it.W, it.H, fill, a);
        var icon = it.K switch
        {
            Kind.Calm => Icons.Leaf,
            Kind.Feed => Icons.Berry,
            Kind.Mute => app.Cfg.Muted ? Icons.SoundOff : Icons.SoundOn,
            Kind.Sleep => Icons.Moon,
            Kind.Home => Icons.House,
            _ => Icons.Gear,
        };
        Icon(icon, it.X + 1 + Pad, iconY, a);
        int lx = it.X + 1 + Pad + IconW + 4;
        Text(Label(it.K), lx, textY, Ink, a);
        if (it.K is Kind.Calm or Kind.Mute)
        {
            bool on = it.K == Kind.Calm ? app.Cfg.Calm : app.Cfg.Muted;
            Toggle(it.X + it.W - 1 - Pad - ToggleW, y + 5, on, a);
        }
        if (it.K == Kind.Settings && settings) Put(it.X + it.W - 3, y + 2, Orange, a);
    }
}

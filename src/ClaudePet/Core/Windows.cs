using System.Runtime.InteropServices;
using ClaudePet.Native;
using ClaudePet.Util;

namespace ClaudePet.Core;

/// <summary>Une fenêtre normale vue par Pet.</summary>
internal struct WinInfo
{
    public nint Hwnd;
    public RECT R;          // cadre visible (sans les bordures invisibles de Windows 10/11)
    public bool Maximized;
}

/// <summary>Un morceau visible du haut d'une fenêtre : Pet peut y marcher.</summary>
internal struct Platform
{
    public nint Hwnd;
    public double Y, X0, X1;
}

/// <summary>
/// Liste les fenêtres normales dans l'ordre d'empilement (la plus haute d'abord) : visibles, pas réduites,
/// pas « cloaked », pas des fenêtres outils, ni le bureau, ni la barre des tâches, ni celles de Pet.
/// En déduit :
/// <list type="bullet">
/// <item>les plates-formes : les morceaux visibles du haut des fenêtres non agrandies, sur les écrans libres ;</item>
/// <item>les écrans en plein écran : la plus haute fenêtre de l'écran le couvre en entier sans être agrandie
/// (ou c'est le cas de la fenêtre active).</item>
/// </list>
/// Lecture seule : Pet ne touche à aucune fenêtre des autres programmes.
/// </summary>
internal sealed unsafe class WindowScanner
{
    public readonly List<WinInfo> Windows = new();
    public readonly List<Platform> Platforms = new();
    /// <summary>Outil de test : état plein écran imposé à un écran (numéro → plein ou libre).</summary>
    public readonly Dictionary<int, bool> Override = new();
    private static readonly List<nint> found = new();
    private readonly uint myPid = Kernel32.GetCurrentProcessId();
    private readonly List<(double A, double B)> spans = new(), work = new();

    private static readonly HashSet<string> Ignored = new(StringComparer.Ordinal)
    {
        "Progman", "WorkerW", "Shell_TrayWnd", "Shell_SecondaryTrayWnd", "NotifyIconOverflowWindow",
        "Windows.UI.Core.CoreWindow", "ForegroundStaging", "MultitaskingViewFrame", "XamlExplorerHostIslandWindow",
        "TopLevelWindowForOverflowXamlIsland", "Xaml_WindowedPopupClass", "Shell_InputSwitchTopLevelWindow",
        "ApplicationManager_ImmersiveShellWindow", "EdgeUiInputTopWndClass", "SysShadow", "tooltips_class32", "#32768",
        "Shell_LightDismissOverlay", "PseudoConsoleWindow", "IME", "MSCTFIME UI", "DummyDWMListenerWindow",
    };

    [UnmanagedCallersOnly]
    private static int EnumProc(nint hwnd, nint data)
    {
        found.Add(hwnd);
        return 1;
    }

    /// <summary>Relit les fenêtres et recalcule les plates-formes. Renvoie vrai si un écran a changé d'état plein écran.</summary>
    public bool Scan(Desk desk)
    {
        found.Clear();
        User32.EnumWindows(&EnumProc, 0);
        Windows.Clear();
        char* cls = stackalloc char[64];
        foreach (var h in found)
        {
            if (!Normal(h, cls)) continue;
            RECT r;
            if (Dwm.DwmGetWindowAttribute(h, C.DWMWA_EXTENDED_FRAME_BOUNDS, &r, (uint)sizeof(RECT)) != 0) User32.GetWindowRect(h, &r);
            if (r.Right - r.Left < 80 || r.Bottom - r.Top < 40) continue;
            Windows.Add(new WinInfo { Hwnd = h, R = r, Maximized = User32.IsZoomed(h) != 0 });
        }
        bool changed = UpdateFullscreen(desk);
        BuildPlatforms(desk);
        return changed;
    }

    private bool Normal(nint h, char* cls)
    {
        if (User32.IsWindowVisible(h) == 0 || User32.IsIconic(h) != 0) return false;
        uint pid;
        User32.GetWindowThreadProcessId(h, &pid);
        if (pid == myPid) return false;
        uint st = (uint)User32.GetWindowLongPtr(h, C.GWL_STYLE);
        uint ex = (uint)User32.GetWindowLongPtr(h, C.GWL_EXSTYLE);
        if ((st & C.WS_CHILD) != 0) return false;
        if ((ex & C.WS_EX_TOOLWINDOW) != 0 && (ex & C.WS_EX_APPWINDOW) == 0) return false;
        if ((ex & C.WS_EX_NOACTIVATE) != 0) return false;
        if ((ex & C.WS_EX_LAYERED) != 0 && (ex & C.WS_EX_TRANSPARENT) != 0) return false; // surcouches qui laissent passer les clics
        nint owner = User32.GetWindow(h, C.GW_OWNER);
        if (owner != 0 && (st & C.WS_CAPTION) != C.WS_CAPTION && (ex & C.WS_EX_APPWINDOW) == 0) return false; // menus, bulles...
        int cloaked = 0;
        if (Dwm.DwmGetWindowAttribute(h, C.DWMWA_CLOAKED, &cloaked, 4) == 0 && cloaked != 0) return false;
        int n = User32.GetClassName(h, cls, 64);
        if (n > 0 && Ignored.Contains(new string(cls, 0, n))) return false;
        return true;
    }

    // ------------------------------------------------------------------ plein écran

    private bool UpdateFullscreen(Desk desk)
    {
        nint fg = User32.GetForegroundWindow();
        bool changed = false;
        foreach (var s in desk.Screens)
        {
            var b = s.Bounds;
            bool fs = false;
            // la fenêtre active couvre l'écran (un jeu, une vidéo en plein écran)
            foreach (var w in Windows)
                if (w.Hwnd == fg) { fs = Covers(w, b); break; }
            // ou la plus haute fenêtre de cet écran le couvre entièrement, sans être agrandie
            if (!fs)
                foreach (var w in Windows)
                {
                    var r = w.R;
                    double ow = Math.Min(r.Right, b.Right) - Math.Max(r.Left, b.Left);
                    double oh = Math.Min(r.Bottom, b.Bottom) - Math.Max(r.Top, b.Top);
                    if (ow < 64 || oh < 64) continue;
                    fs = Covers(w, b);
                    break;
                }
            if (Override.TryGetValue(s.Index, out bool forced)) fs = forced;
            if (fs != s.Fullscreen)
            {
                s.Fullscreen = fs;
                changed = true;
                Log.Write($"Écran {s.Index} : {(fs ? "plein écran" : "libre")}");
            }
        }
        return changed;
    }

    private static bool Covers(in WinInfo w, RECT b)
        => !w.Maximized && w.R.Left <= b.Left + 1 && w.R.Top <= b.Top + 1 && w.R.Right >= b.Right - 1 && w.R.Bottom >= b.Bottom - 1;

    // ------------------------------------------------------------------ plates-formes

    private void BuildPlatforms(Desk desk)
    {
        Platforms.Clear();
        for (int i = 0; i < Windows.Count; i++)
        {
            var w = Windows[i];
            if (w.Maximized) continue;
            double y = w.R.Top;
            spans.Clear();
            spans.Add((w.R.Left, w.R.Right));
            // les parties cachées par une fenêtre plus haute
            for (int j = 0; j < i && spans.Count > 0; j++)
            {
                var o = Windows[j].R;
                if (o.Top <= y && o.Bottom > y) Subtract(o.Left, o.Right);
            }
            // seulement sur les écrans libres, entre le haut et le sol de leur zone de travail
            foreach (var (a, bb) in spans)
                foreach (var s in desk.Screens)
                {
                    if (s.Fullscreen) continue;
                    var wk = s.Work;
                    if (y <= wk.Top + 4 || y >= wk.Bottom - 8) continue;
                    double x0 = Math.Max(a, wk.Left), x1 = Math.Min(bb, wk.Right);
                    if (x1 - x0 >= 24) Platforms.Add(new Platform { Hwnd = w.Hwnd, Y = y, X0 = x0, X1 = x1 });
                }
        }
        // les morceaux d'une même fenêtre sur deux écrans qui se touchent ne font qu'un
        for (int i = Platforms.Count - 1; i > 0; i--)
        {
            var p = Platforms[i];
            for (int j = 0; j < i; j++)
            {
                var q = Platforms[j];
                if (q.Hwnd != p.Hwnd || q.Y != p.Y) continue;
                if (p.X0 <= q.X1 + 0.5 && q.X0 <= p.X1 + 0.5)
                {
                    q.X0 = Math.Min(q.X0, p.X0);
                    q.X1 = Math.Max(q.X1, p.X1);
                    Platforms[j] = q;
                    Platforms.RemoveAt(i);
                    break;
                }
            }
        }
    }

    private void Subtract(double a, double b)
    {
        work.Clear();
        foreach (var (x0, x1) in spans)
        {
            if (b <= x0 || a >= x1) { work.Add((x0, x1)); continue; }
            if (a > x0) work.Add((x0, a));
            if (b < x1) work.Add((b, x1));
        }
        spans.Clear();
        spans.AddRange(work);
    }

    /// <summary>La fenêtre a bougé : ses plates-formes suivent, en attendant le prochain relevé.</summary>
    public void Shift(nint hwnd, double dx, RECT r)
    {
        for (int i = 0; i < Platforms.Count; i++)
        {
            var p = Platforms[i];
            if (p.Hwnd != hwnd) continue;
            p.X0 = Math.Max(p.X0 + dx, r.Left);
            p.X1 = Math.Min(p.X1 + dx, r.Right);
            p.Y = r.Top;
            Platforms[i] = p;
        }
    }

    /// <summary>
    /// Cadre actuel d'une fenêtre sur laquelle Pet se tient. Faux si elle n'est plus utilisable :
    /// fermée, cachée, réduite, agrandie ou « cloaked ».
    /// </summary>
    public static bool Frame(nint h, out RECT r)
    {
        r = default;
        if (h == 0 || User32.IsWindow(h) == 0 || User32.IsWindowVisible(h) == 0 || User32.IsIconic(h) != 0 || User32.IsZoomed(h) != 0) return false;
        int cloaked = 0;
        if (Dwm.DwmGetWindowAttribute(h, C.DWMWA_CLOAKED, &cloaked, 4) == 0 && cloaked != 0) return false;
        RECT rr;
        if (Dwm.DwmGetWindowAttribute(h, C.DWMWA_EXTENDED_FRAME_BOUNDS, &rr, (uint)sizeof(RECT)) != 0 && User32.GetWindowRect(h, &rr) == 0) return false;
        r = rr;
        return true;
    }
}

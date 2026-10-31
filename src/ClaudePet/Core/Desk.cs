using System.Runtime.InteropServices;
using ClaudePet.Native;
using ClaudePet.Util;

namespace ClaudePet.Core;

/// <summary>Un écran : son rectangle, sa zone de travail (le sol est en bas de celle-ci), son échelle.</summary>
internal sealed class Screen
{
    public nint Handle;
    public RECT Bounds, Work;
    public uint Dpi = 96;
    public bool Primary;
    public bool Fullscreen;
    public int Index;
    public double Scale => Dpi / 96.0;
    public double Floor => Work.Bottom;
    public double CenterX => (Work.Left + Work.Right) * 0.5;
    public override string ToString() => $"#{Index}{(Primary ? "*" : "")} {Bounds} work={Work} dpi={Dpi}{(Fullscreen ? " PLEIN-ÉCRAN" : "")}";
}

/// <summary>
/// Le « monde » de Pet : l'union des zones de travail des écrans (moins ceux en plein écran).
/// Tout ce qui est en dehors est solide : bords extérieurs, plafond, sol, zones mortes entre écrans.
/// </summary>
internal sealed unsafe class Desk
{
    public readonly List<Screen> Screens = new();
    public Screen Primary = new();
    public int Version;
    private static readonly List<nint> found = new();

    [UnmanagedCallersOnly]
    private static int EnumProc(nint monitor, nint hdc, RECT* rect, nint data)
    {
        found.Add(monitor);
        return 1;
    }

    /// <summary>Relit les écrans. Renvoie vrai si quelque chose a changé.</summary>
    public bool Refresh()
    {
        found.Clear();
        User32.EnumDisplayMonitors(0, null, &EnumProc, 0);
        var list = new List<Screen>();
        foreach (var h in found)
        {
            MONITORINFOEXW mi = new() { cbSize = (uint)sizeof(MONITORINFOEXW) };
            if (User32.GetMonitorInfo(h, &mi) == 0) continue;
            uint dx = 96, dy = 96;
            if (Shcore.GetDpiForMonitor(h, C.MDT_EFFECTIVE_DPI, &dx, &dy) != 0) dx = 96;
            list.Add(new Screen
            {
                Handle = h, Bounds = mi.rcMonitor, Work = mi.rcWork, Dpi = dx,
                Primary = (mi.dwFlags & C.MONITORINFOF_PRIMARY) != 0,
            });
        }
        if (list.Count == 0) return false;
        list.Sort((a, b) => a.Bounds.Left != b.Bounds.Left ? a.Bounds.Left.CompareTo(b.Bounds.Left) : a.Bounds.Top.CompareTo(b.Bounds.Top));

        bool changed = list.Count != Screens.Count;
        for (int i = 0; !changed && i < list.Count; i++)
        {
            var a = list[i]; var b = Screens[i];
            changed = a.Handle != b.Handle || !Same(a.Bounds, b.Bounds) || !Same(a.Work, b.Work) || a.Dpi != b.Dpi || a.Primary != b.Primary;
        }
        if (!changed) return false;

        // garder l'état plein écran connu pour les écrans qui restent
        foreach (var s in list)
            foreach (var old in Screens)
                if (old.Handle == s.Handle) s.Fullscreen = old.Fullscreen;

        Screens.Clear();
        Screens.AddRange(list);
        for (int i = 0; i < Screens.Count; i++) Screens[i].Index = i;
        Primary = Screens.Find(s => s.Primary) ?? Screens[0];
        Version++;
        Log.Write("Écrans : " + string.Join(" | ", Screens));
        return true;
    }

    private static bool Same(RECT a, RECT b) => a.Left == b.Left && a.Top == b.Top && a.Right == b.Right && a.Bottom == b.Bottom;

    public Screen? At(double x, double y)
    {
        foreach (var s in Screens)
            if (x >= s.Bounds.Left && x < s.Bounds.Right && y >= s.Bounds.Top && y < s.Bounds.Bottom) return s;
        return null;
    }

    public Screen Nearest(double x, double y)
    {
        Screen best = Primary;
        double bd = double.MaxValue;
        foreach (var s in Screens)
        {
            double dx = x < s.Bounds.Left ? s.Bounds.Left - x : x >= s.Bounds.Right ? x - s.Bounds.Right + 1 : 0;
            double dy = y < s.Bounds.Top ? s.Bounds.Top - y : y >= s.Bounds.Bottom ? y - s.Bounds.Bottom + 1 : 0;
            double d = dx * dx + dy * dy;
            if (d < bd) { bd = d; best = s; }
        }
        return best;
    }

    /// <summary>Écran sur lequel se trouve un point (ou le plus proche), parmi ceux qui ne sont pas en plein écran.</summary>
    public Screen? NearestFree(double x, double y, Screen? except = null)
    {
        Screen? best = null;
        double bd = double.MaxValue;
        foreach (var s in Screens)
        {
            if (s.Fullscreen || s == except) continue;
            double cx = Math.Clamp(x, s.Work.Left, s.Work.Right - 1), cy = Math.Clamp(y, s.Work.Top, s.Work.Bottom - 1);
            double d = (cx - x) * (cx - x) + (cy - y) * (cy - y);
            if (d < bd) { bd = d; best = s; }
        }
        return best;
    }

    public bool AllFullscreen => Screens.TrueForAll(s => s.Fullscreen);

    /// <summary>
    /// Vrai si la boîte [x0,x1] x [y0,y1] est entièrement dans l'espace libre.
    /// <paramref name="allow"/> : un écran en plein écran qu'on accepte quand même (celui où est Pet).
    /// </summary>
    public bool IsFree(double x0, double y0, double x1, double y1, Screen? allow = null)
    {
        const double eps = 0.25;
        Span<double> a = stackalloc double[8];
        Span<double> b = stackalloc double[8];
        int n = 0;
        foreach (var s in Screens)
        {
            if (s.Fullscreen && s != allow) continue;
            var w = s.Work;
            if (w.Top > y0 + eps || y1 > w.Bottom + eps) continue;
            if (w.Right <= x0 || w.Left >= x1) continue;
            if (n >= 8) break;
            a[n] = Math.Max(w.Left, x0);
            b[n] = Math.Min(w.Right, x1);
            n++;
        }
        if (n == 0) return false;
        // tri par début d'intervalle (petit n)
        for (int i = 1; i < n; i++)
            for (int j = i; j > 0 && a[j] < a[j - 1]; j--)
            {
                (a[j], a[j - 1]) = (a[j - 1], a[j]);
                (b[j], b[j - 1]) = (b[j - 1], b[j]);
            }
        double cover = x0;
        for (int i = 0; i < n; i++)
        {
            if (a[i] > cover + eps) return false;
            if (b[i] > cover) cover = b[i];
        }
        return cover >= x1 - eps;
    }
}

using ClaudePet.Native;
using ClaudePet.Util;

namespace ClaudePet.Core;

/// <summary>
/// Retrouve une icône du bureau (le raccourci « Pet ») et sa position à l'écran.
/// Méthode : IShellWindows → bureau → IShellBrowser → IShellView → IFolderView (GetItemPosition, GetSpacing)
/// et IFolderView2 (taille des icônes). Uniquement des appels COM documentés : rien dans la mémoire d'un autre processus.
/// </summary>
internal static unsafe class DesktopIcons
{
    private static Guid CLSID_ShellWindows = new("9BA05972-F6A8-11CF-A442-00A0C90A8F39");
    private static Guid IID_IShellWindows = new("85CB6900-4D95-11CF-960C-0080C7F4EE85");
    private static Guid IID_IServiceProvider = new("6D5140C1-7436-11CE-8034-00AA006009FA");
    private static Guid SID_STopLevelBrowser = new("4C96BE40-915C-11CF-99D3-00AA004AE837");
    private static Guid IID_IShellBrowser = new("000214E2-0000-0000-C000-000000000046");
    private static Guid IID_IFolderView = new("CDE725B0-CCC9-4519-917E-325D72FAB4CE");
    private static Guid IID_IFolderView2 = new("1AF3A467-214F-4298-908E-06B03E0B39F9");
    private static Guid IID_IShellFolder = new("000214E6-0000-0000-C000-000000000046");

    public struct Icon
    {
        public RECT Rect;     // rectangle de l'image de l'icône, en pixels écran
        public int Size;      // taille de l'image en pixels
        public string How;    // comment on l'a trouvée (pour le rapport et le journal)
    }

    public enum Result { Found, NotFound, ShellNotReady }

    /// <summary>Cherche une icône du bureau par son nom (« Pet » ou « Pet.lnk »).</summary>
    public static Result Find(string name, out Icon icon)
    {
        icon = default;
        void* sw = null, disp = null, sp = null, browser = null, view = null, fv = null, fv2 = null, folder = null;
        try
        {
            Guid clsid = CLSID_ShellWindows, iid = IID_IShellWindows;
            int hr = Ole32.CoCreateInstance(&clsid, 0, C.CLSCTX_ALL, &iid, &sw);
            if (hr < 0 || sw == null) { Log.Write($"Bureau : ShellWindows indisponible (0x{hr:X8})"); return Result.ShellNotReady; }

            VARIANT loc = default; loc.vt = 3; loc.intVal = 0; // VT_I4, CSIDL_DESKTOP
            VARIANT empty = default;
            int hwnd = 0;
            // IShellWindows::FindWindowSW (index 15) : SWC_DESKTOP = 8, SWFO_NEEDDISPATCH = 1
            hr = ((delegate* unmanaged<void*, VARIANT*, VARIANT*, int, int*, int, void**, int>)Com.Vtbl(sw)[15])(sw, &loc, &empty, 8, &hwnd, 1, &disp);
            if (hr != 0 || disp == null) { Log.Write($"Bureau : FindWindowSW a échoué (0x{hr:X8})"); return Result.ShellNotReady; }

            Guid isp = IID_IServiceProvider;
            if (Com.QueryInterface(disp, &isp, &sp) < 0 || sp == null) return Result.ShellNotReady;
            Guid sid = SID_STopLevelBrowser, isb = IID_IShellBrowser;
            hr = ((delegate* unmanaged<void*, Guid*, Guid*, void**, int>)Com.Vtbl(sp)[3])(sp, &sid, &isb, &browser);
            if (hr < 0 || browser == null) return Result.ShellNotReady;
            // IShellBrowser::QueryActiveShellView (index 15)
            hr = ((delegate* unmanaged<void*, void**, int>)Com.Vtbl(browser)[15])(browser, &view);
            if (hr < 0 || view == null) return Result.ShellNotReady;
            Guid ifv = IID_IFolderView;
            if (Com.QueryInterface(view, &ifv, &fv) < 0 || fv == null) return Result.ShellNotReady;
            Guid isf = IID_IShellFolder;
            // IFolderView::GetFolder (index 5)
            hr = ((delegate* unmanaged<void*, Guid*, void**, int>)Com.Vtbl(fv)[5])(fv, &isf, &folder);
            if (hr < 0 || folder == null) return Result.ShellNotReady;

            int count = 0;
            // IFolderView::ItemCount (index 7), SVGIO_ALLVIEW = 2
            ((delegate* unmanaged<void*, uint, int*, int>)Com.Vtbl(fv)[7])(fv, 2, &count);

            int found = -1;
            POINT pos = default;
            char* buf = stackalloc char[300];
            for (int i = 0; i < count && found < 0; i++)
            {
                void* pidl = null;
                // IFolderView::Item (index 6)
                if (((delegate* unmanaged<void*, int, void**, int>)Com.Vtbl(fv)[6])(fv, i, &pidl) < 0 || pidl == null) continue;
                try
                {
                    string parse = DisplayName(folder, pidl, 0x8001, buf); // SHGDN_INFOLDER | SHGDN_FORPARSING
                    string shown = DisplayName(folder, pidl, 0x0001, buf); // SHGDN_INFOLDER
                    bool match = parse.Equals(name + ".lnk", StringComparison.OrdinalIgnoreCase)
                              || shown.Equals(name, StringComparison.OrdinalIgnoreCase)
                              || parse.Equals(name, StringComparison.OrdinalIgnoreCase);
                    if (match)
                    {
                        POINT p;
                        // IFolderView::GetItemPosition (index 11)
                        if (((delegate* unmanaged<void*, void*, POINT*, int>)Com.Vtbl(fv)[11])(fv, pidl, &p) >= 0)
                        {
                            found = i;
                            pos = p;
                        }
                    }
                }
                finally { Ole32.CoTaskMemFree(pidl); }
            }
            if (found < 0) { Log.Write($"Bureau : icône « {name} » introuvable parmi {count} éléments"); return Result.NotFound; }

            // Taille des icônes (IFolderView2::GetViewModeAndIconSize, index 36)
            int iconLogical = 48;
            Guid ifv2 = IID_IFolderView2;
            if (Com.QueryInterface(fv, &ifv2, &fv2) >= 0 && fv2 != null)
            {
                int mode = 0, sz = 0;
                if (((delegate* unmanaged<void*, int*, int*, int>)Com.Vtbl(fv2)[36])(fv2, &mode, &sz) >= 0 && sz > 0) iconLogical = sz;
            }

            // Fenêtre de la liste du bureau (IOleWindow::GetWindow, index 3, puis l'enfant SysListView32)
            nint defView = 0;
            ((delegate* unmanaged<void*, nint*, int>)Com.Vtbl(view)[3])(view, &defView);
            nint list = defView != 0 ? User32.FindWindowEx(defView, 0, "SysListView32", null) : 0;
            if (list == 0) { Log.Write("Bureau : liste des icônes introuvable"); return Result.ShellNotReady; }
            if (User32.IsWindowVisible(list) == 0) { Log.Write("Bureau : les icônes du bureau sont masquées"); return Result.NotFound; }

            // Rectangle de l'image calculé à partir de ce que donne l'Explorateur : coin de la case de l'élément,
            // espacement entre éléments et taille des icônes. L'image est centrée en largeur dans sa case, près du haut.
            // (On ne lit ni n'écrit jamais dans la mémoire de l'Explorateur : les antivirus prennent ça pour une injection.)
            {
                POINT p = pos;
                User32.ClientToScreen(list, &p);
                nint mon = User32.MonitorFromPoint(p, C.MONITOR_DEFAULTTONEAREST);
                uint dx = 96, dy = 96;
                Shcore.GetDpiForMonitor(mon, C.MDT_EFFECTIVE_DPI, &dx, &dy);
                int px = (int)Math.Round(iconLogical * dx / 96.0);
                POINT spacing = default;
                ((delegate* unmanaged<void*, POINT*, int>)Com.Vtbl(fv)[12])(fv, &spacing); // GetSpacing
                int cellW = spacing.X > px ? spacing.X : px + (int)Math.Round(28 * dx / 96.0);
                int left = p.X + (cellW - px) / 2, top = p.Y + (int)Math.Round(IconTopPad * dx / 96.0);
                icon.Rect = new RECT(left, top, left + px, top + px);
                icon.How = "IFolderView (position, espacement, taille)";
                Log.Write($"Bureau : position {pos.X},{pos.Y} (écran {p.X},{p.Y}), espacement {spacing.X}x{spacing.Y}, dpi {dx}");
            }
            // l'image de l'icône est carrée : on garde le carré centré dans le rectangle trouvé
            int size = Math.Min(icon.Rect.Width, icon.Rect.Height);
            int expected = (int)Math.Round(iconLogical * DpiAt(icon.Rect) / 96.0);
            if (expected > 0 && expected < size) size = expected;
            int cx = (icon.Rect.Left + icon.Rect.Right) / 2, cy = (icon.Rect.Top + icon.Rect.Bottom) / 2;
            icon.Rect = new RECT(cx - size / 2, cy - size / 2, cx - size / 2 + size, cy - size / 2 + size);
            icon.Size = size;
            Log.Write($"Bureau : icône « {name} » trouvée {icon.Rect} (taille {size}, réglage {iconLogical}) par {icon.How}");
            return size > 0 ? Result.Found : Result.NotFound;
        }
        catch (Exception e)
        {
            Log.Write("Bureau : erreur " + e.Message);
            return Result.ShellNotReady;
        }
        finally
        {
            Com.Release(folder); Com.Release(fv2); Com.Release(fv); Com.Release(view);
            Com.Release(browser); Com.Release(sp); Com.Release(disp); Com.Release(sw);
        }
    }

    private static uint DpiAt(RECT r)
    {
        nint mon = User32.MonitorFromRect(&r, C.MONITOR_DEFAULTTONEAREST);
        uint dx = 96, dy = 96;
        Shcore.GetDpiForMonitor(mon, C.MDT_EFFECTIVE_DPI, &dx, &dy);
        return dx;
    }

    private static string DisplayName(void* folder, void* pidl, uint flags, char* buf)
    {
        STRRET sr = default;
        // IShellFolder::GetDisplayNameOf (index 11)
        if (((delegate* unmanaged<void*, void*, uint, STRRET*, int>)Com.Vtbl(folder)[11])(folder, pidl, flags, &sr) < 0) return "";
        if (Shlwapi.StrRetToBuf(&sr, pidl, buf, 300) < 0) return "";
        return new string(buf);
    }

    /// <summary>Marge entre le haut de la case d'un élément du bureau et l'image de son icône (pixels à 100 %).</summary>
    private const int IconTopPad = 2;
}

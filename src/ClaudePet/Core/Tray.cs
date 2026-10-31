using ClaudePet.Art;
using ClaudePet.Gfx;
using ClaudePet.Native;

namespace ClaudePet.Core;

/// <summary>Icône dans la zone de notification, avec un petit menu (Calme, Muet, Langue, Rentrer à la maison).</summary>
internal sealed unsafe class Tray
{
    public const uint CallbackMessage = C.WM_APP + 1;
    public const uint CmdCalm = 1001, CmdMute = 1002, CmdHome = 1003, CmdEnglish = 1004, CmdFrench = 1005;
    private const uint Id = 1;
    private nint hwnd, icon;
    private bool added;

    public void Add(nint owner)
    {
        hwnd = owner;
        uint dpi = User32.GetDpiForSystem();
        int size = User32.GetSystemMetricsForDpi(C.SM_CXSMICON, dpi);
        if (size <= 0) size = 16;
        if (icon != 0) User32.DestroyIcon(icon);
        icon = IconFactory.Create(HouseArt.Render(size), size);

        NOTIFYICONDATAW nid = Base();
        nid.uFlags = C.NIF_MESSAGE | C.NIF_ICON | C.NIF_TIP | C.NIF_SHOWTIP;
        nid.uCallbackMessage = CallbackMessage;
        nid.hIcon = icon;
        SetTip(ref nid, "Claude Pet");
        added = Shell32.Shell_NotifyIcon(C.NIM_ADD, &nid) != 0;
        if (!added)
        {
            // l'icône existe peut-être déjà (redémarrage de l'Explorateur) : on la met à jour
            added = Shell32.Shell_NotifyIcon(C.NIM_MODIFY, &nid) != 0;
        }
        nid.uVersion = C.NOTIFYICON_VERSION_4;
        Shell32.Shell_NotifyIcon(C.NIM_SETVERSION, &nid);
        ClaudePet.Util.Log.Write($"Icône de notification : {(added ? "ajoutée" : "ÉCHEC")} ({size} px)");
    }

    public void Remove()
    {
        if (added)
        {
            NOTIFYICONDATAW nid = Base();
            Shell32.Shell_NotifyIcon(C.NIM_DELETE, &nid);
            added = false;
        }
        if (icon != 0) { User32.DestroyIcon(icon); icon = 0; }
    }

    private NOTIFYICONDATAW Base()
    {
        NOTIFYICONDATAW nid = default;
        nid.cbSize = (uint)sizeof(NOTIFYICONDATAW);
        nid.hWnd = hwnd;
        nid.uID = Id;
        return nid;
    }

    private static void SetTip(ref NOTIFYICONDATAW nid, string text)
    {
        fixed (char* tip = nid.szTip)
        {
            int n = Math.Min(text.Length, 127);
            for (int i = 0; i < n; i++) tip[i] = text[i];
            tip[n] = '\0';
        }
    }

    /// <summary>Affiche le menu et renvoie la commande choisie (0 si aucune).</summary>
    public uint ShowMenu(int x, int y, bool calm, bool muted)
    {
        nint menu = User32.CreatePopupMenu();
        User32.AppendMenu(menu, C.MF_STRING | C.MF_GRAYED, 0, "Claude Pet");
        User32.AppendMenu(menu, C.MF_SEPARATOR, 0, null);
        User32.AppendMenu(menu, C.MF_STRING | (calm ? C.MF_CHECKED : 0), CmdCalm, Lang.Calm);
        User32.AppendMenu(menu, C.MF_STRING | (muted ? C.MF_CHECKED : 0), CmdMute, Lang.Mute);
        // chaque langue écrite dans sa propre langue ; DestroyMenu détruit aussi ce sous-menu
        nint langs = User32.CreatePopupMenu();
        User32.AppendMenu(langs, C.MF_STRING | C.MF_RADIOCHECK | (Lang.French ? 0 : C.MF_CHECKED), CmdEnglish, "English");
        User32.AppendMenu(langs, C.MF_STRING | C.MF_RADIOCHECK | (Lang.French ? C.MF_CHECKED : 0), CmdFrench, "Français");
        User32.AppendMenu(menu, C.MF_STRING | C.MF_POPUP, (nuint)langs, Lang.Language);
        User32.AppendMenu(menu, C.MF_SEPARATOR, 0, null);
        User32.AppendMenu(menu, C.MF_STRING, CmdHome, Lang.GoHome);
        User32.SetForegroundWindow(hwnd);
        uint cmd = (uint)User32.TrackPopupMenuEx(menu, C.TPM_RIGHTBUTTON | C.TPM_RETURNCMD | C.TPM_NONOTIFY | C.TPM_BOTTOMALIGN,
            x, y, hwnd, 0);
        User32.PostMessage(hwnd, C.WM_NULL, 0, 0);
        User32.DestroyMenu(menu);
        return cmd;
    }
}

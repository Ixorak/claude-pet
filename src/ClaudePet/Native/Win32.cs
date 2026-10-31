using System.Runtime.InteropServices;

namespace ClaudePet.Native;

// Déclarations Win32 (blittables uniquement : compatibles avec la compilation native AOT).

[StructLayout(LayoutKind.Sequential)]
internal struct POINT
{
    public int X, Y;
    public POINT(int x, int y) { X = x; Y = y; }
}

[StructLayout(LayoutKind.Sequential)]
internal struct SIZE
{
    public int CX, CY;
    public SIZE(int cx, int cy) { CX = cx; CY = cy; }
}

[StructLayout(LayoutKind.Sequential)]
internal struct RECT
{
    public int Left, Top, Right, Bottom;
    public RECT(int l, int t, int r, int b) { Left = l; Top = t; Right = r; Bottom = b; }
    public readonly int Width => Right - Left;
    public readonly int Height => Bottom - Top;
    public readonly bool Contains(int x, int y) => x >= Left && x < Right && y >= Top && y < Bottom;
    public override readonly string ToString() => $"[{Left},{Top} {Width}x{Height}]";
}

[StructLayout(LayoutKind.Sequential)]
internal struct MSG
{
    public nint hwnd;
    public uint message;
    public nuint wParam;
    public nint lParam;
    public uint time;
    public POINT pt;
    public uint lPrivate;
}

[StructLayout(LayoutKind.Sequential)]
internal unsafe struct WNDCLASSEXW
{
    public uint cbSize;
    public uint style;
    public delegate* unmanaged<nint, uint, nuint, nint, nint> lpfnWndProc;
    public int cbClsExtra;
    public int cbWndExtra;
    public nint hInstance;
    public nint hIcon;
    public nint hCursor;
    public nint hbrBackground;
    public char* lpszMenuName;
    public char* lpszClassName;
    public nint hIconSm;
}

[StructLayout(LayoutKind.Sequential)]
internal struct BLENDFUNCTION
{
    public byte BlendOp, BlendFlags, SourceConstantAlpha, AlphaFormat;
}

[StructLayout(LayoutKind.Sequential)]
internal struct BITMAPINFOHEADER
{
    public uint biSize;
    public int biWidth;
    public int biHeight;
    public ushort biPlanes;
    public ushort biBitCount;
    public uint biCompression;
    public uint biSizeImage;
    public int biXPelsPerMeter;
    public int biYPelsPerMeter;
    public uint biClrUsed;
    public uint biClrImportant;
}

[StructLayout(LayoutKind.Sequential)]
internal unsafe struct MONITORINFOEXW
{
    public uint cbSize;
    public RECT rcMonitor;
    public RECT rcWork;
    public uint dwFlags;
    public fixed char szDevice[32];
}

[StructLayout(LayoutKind.Sequential)]
internal unsafe struct NOTIFYICONDATAW
{
    public uint cbSize;
    public nint hWnd;
    public uint uID;
    public uint uFlags;
    public uint uCallbackMessage;
    public nint hIcon;
    public fixed char szTip[128];
    public uint dwState;
    public uint dwStateMask;
    public fixed char szInfo[256];
    public uint uVersion;
    public fixed char szInfoTitle[64];
    public uint dwInfoFlags;
    public Guid guidItem;
    public nint hBalloonIcon;
}

[StructLayout(LayoutKind.Sequential)]
internal struct ICONINFO
{
    public int fIcon;
    public int xHotspot;
    public int yHotspot;
    public nint hbmMask;
    public nint hbmColor;
}

[StructLayout(LayoutKind.Sequential)]
internal struct LASTINPUTINFO
{
    public uint cbSize;
    public uint dwTime;
}

[StructLayout(LayoutKind.Sequential)]
internal struct COPYDATASTRUCT
{
    public nuint dwData;
    public uint cbData;
    public nint lpData;
}

[StructLayout(LayoutKind.Sequential, Pack = 1)]
internal struct WAVEFORMATEX
{
    public ushort wFormatTag;
    public ushort nChannels;
    public uint nSamplesPerSec;
    public uint nAvgBytesPerSec;
    public ushort nBlockAlign;
    public ushort wBitsPerSample;
    public ushort cbSize;
}

[StructLayout(LayoutKind.Sequential)]
internal struct WAVEHDR
{
    public nint lpData;
    public uint dwBufferLength;
    public uint dwBytesRecorded;
    public nuint dwUser;
    public uint dwFlags;
    public uint dwLoops;
    public nint lpNext;
    public nuint reserved;
}

/// <summary>VARIANT (24 octets en 64 bits) : seuls VT_EMPTY et VT_I4 servent ici.</summary>
[StructLayout(LayoutKind.Explicit, Size = 24)]
internal struct VARIANT
{
    [FieldOffset(0)] public ushort vt;
    [FieldOffset(8)] public int intVal;
    [FieldOffset(8)] public long llVal;
}

/// <summary>STRRET (264 octets en 64 bits).</summary>
[StructLayout(LayoutKind.Explicit, Size = 272)]
internal struct STRRET
{
    [FieldOffset(0)] public uint uType;
}

internal static class C
{
    // Styles de fenêtre
    public const uint WS_POPUP = 0x80000000;
    public const uint WS_EX_LAYERED = 0x00080000;
    public const uint WS_EX_TRANSPARENT = 0x00000020;
    public const uint WS_EX_TOPMOST = 0x00000008;
    public const uint WS_EX_TOOLWINDOW = 0x00000080;
    public const uint WS_EX_NOACTIVATE = 0x08000000;
    public const uint WS_EX_APPWINDOW = 0x00040000;
    public const uint WS_CAPTION = 0x00C00000;
    public const uint WS_CHILD = 0x40000000;
    public const int GWL_STYLE = -16;
    public const int GWL_EXSTYLE = -20;
    public const uint WS_THICKFRAME = 0x00040000;
    public const uint GW_OWNER = 4;
    public const int GWLP_USERDATA = -21;

    // Messages
    public const uint WM_NULL = 0x0000;
    public const uint WM_CREATE = 0x0001;
    public const uint WM_DESTROY = 0x0002;
    public const uint WM_ACTIVATE = 0x0006;
    public const uint WM_CLOSE = 0x0010;
    public const uint WM_QUERYENDSESSION = 0x0011;
    public const uint WM_QUIT = 0x0012;
    public const uint WM_ENDSESSION = 0x0016;
    public const uint WM_SETTINGCHANGE = 0x001A;
    public const uint WM_SETCURSOR = 0x0020;
    public const uint WM_MOUSEACTIVATE = 0x0021;
    public const uint WM_COPYDATA = 0x004A;
    public const uint WM_CONTEXTMENU = 0x007B;
    public const uint WM_DISPLAYCHANGE = 0x007E;
    public const uint WM_NCHITTEST = 0x0084;
    public const uint WM_KEYDOWN = 0x0100;
    public const uint WM_COMMAND = 0x0111;
    public const uint WM_TIMER = 0x0113;
    public const uint WM_MOUSEMOVE = 0x0200;
    public const uint WM_LBUTTONDOWN = 0x0201;
    public const uint WM_LBUTTONUP = 0x0202;
    public const uint WM_LBUTTONDBLCLK = 0x0203;
    public const uint WM_RBUTTONDOWN = 0x0204;
    public const uint WM_RBUTTONUP = 0x0205;
    public const uint WM_RBUTTONDBLCLK = 0x0206;
    public const uint WM_MBUTTONDOWN = 0x0207;
    public const uint WM_MBUTTONUP = 0x0208;
    public const uint WM_MOUSEWHEEL = 0x020A;
    public const uint WM_XBUTTONDOWN = 0x020B;
    public const uint WM_XBUTTONUP = 0x020C;
    public const uint WM_CAPTURECHANGED = 0x0215;
    public const uint WM_POWERBROADCAST = 0x0218;
    public const uint WM_WTSSESSION_CHANGE = 0x02B1;
    public const uint WM_DPICHANGED = 0x02E0;
    public const uint WM_USER = 0x0400;
    public const uint WM_APP = 0x8000;
    public const uint WM_NCLBUTTONDOWN = 0x00A1;

    public const int MA_NOACTIVATE = 3;
    public const int HTCLIENT = 1;
    public const int HTTRANSPARENT = -1;

    // ShowWindow
    public const int SW_HIDE = 0;
    public const int SW_SHOWNOACTIVATE = 4;
    public const int SW_SHOWNA = 8;

    // SetWindowPos
    public static readonly nint HWND_TOPMOST = -1;
    public static readonly nint HWND_NOTOPMOST = -2;
    public static readonly nint HWND_MESSAGE = -3;
    public const uint SWP_NOSIZE = 0x0001;
    public const uint SWP_NOMOVE = 0x0002;
    public const uint SWP_NOZORDER = 0x0004;
    public const uint SWP_NOREDRAW = 0x0008;
    public const uint SWP_NOACTIVATE = 0x0010;
    public const uint SWP_FRAMECHANGED = 0x0020;
    public const uint SWP_SHOWWINDOW = 0x0040;
    public const uint SWP_HIDEWINDOW = 0x0080;
    public const uint SWP_NOOWNERZORDER = 0x0200;
    public const uint SWP_NOSENDCHANGING = 0x0400;
    public const uint SWP_ASYNCWINDOWPOS = 0x4000;

    // UpdateLayeredWindow
    public const uint ULW_ALPHA = 0x00000002;
    public const byte AC_SRC_OVER = 0x00;
    public const byte AC_SRC_ALPHA = 0x01;

    // DIB
    public const uint BI_RGB = 0;
    public const uint DIB_RGB_COLORS = 0;

    // PeekMessage / MsgWait
    public const uint PM_REMOVE = 0x0001;
    public const uint QS_ALLINPUT = 0x04FF;
    public const uint MWMO_INPUTAVAILABLE = 0x0004;
    public const uint WAIT_OBJECT_0 = 0;
    public const uint WAIT_TIMEOUT = 0x102;
    public const uint INFINITE = 0xFFFFFFFF;

    // Moniteurs
    public const uint MONITOR_DEFAULTTONULL = 0;
    public const uint MONITOR_DEFAULTTOPRIMARY = 1;
    public const uint MONITOR_DEFAULTTONEAREST = 2;
    public const uint MONITORINFOF_PRIMARY = 1;
    public const int MDT_EFFECTIVE_DPI = 0;

    // Métriques
    public const int SM_CXSCREEN = 0;
    public const int SM_CYSCREEN = 1;
    public const int SM_CXSMICON = 49;
    public const int SM_CYSMICON = 50;
    public const int SM_CXICON = 11;
    public const int SM_CXDRAG = 68;
    public const int SM_CYDRAG = 69;
    public const int SM_XVIRTUALSCREEN = 76;
    public const int SM_YVIRTUALSCREEN = 77;
    public const int SM_CXVIRTUALSCREEN = 78;
    public const int SM_CYVIRTUALSCREEN = 79;
    public const int SM_SWAPBUTTON = 23;

    // Curseurs
    public const int IDC_ARROW = 32512;
    public const int IDC_HAND = 32649;

    // Menus
    public const uint MF_STRING = 0x0000;
    public const uint MF_GRAYED = 0x0001;
    public const uint MF_DISABLED = 0x0002;
    public const uint MF_CHECKED = 0x0008;
    public const uint MF_SEPARATOR = 0x0800;
    public const uint TPM_RIGHTBUTTON = 0x0002;
    public const uint TPM_RETURNCMD = 0x0100;
    public const uint TPM_NONOTIFY = 0x0080;
    public const uint TPM_BOTTOMALIGN = 0x0020;
    public const uint TPM_RIGHTALIGN = 0x0008;

    // Zone de notification
    public const uint NIM_ADD = 0;
    public const uint NIM_MODIFY = 1;
    public const uint NIM_DELETE = 2;
    public const uint NIM_SETVERSION = 4;
    public const uint NIF_MESSAGE = 0x01;
    public const uint NIF_ICON = 0x02;
    public const uint NIF_TIP = 0x04;
    public const uint NIF_SHOWTIP = 0x80;
    public const uint NOTIFYICON_VERSION_4 = 4;
    public const uint NIN_SELECT = WM_USER + 0;
    public const uint NIN_KEYSELECT = WM_USER + 1;

    // DWM
    public const uint DWMWA_EXTENDED_FRAME_BOUNDS = 9;
    public const uint DWMWA_CLOAKED = 14;

    // Énergie / session
    public const uint PBT_APMSUSPEND = 0x0004;
    public const uint PBT_APMRESUMEAUTOMATIC = 0x0012;
    public const uint PBT_APMRESUMESUSPEND = 0x0007;
    public const uint WTS_SESSION_LOCK = 0x7;
    public const uint WTS_SESSION_UNLOCK = 0x8;
    public const uint NOTIFY_FOR_THIS_SESSION = 0;
    public const uint SPI_SETWORKAREA = 0x002F;

    // Divers
    public const uint ERROR_ALREADY_EXISTS = 183;
    public const uint CREATE_WAITABLE_TIMER_HIGH_RESOLUTION = 0x00000002;
    public const uint TIMER_ALL_ACCESS = 0x1F0003;
    public const uint COINIT_APARTMENTTHREADED = 0x2;
    public const uint CLSCTX_LOCAL_SERVER = 0x4;
    public const uint CLSCTX_ALL = 0x17;

    // waveOut
    public const uint WAVE_MAPPER = 0xFFFFFFFF;
    public const uint CALLBACK_EVENT = 0x00050000;
    public const uint WHDR_DONE = 0x00000001;
    public const ushort WAVE_FORMAT_PCM = 1;

    // Touches
    public const int VK_LBUTTON = 0x01;
    public const int VK_RBUTTON = 0x02;
    public const int VK_MBUTTON = 0x04;
    public const int VK_ESCAPE = 0x1B;

    // Activation
    public const int WA_INACTIVE = 0;
    public const int MA_ACTIVATE = 1;
}

internal static unsafe partial class User32
{
    private const string Dll = "user32.dll";

    [LibraryImport(Dll, EntryPoint = "RegisterClassExW", SetLastError = true)]
    public static partial ushort RegisterClassEx(WNDCLASSEXW* wc);

    [LibraryImport(Dll, EntryPoint = "CreateWindowExW", SetLastError = true, StringMarshalling = StringMarshalling.Utf16)]
    public static partial nint CreateWindowEx(uint exStyle, string className, string windowName, uint style,
        int x, int y, int width, int height, nint parent, nint menu, nint instance, nint param);

    [LibraryImport(Dll, EntryPoint = "DefWindowProcW")]
    public static partial nint DefWindowProc(nint hwnd, uint msg, nuint wParam, nint lParam);

    [LibraryImport(Dll)]
    public static partial int DestroyWindow(nint hwnd);

    [LibraryImport(Dll)]
    public static partial int ShowWindow(nint hwnd, int cmd);

    [LibraryImport(Dll)]
    public static partial int IsWindow(nint hwnd);

    [LibraryImport(Dll)]
    public static partial int IsWindowVisible(nint hwnd);

    [LibraryImport(Dll)]
    public static partial int IsIconic(nint hwnd);

    [LibraryImport(Dll)]
    public static partial int IsZoomed(nint hwnd);

    [LibraryImport(Dll)]
    public static partial int SetWindowPos(nint hwnd, nint after, int x, int y, int cx, int cy, uint flags);

    [LibraryImport(Dll)]
    public static partial int UpdateLayeredWindow(nint hwnd, nint hdcDst, POINT* pptDst, SIZE* psize,
        nint hdcSrc, POINT* pptSrc, uint crKey, BLENDFUNCTION* pblend, uint flags);

    [LibraryImport(Dll)]
    public static partial int GetCursorPos(POINT* pt);

    [LibraryImport(Dll)]
    public static partial nint GetDC(nint hwnd);

    [LibraryImport(Dll)]
    public static partial int ReleaseDC(nint hwnd, nint hdc);

    [LibraryImport(Dll, EntryPoint = "PeekMessageW")]
    public static partial int PeekMessage(MSG* msg, nint hwnd, uint min, uint max, uint remove);

    [LibraryImport(Dll)]
    public static partial int TranslateMessage(MSG* msg);

    [LibraryImport(Dll, EntryPoint = "DispatchMessageW")]
    public static partial nint DispatchMessage(MSG* msg);

    [LibraryImport(Dll, EntryPoint = "PostMessageW")]
    public static partial int PostMessage(nint hwnd, uint msg, nuint wParam, nint lParam);

    [LibraryImport(Dll, EntryPoint = "SendMessageW")]
    public static partial nint SendMessage(nint hwnd, uint msg, nuint wParam, nint lParam);

    [LibraryImport(Dll, EntryPoint = "SendMessageTimeoutW")]
    public static partial nint SendMessageTimeout(nint hwnd, uint msg, nuint wParam, nint lParam, uint flags, uint timeout, nuint* result);

    [LibraryImport(Dll)]
    public static partial void PostQuitMessage(int code);

    [LibraryImport(Dll)]
    public static partial uint MsgWaitForMultipleObjectsEx(uint count, nint* handles, uint ms, uint wakeMask, uint flags);

    [LibraryImport(Dll)]
    public static partial nint SetCapture(nint hwnd);

    [LibraryImport(Dll)]
    public static partial int ReleaseCapture();

    [LibraryImport(Dll)]
    public static partial nint GetCapture();

    [LibraryImport(Dll)]
    public static partial int GetSystemMetrics(int index);

    [LibraryImport(Dll)]
    public static partial int GetSystemMetricsForDpi(int index, uint dpi);

    [LibraryImport(Dll)]
    public static partial int EnumDisplayMonitors(nint hdc, RECT* clip, delegate* unmanaged<nint, nint, RECT*, nint, int> proc, nint data);

    [LibraryImport(Dll, EntryPoint = "GetMonitorInfoW")]
    public static partial int GetMonitorInfo(nint monitor, MONITORINFOEXW* info);

    [LibraryImport(Dll)]
    public static partial nint MonitorFromPoint(POINT pt, uint flags);

    [LibraryImport(Dll)]
    public static partial nint MonitorFromWindow(nint hwnd, uint flags);

    [LibraryImport(Dll)]
    public static partial nint MonitorFromRect(RECT* rect, uint flags);

    [LibraryImport(Dll, EntryPoint = "GetWindowLongPtrW")]
    public static partial nint GetWindowLongPtr(nint hwnd, int index);

    [LibraryImport(Dll, EntryPoint = "SetWindowLongPtrW")]
    public static partial nint SetWindowLongPtr(nint hwnd, int index, nint value);

    [LibraryImport(Dll)]
    public static partial int EnumWindows(delegate* unmanaged<nint, nint, int> proc, nint data);

    [LibraryImport(Dll)]
    public static partial int GetWindowRect(nint hwnd, RECT* rect);

    [LibraryImport(Dll, EntryPoint = "GetClassNameW")]
    public static partial int GetClassName(nint hwnd, char* buffer, int max);

    [LibraryImport(Dll)]
    public static partial uint GetWindowThreadProcessId(nint hwnd, uint* pid);

    [LibraryImport(Dll, EntryPoint = "FindWindowW", StringMarshalling = StringMarshalling.Utf16)]
    public static partial nint FindWindow(string? className, string? windowName);

    [LibraryImport(Dll, EntryPoint = "FindWindowExW", StringMarshalling = StringMarshalling.Utf16)]
    public static partial nint FindWindowEx(nint parent, nint childAfter, string? className, string? windowName);

    [LibraryImport(Dll)]
    public static partial int SetForegroundWindow(nint hwnd);

    [LibraryImport(Dll)]
    public static partial nint GetForegroundWindow();

    [LibraryImport(Dll)]
    public static partial nint CreatePopupMenu();

    [LibraryImport(Dll, EntryPoint = "AppendMenuW", StringMarshalling = StringMarshalling.Utf16)]
    public static partial int AppendMenu(nint menu, uint flags, nuint id, string? text);

    [LibraryImport(Dll)]
    public static partial int TrackPopupMenuEx(nint menu, uint flags, int x, int y, nint hwnd, nint tpm);

    [LibraryImport(Dll)]
    public static partial int DestroyMenu(nint menu);

    [LibraryImport(Dll, EntryPoint = "LoadCursorW")]
    public static partial nint LoadCursor(nint instance, nint cursorId);

    [LibraryImport(Dll)]
    public static partial nint SetCursor(nint cursor);

    [LibraryImport(Dll)]
    public static partial nint CreateIconIndirect(ICONINFO* info);

    [LibraryImport(Dll)]
    public static partial int DestroyIcon(nint icon);

    [LibraryImport(Dll)]
    public static partial uint GetDoubleClickTime();

    [LibraryImport(Dll, EntryPoint = "RegisterWindowMessageW", StringMarshalling = StringMarshalling.Utf16)]
    public static partial uint RegisterWindowMessage(string name);

    [LibraryImport(Dll)]
    public static partial int SetProcessDpiAwarenessContext(nint value);

    [LibraryImport(Dll)]
    public static partial uint GetDpiForWindow(nint hwnd);

    [LibraryImport(Dll)]
    public static partial uint GetDpiForSystem();

    [LibraryImport(Dll)]
    public static partial short GetAsyncKeyState(int vKey);

    [LibraryImport(Dll)]
    public static partial int GetLastInputInfo(LASTINPUTINFO* info);

    [LibraryImport(Dll)]
    public static partial int ClientToScreen(nint hwnd, POINT* pt);

    [LibraryImport(Dll)]
    public static partial nint GetAncestor(nint hwnd, uint flags);

    [LibraryImport(Dll)]
    public static partial nint GetWindow(nint hwnd, uint cmd);

    [LibraryImport(Dll)]
    public static partial nint WindowFromPoint(POINT pt);

    [LibraryImport(Dll)]
    public static partial nint GetShellWindow();

    [LibraryImport(Dll)]
    public static partial nint GetDesktopWindow();

    [LibraryImport(Dll, EntryPoint = "SystemParametersInfoW")]
    public static partial int SystemParametersInfo(uint action, uint param, void* pvParam, uint winIni);

    [LibraryImport(Dll)]
    public static partial nint SetWinEventHook(uint eventMin, uint eventMax, nint module,
        delegate* unmanaged<nint, uint, nint, int, int, uint, uint, void> proc, uint pid, uint tid, uint flags);

    [LibraryImport(Dll)]
    public static partial int UnhookWinEvent(nint hook);

    [LibraryImport(Dll)]
    public static partial int GetLayeredWindowAttributes(nint hwnd, uint* key, byte* alpha, uint* flags);

    [LibraryImport(Dll, EntryPoint = "GetWindowTextLengthW")]
    public static partial int GetWindowTextLength(nint hwnd);

    [LibraryImport(Dll)]
    public static partial int AllowSetForegroundWindow(uint pid);
}

internal static unsafe partial class Gdi32
{
    private const string Dll = "gdi32.dll";

    [LibraryImport(Dll)]
    public static partial nint CreateCompatibleDC(nint hdc);

    [LibraryImport(Dll)]
    public static partial int DeleteDC(nint hdc);

    [LibraryImport(Dll)]
    public static partial nint SelectObject(nint hdc, nint obj);

    [LibraryImport(Dll)]
    public static partial int DeleteObject(nint obj);

    [LibraryImport(Dll)]
    public static partial nint CreateDIBSection(nint hdc, BITMAPINFOHEADER* bmi, uint usage, void** bits, nint section, uint offset);

    [LibraryImport(Dll)]
    public static partial nint CreateBitmap(int width, int height, uint planes, uint bitCount, void* bits);
}

internal static unsafe partial class Kernel32
{
    private const string Dll = "kernel32.dll";

    [LibraryImport(Dll, EntryPoint = "CreateWaitableTimerExW", SetLastError = true)]
    public static partial nint CreateWaitableTimerEx(nint attributes, char* name, uint flags, uint access);

    [LibraryImport(Dll, SetLastError = true)]
    public static partial int SetWaitableTimer(nint timer, long* dueTime, int period, nint completion, nint arg, int resume);

    [LibraryImport(Dll)]
    public static partial int CloseHandle(nint handle);

    [LibraryImport(Dll, EntryPoint = "CreateMutexW", SetLastError = true, StringMarshalling = StringMarshalling.Utf16)]
    public static partial nint CreateMutex(nint attributes, int initialOwner, string name);

    [LibraryImport(Dll, EntryPoint = "CreateEventW", SetLastError = true)]
    public static partial nint CreateEvent(nint attributes, int manualReset, int initialState, char* name);

    [LibraryImport(Dll)]
    public static partial int SetEvent(nint handle);

    [LibraryImport(Dll)]
    public static partial uint WaitForSingleObject(nint handle, uint ms);

    [LibraryImport(Dll)]
    public static partial uint WaitForMultipleObjects(uint count, nint* handles, int waitAll, uint ms);

    [LibraryImport(Dll, EntryPoint = "GetModuleHandleW")]
    public static partial nint GetModuleHandle(char* name);

    [LibraryImport(Dll)]
    public static partial int QueryUnbiasedInterruptTime(ulong* time);

    [LibraryImport(Dll)]
    public static partial uint GetCurrentProcessId();

    [LibraryImport(Dll)]
    public static partial uint GetCurrentThreadId();

    [LibraryImport(Dll)]
    public static partial int SetPriorityClass(nint process, uint priority);

    [LibraryImport(Dll)]
    public static partial nint GetCurrentProcess();

    [LibraryImport(Dll)]
    public static partial int SetProcessInformation(nint process, int infoClass, void* info, uint size);
}

internal static unsafe partial class Shcore
{
    [LibraryImport("shcore.dll")]
    public static partial int GetDpiForMonitor(nint monitor, int type, uint* dpiX, uint* dpiY);
}

internal static unsafe partial class Dwm
{
    [LibraryImport("dwmapi.dll")]
    public static partial int DwmGetWindowAttribute(nint hwnd, uint attribute, void* value, uint size);
}

internal static unsafe partial class Shell32
{
    [LibraryImport("shell32.dll", EntryPoint = "Shell_NotifyIconW")]
    public static partial int Shell_NotifyIcon(uint message, NOTIFYICONDATAW* data);
}

internal static unsafe partial class Ole32
{
    [LibraryImport("ole32.dll")]
    public static partial int CoInitializeEx(nint reserved, uint coInit);

    [LibraryImport("ole32.dll")]
    public static partial void CoUninitialize();

    [LibraryImport("ole32.dll")]
    public static partial int CoCreateInstance(Guid* clsid, nint outer, uint context, Guid* iid, void** ppv);

    [LibraryImport("ole32.dll")]
    public static partial void CoTaskMemFree(void* p);

    [LibraryImport("oleaut32.dll")]
    public static partial int VariantClear(VARIANT* v);
}

internal static unsafe partial class Shlwapi
{
    [LibraryImport("shlwapi.dll", EntryPoint = "StrRetToBufW")]
    public static partial int StrRetToBuf(STRRET* str, void* pidl, char* buffer, uint size);
}

internal static unsafe partial class WinMM
{
    private const string Dll = "winmm.dll";

    [LibraryImport(Dll)]
    public static partial uint waveOutOpen(nint* handle, uint deviceId, WAVEFORMATEX* format, nint callback, nint instance, uint flags);

    [LibraryImport(Dll)]
    public static partial uint waveOutPrepareHeader(nint handle, WAVEHDR* header, uint size);

    [LibraryImport(Dll)]
    public static partial uint waveOutUnprepareHeader(nint handle, WAVEHDR* header, uint size);

    [LibraryImport(Dll)]
    public static partial uint waveOutWrite(nint handle, WAVEHDR* header, uint size);

    [LibraryImport(Dll)]
    public static partial uint waveOutReset(nint handle);

    [LibraryImport(Dll)]
    public static partial uint waveOutClose(nint handle);
}

internal static partial class WtsApi32
{
    [LibraryImport("wtsapi32.dll")]
    public static partial int WTSRegisterSessionNotification(nint hwnd, uint flags);

    [LibraryImport("wtsapi32.dll")]
    public static partial int WTSUnRegisterSessionNotification(nint hwnd);
}

/// <summary>Appels COM « à la main » par la table virtuelle (compatible AOT, sans marshalling).</summary>
internal static unsafe class Com
{
    public static void** Vtbl(void* p) => *(void***)p;

    public static int QueryInterface(void* p, Guid* iid, void** ppv)
        => ((delegate* unmanaged<void*, Guid*, void**, int>)Vtbl(p)[0])(p, iid, ppv);

    public static uint Release(void* p)
        => p == null ? 0 : ((delegate* unmanaged<void*, uint>)Vtbl(p)[2])(p);
}

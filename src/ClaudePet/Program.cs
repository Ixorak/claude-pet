using System.Runtime.InteropServices;
using ClaudePet.Core;
using ClaudePet.Native;
using ClaudePet.Util;

namespace ClaudePet;

internal static unsafe class Program
{
    [STAThread]
    private static int Main(string[] args)
    {
        // Conscience DPI par écran (Per-Monitor V2) : déjà dans le manifeste, on le redemande par sécurité.
        User32.SetProcessDpiAwarenessContext(-4);

        // Outil de test : envoyer une commande à l'instance en cours, puis quitter.
        int ci = Array.IndexOf(args, "--cmd");
        if (ci >= 0) return SendCommand(string.Join(' ', args.Skip(ci + 1)));

        if (args.Contains("--log")) Log.Open(Path.Combine(Config.Folder, "log.txt"));

        // Une seule instance : si Pet est déjà là, il rentre dans sa maison et l'appli se ferme.
        nint mutex = Kernel32.CreateMutex(0, 0, "Local\\ClaudePet.SingleInstance");
        if (Marshal.GetLastPInvokeError() == (int)C.ERROR_ALREADY_EXISTS)
        {
            nint other = User32.FindWindow(App.ControllerClass, null);
            if (other != 0)
            {
                uint pid;
                User32.GetWindowThreadProcessId(other, &pid);
                User32.AllowSetForegroundWindow(pid);
                User32.PostMessage(other, App.MsgGoHome, 0, 0);
            }
            return 0;
        }

        Ole32.CoInitializeEx(0, C.COINIT_APARTMENTTHREADED);
        var app = new App();
        App.I = app;
        try
        {
            app.Init(args);
            app.Run();
        }
        catch (Exception e)
        {
            CrashLog(e);
        }
        finally
        {
            try { app.Shutdown(); } catch (Exception e) { CrashLog(e); }
            Log.Close();
            Ole32.CoUninitialize();
            GC.KeepAlive(mutex);
            Kernel32.CloseHandle(mutex);
        }
        return 0;
    }

    private static void CrashLog(Exception e)
    {
        try
        {
            Directory.CreateDirectory(Config.Folder);
            File.AppendAllText(Path.Combine(Config.Folder, "crash.txt"), $"{DateTime.Now:yyyy-MM-dd HH:mm:ss} {e}\r\n\r\n");
        }
        catch { }
        Log.Write("PLANTAGE : " + e);
    }

    private static int SendCommand(string text)
    {
        nint h = User32.FindWindow(App.ControllerClass, null);
        if (h == 0) { Console.Error.WriteLine("Claude Pet n'est pas lancé."); return 1; }
        fixed (char* p = text + "\0")
        {
            COPYDATASTRUCT cds = new() { dwData = 1, cbData = (uint)((text.Length + 1) * 2), lpData = (nint)p };
            nuint res;
            User32.SendMessageTimeout(h, C.WM_COPYDATA, 0, (nint)(&cds), 0x0002 /* SMTO_ABORTIFHUNG */, 2000, &res);
        }
        return 0;
    }
}

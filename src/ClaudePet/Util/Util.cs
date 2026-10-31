using System.Diagnostics;
using System.Runtime.CompilerServices;
using ClaudePet.Native;

namespace ClaudePet.Util;

/// <summary>Horloge monotone en secondes.</summary>
internal static class Clock
{
    private static readonly double Inv = 1.0 / Stopwatch.Frequency;
    public static double Now => Stopwatch.GetTimestamp() * Inv;
}

/// <summary>Petit générateur pseudo-aléatoire (xorshift), rapide et sans allocation.</summary>
internal static class Rng
{
    private static ulong s = (ulong)DateTime.UtcNow.Ticks | 1;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static ulong Next()
    {
        s ^= s << 13; s ^= s >> 7; s ^= s << 17;
        return s;
    }

    /// <summary>Nombre dans [0, 1).</summary>
    public static double F() => (Next() >> 11) * (1.0 / (1UL << 53));
    public static double Range(double a, double b) => a + (b - a) * F();
    public static int Int(int a, int bExclusive) => a + (int)(F() * (bExclusive - a));
    public static bool Chance(double p) => F() < p;
    public static int Sign() => F() < 0.5 ? -1 : 1;

    /// <summary>Choix pondéré : renvoie l'index tiré.</summary>
    public static int Pick(ReadOnlySpan<double> weights)
    {
        double total = 0;
        foreach (var w in weights) total += Math.Max(0, w);
        if (total <= 0) return -1;
        double r = F() * total;
        for (int i = 0; i < weights.Length; i++)
        {
            double w = Math.Max(0, weights[i]);
            if (r < w) return i;
            r -= w;
        }
        return weights.Length - 1;
    }
}

internal static class MathX
{
    public static double Clamp(double v, double a, double b) => v < a ? a : v > b ? b : v;
    public static int Clamp(int v, int a, int b) => v < a ? a : v > b ? b : v;
    public static double Lerp(double a, double b, double t) => a + (b - a) * t;
    public static double Approach(double v, double target, double step)
        => v < target ? Math.Min(v + step, target) : Math.Max(v - step, target);
    public static double SmoothStep(double t) { t = Clamp(t, 0, 1); return t * t * (3 - 2 * t); }
    public static double Hypot(double x, double y) => Math.Sqrt(x * x + y * y);
}

/// <summary>Journal de débogage (activé par --log), dans %AppData%\Pet\journal.txt.</summary>
internal static class Log
{
    private static StreamWriter? writer;
    public static bool Enabled => writer != null;

    public static void Open(string path)
    {
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            writer = new StreamWriter(path, append: false) { AutoFlush = true };
            Write("Journal ouvert");
        }
        catch { writer = null; }
    }

    public static void Write(string text)
    {
        if (writer == null) return;
        try { writer.WriteLine($"{DateTime.Now:HH:mm:ss.fff} {text}"); } catch { }
    }

    public static void Close()
    {
        try { writer?.Dispose(); } catch { }
        writer = null;
    }
}

internal static unsafe class SysInfo
{
    /// <summary>Temps allumé depuis le démarrage, sans le temps de veille ni d'hibernation (en secondes).</summary>
    public static double UnbiasedSeconds
    {
        get
        {
            ulong t;
            Kernel32.QueryUnbiasedInterruptTime(&t);
            return t / 10_000_000.0;
        }
    }

    /// <summary>Repère du démarrage de Windows (heure UTC du boot, en secondes, arrondie à la minute).</summary>
    public static long BootStamp
    {
        get
        {
            var boot = DateTime.UtcNow - TimeSpan.FromMilliseconds(Environment.TickCount64);
            return (long)Math.Round(boot.Ticks / (double)TimeSpan.TicksPerMinute);
        }
    }

    /// <summary>Secondes depuis la dernière action clavier ou souris de l'utilisateur.</summary>
    public static double UserIdleSeconds
    {
        get
        {
            LASTINPUTINFO li = new() { cbSize = (uint)sizeof(LASTINPUTINFO) };
            if (User32.GetLastInputInfo(&li) == 0) return 0;
            uint now = (uint)Environment.TickCount;
            return unchecked(now - li.dwTime) / 1000.0;
        }
    }
}

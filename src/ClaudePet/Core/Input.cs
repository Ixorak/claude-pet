using ClaudePet.Native;
using ClaudePet.Util;

namespace ClaudePet.Core;

/// <summary>Suit le pointeur : position, vitesse, secousses.</summary>
internal sealed unsafe class CursorTracker
{
    private struct Sample { public double T, X, Y; }

    private readonly Sample[] buf = new Sample[128];
    private int head = -1, count;
    public double X, Y;
    public double LastMoveTime;

    /// <summary>Outil de test : un pointeur imaginaire qui suit un trajet (la vraie souris n'est pas touchée).</summary>
    public Func<double, (double X, double Y)>? Fake;
    public double FakeUntil;

    public void Update(double now)
    {
        double nx, ny;
        if (Fake != null && now < FakeUntil)
        {
            (nx, ny) = Fake(now);
        }
        else
        {
            Fake = null;
            POINT p;
            if (User32.GetCursorPos(&p) == 0) return;
            nx = p.X; ny = p.Y;
        }
        bool moved = nx != X || ny != Y || count == 0;
        X = nx; Y = ny;
        if (moved) LastMoveTime = now;
        if (count > 0 && now - buf[head].T < 0.004 && !moved) return;
        head = (head + 1) % buf.Length;
        buf[head] = new Sample { T = now, X = X, Y = Y };
        if (count < buf.Length) count++;
    }

    /// <summary>Vitesse moyenne sur la fenêtre de temps donnée (px/s).</summary>
    public (double vx, double vy) Velocity(double now, double window = 0.09)
    {
        if (count < 2) return (0, 0);
        int i = head;
        Sample newest = buf[head], oldest = newest;
        for (int k = 0; k < count; k++)
        {
            var s = buf[i];
            if (now - s.T > window) break;
            oldest = s;
            i = (i - 1 + buf.Length) % buf.Length;
        }
        double dt = newest.T - oldest.T;
        if (dt < 0.012) return (0, 0);
        // si le pointeur est immobile depuis un moment, la vitesse est nulle
        if (now - newest.T > 0.06) return (0, 0);
        return ((newest.X - oldest.X) / dt, (newest.Y - oldest.Y) / dt);
    }

    /// <summary>Nombre d'allers-retours rapides (changements de sens) dans la fenêtre : détecte une secousse.</summary>
    public int Reversals(double now, double window, double minSpeed)
    {
        if (count < 3) return 0;
        int best = 0;
        for (int axis = 0; axis < 2; axis++)
        {
            int rev = 0, lastSign = 0;
            double travel = 0;
            int i = head;
            Sample prev = buf[head];
            for (int k = 1; k < count; k++)
            {
                i = (i - 1 + buf.Length) % buf.Length;
                var s = buf[i];
                if (now - s.T > window) break;
                double dt = prev.T - s.T;
                if (dt <= 0) { prev = s; continue; }
                double d = axis == 0 ? prev.X - s.X : prev.Y - s.Y;
                double v = d / dt;
                travel += Math.Abs(d);
                if (Math.Abs(v) >= minSpeed)
                {
                    int sign = Math.Sign(v);
                    if (lastSign != 0 && sign != lastSign) rev++;
                    lastSign = sign;
                }
                prev = s;
            }
            if (travel > minSpeed * 0.25) best = Math.Max(best, rev);
        }
        return best;
    }

    /// <summary>Distance parcourue par le pointeur depuis un instant donné.</summary>
    public double DistanceSince(double since)
    {
        if (count < 2) return 0;
        double total = 0;
        int i = head;
        Sample prev = buf[head];
        for (int k = 1; k < count; k++)
        {
            i = (i - 1 + buf.Length) % buf.Length;
            var s = buf[i];
            if (s.T < since) break;
            total += MathX.Hypot(prev.X - s.X, prev.Y - s.Y);
            prev = s;
        }
        return total;
    }
}

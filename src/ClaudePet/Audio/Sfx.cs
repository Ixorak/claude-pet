using ClaudePet.Core;
using ClaudePet.Util;

namespace ClaudePet.Audio;

internal enum Wave : byte { Square, Triangle, Sine, Saw }

/// <summary>Synthèse de petits sons 8 bits, doux (filtrés) : rien n'est enregistré, tout est calculé.</summary>
internal static class Synth
{
    private const int R = Mixer.Rate;

    public static float[] Buf(double seconds) => new float[Math.Max(1, (int)(seconds * R))];

    public static void Tone(float[] b, double t0, double dur, double f0, double f1, Wave w, double vol,
        double attack = 0.004, double release = 0.04, double duty = 0.5, double vib = 0, double vibRate = 6, double trem = 0, double tremRate = 0)
    {
        int s0 = (int)(t0 * R), n = (int)(dur * R);
        double phase = 0;
        double ratio = f1 / f0;
        for (int i = 0; i < n; i++)
        {
            int k = s0 + i;
            if ((uint)k >= (uint)b.Length) break;
            double t = i / (double)R, u = i / (double)n;
            double f = f0 * Math.Pow(ratio, u);
            if (vib > 0) f *= 1 + vib * Math.Sin(2 * Math.PI * vibRate * t);
            phase += f / R;
            phase -= Math.Floor(phase);
            double v = w switch
            {
                Wave.Square => phase < duty ? 1 : -1,
                Wave.Triangle => 1 - 4 * Math.Abs(phase - 0.5),
                Wave.Saw => 2 * phase - 1,
                _ => Math.Sin(2 * Math.PI * phase),
            };
            double env = Env(t, dur, attack, release);
            if (trem > 0) env *= 1 - trem * (0.5 + 0.5 * Math.Sin(2 * Math.PI * tremRate * t));
            b[k] += (float)(v * env * vol);
        }
    }

    /// <summary>Bruit « 8 bits » (valeur tenue à une fréquence donnée), filtré.</summary>
    public static void Noise(float[] b, double t0, double dur, double vol, double holdHz, double attack = 0.003, double release = 0.03, double cutoff = 4000)
    {
        int s0 = (int)(t0 * R), n = (int)(dur * R);
        int hold = Math.Max(1, (int)(R / holdHz));
        double v = 0, y = 0;
        double a = 1 - Math.Exp(-2 * Math.PI * cutoff / R);
        for (int i = 0; i < n; i++)
        {
            int k = s0 + i;
            if ((uint)k >= (uint)b.Length) break;
            if (i % hold == 0) v = Rng.F() * 2 - 1;
            y += a * (v - y);
            double t = i / (double)R;
            b[k] += (float)(y * Env(t, dur, attack, release) * vol);
        }
    }

    private static double Env(double t, double dur, double a, double r)
    {
        double e = a > 0 && t < a ? t / a : 1;
        double tail = dur - t;
        if (r > 0 && tail < r) e *= Math.Max(0, tail / r);
        return e;
    }

    /// <summary>Filtre passe-bas d'ordre 1 : adoucit les ondes carrées.</summary>
    public static float[] Soft(float[] b, double cutoff)
    {
        double a = 1 - Math.Exp(-2 * Math.PI * cutoff / R), y = 0;
        for (int i = 0; i < b.Length; i++) { y += a * (b[i] - y); b[i] = (float)y; }
        return b;
    }

    public static double Note(int semitonesFromA4) => 440 * Math.Pow(2, semitonesFromA4 / 12.0);
}

internal static class Sfx
{
    private const double C5 = 523.25, E5 = 659.25, G5 = 783.99, C6 = 1046.5, E6 = 1318.5, G6 = 1568, B6 = 1975.5, D6 = 1174.7;

    public static float[] Make(Snd s, double p)
    {
        float[] b;
        switch (s)
        {
            case Snd.Exit:
                b = Synth.Buf(0.6);
                double[] up = { C5, E5, G5, C6 };
                for (int i = 0; i < up.Length; i++)
                {
                    Synth.Tone(b, i * 0.075, 0.12, up[i] * p, up[i] * p, Wave.Triangle, 0.32, release: 0.07);
                    Synth.Tone(b, i * 0.075, 0.1, up[i] * 2 * p, up[i] * 2 * p, Wave.Square, 0.05, duty: 0.25);
                }
                Synth.Tone(b, 0.33, 0.22, C6 * 2, C6 * 2, Wave.Sine, 0.12, release: 0.18);
                return Synth.Soft(b, 5000);

            case Snd.Enter:
                b = Synth.Buf(0.5);
                double[] down = { G5, E5, C5 };
                for (int i = 0; i < down.Length; i++)
                    Synth.Tone(b, i * 0.08, 0.13, down[i] * p, down[i] * p, Wave.Triangle, 0.3, release: 0.08);
                Synth.Tone(b, 0.27, 0.07, 320, 140, Wave.Sine, 0.35);
                return Synth.Soft(b, 4500);

            case Snd.Step:
                b = Synth.Buf(0.05);
                Synth.Tone(b, 0, 0.035, 170 * p, 110 * p, Wave.Triangle, 0.35, release: 0.025);
                Synth.Noise(b, 0, 0.012, 0.08, 6000, cutoff: 2500);
                return Synth.Soft(b, 2200);

            case Snd.Jump:
                b = Synth.Buf(0.16);
                Synth.Tone(b, 0, 0.14, 280 * p, 640 * p, Wave.Square, 0.2, duty: 0.25, release: 0.05);
                return Synth.Soft(b, 3200);

            case Snd.Bounce:
                b = Synth.Buf(0.16);
                Synth.Tone(b, 0, 0.13, 320 * p, 115 * p, Wave.Triangle, 0.55, release: 0.07);
                Synth.Noise(b, 0, 0.02, 0.12, 4000, cutoff: 2000);
                return Synth.Soft(b, 3000);

            case Snd.Tap:
                b = Synth.Buf(0.09);
                Synth.Tone(b, 0, 0.055, 950 * p, 480 * p, Wave.Square, 0.22, release: 0.03);
                Synth.Tone(b, 0, 0.03, 1500 * p, 1400 * p, Wave.Sine, 0.14);
                Synth.Noise(b, 0, 0.008, 0.12, 8000, cutoff: 5000);
                return Synth.Soft(b, 4200);

            case Snd.Angry:
                b = Synth.Buf(0.62);
                Synth.Tone(b, 0, 0.42, 98 * p, 92 * p, Wave.Square, 0.24, duty: 0.3, trem: 0.7, tremRate: 18, release: 0.06);
                Synth.Tone(b, 0, 0.42, 196 * p, 186 * p, Wave.Triangle, 0.1, trem: 0.7, tremRate: 18);
                Synth.Tone(b, 0.45, 0.13, 170 * p, 270 * p, Wave.Square, 0.2, duty: 0.35, release: 0.05);
                return Synth.Soft(b, 1600);

            case Snd.Stars:
                b = Synth.Buf(0.55);
                double[] tw = { G6, B6, E6 * 1.0, D6 * 1.5 };
                for (int i = 0; i < tw.Length; i++)
                    Synth.Tone(b, i * 0.07, 0.16, tw[i] * p, tw[i] * p, Wave.Triangle, 0.14, release: 0.12);
                return Synth.Soft(b, 7000);

            case Snd.Grab:
                b = Synth.Buf(0.2);
                Synth.Tone(b, 0, 0.07, 500 * p, 900 * p, Wave.Square, 0.18, duty: 0.25);
                Synth.Tone(b, 0.08, 0.08, 760 * p, 1150 * p, Wave.Square, 0.16, duty: 0.25, release: 0.05);
                return Synth.Soft(b, 3500);

            case Snd.Fall:
                b = Synth.Buf(0.6);
                Synth.Tone(b, 0, 0.58, 1300 * p, 340 * p, Wave.Sine, 0.18, attack: 0.02, release: 0.1, vib: 0.02, vibRate: 9);
                return b;

            case Snd.MouthOpen:
                b = Synth.Buf(0.36);
                Synth.Tone(b, 0, 0.34, 300 * p, 430 * p, Wave.Triangle, 0.28, attack: 0.03, release: 0.1, vib: 0.03, vibRate: 7);
                Synth.Tone(b, 0, 0.34, 600 * p, 860 * p, Wave.Square, 0.04, attack: 0.03, release: 0.1);
                return Synth.Soft(b, 3000);

            case Snd.Eat:
                b = Synth.Buf(0.55);
                for (int i = 0; i < 3; i++)
                {
                    double t = i * 0.17;
                    Synth.Noise(b, t, 0.06, 0.32, 2800 * p, cutoff: 2600);
                    Synth.Tone(b, t, 0.035, 190 * p, 120 * p, Wave.Square, 0.12, duty: 0.4);
                }
                return Synth.Soft(b, 3000);

            case Snd.Bubble:
                b = Synth.Buf(0.16);
                Synth.Tone(b, 0, 0.06, 480 * p, 1100 * p, Wave.Sine, 0.26, release: 0.03);
                Synth.Tone(b, 0.065, 0.07, 700 * p, 1500 * p, Wave.Sine, 0.2, release: 0.04);
                return b;

            case Snd.Snore:
                b = Synth.Buf(1.0);
                Synth.Noise(b, 0, 0.62, 0.13, 900, attack: 0.42, release: 0.15, cutoff: 700);
                Synth.Tone(b, 0, 0.62, 90 * p, 80 * p, Wave.Triangle, 0.08, attack: 0.4, release: 0.15);
                Synth.Tone(b, 0.68, 0.26, 820 * p, 700 * p, Wave.Sine, 0.05, attack: 0.05, release: 0.1);
                return b;

            case Snd.Dig:
                b = Synth.Buf(0.1);
                Synth.Noise(b, 0, 0.075, 0.22, 1800 * p, cutoff: 2200);
                Synth.Tone(b, 0, 0.035, 130 * p, 90 * p, Wave.Triangle, 0.2);
                return b;

            case Snd.Emerge:
                b = Synth.Buf(0.25);
                Synth.Tone(b, 0, 0.08, 200 * p, 620 * p, Wave.Sine, 0.3);
                Synth.Tone(b, 0.08, 0.12, 620 * p, 1050 * p, Wave.Triangle, 0.15, release: 0.06);
                return b;

            case Snd.Click:
                b = Synth.Buf(0.025);
                Synth.Tone(b, 0, 0.018, 1250 * p, 1100 * p, Wave.Square, 0.09);
                return Synth.Soft(b, 4000);

            case Snd.Land:
                b = Synth.Buf(0.1);
                Synth.Tone(b, 0, 0.085, 165 * p, 80 * p, Wave.Triangle, 0.32);
                return b;

            case Snd.Yawn:
                b = Synth.Buf(0.6);
                Synth.Tone(b, 0, 0.56, 520 * p, 320 * p, Wave.Sine, 0.15, attack: 0.08, release: 0.15, vib: 0.025, vibRate: 5);
                return b;

            case Snd.Happy:
                b = Synth.Buf(0.22);
                Synth.Tone(b, 0, 0.08, C6 * p, C6 * p, Wave.Triangle, 0.2);
                Synth.Tone(b, 0.085, 0.12, E6 * p, E6 * p, Wave.Triangle, 0.2, release: 0.08);
                return b;

            case Snd.Whoosh:
                b = Synth.Buf(0.28);
                Synth.Noise(b, 0, 0.26, 0.12, 9000, attack: 0.08, release: 0.15, cutoff: 1800);
                return b;

            case Snd.Purr:
            {
                // ronronnement : de petits grondements graves et doux, environ 22 par seconde
                b = Synth.Buf(0.8);
                for (int i = 0; i < 16; i++)
                {
                    double t = i * 0.045;
                    double v = 0.11 * Math.Sin(Math.PI * (i + 0.5) / 16); // monte puis redescend
                    Synth.Noise(b, t, 0.032, v, 700, attack: 0.006, release: 0.02, cutoff: 320);
                    Synth.Tone(b, t, 0.03, 92 * p, 84 * p, Wave.Triangle, v * 0.9, attack: 0.005, release: 0.02);
                }
                return Synth.Soft(b, 900);
            }

            case Snd.Sip:
            {
                // une gorgée de café : un petit « slurp » (souffle qui monte), puis un « glou » grave
                b = Synth.Buf(0.42);
                for (int i = 0; i < 5; i++)
                    Synth.Noise(b, i * 0.035, 0.05, 0.05 + 0.012 * i, 9000, attack: 0.01, release: 0.03, cutoff: 900 + 500 * i);
                Synth.Tone(b, 0.24, 0.12, 330 * p, 190 * p, Wave.Sine, 0.22, attack: 0.01, release: 0.06);
                return Synth.Soft(b, 4000);
            }

            case Snd.Page:
                // une page qu'on tourne : un frottement de papier qui descend
                b = Synth.Buf(0.22);
                Synth.Noise(b, 0, 0.09, 0.09, 14000, attack: 0.02, release: 0.05, cutoff: 5200);
                Synth.Noise(b, 0.07, 0.12, 0.07, 9000, attack: 0.01, release: 0.08, cutoff: 2600);
                return b;

            case Snd.Type:
                // une touche du clavier : un clic très court
                b = Synth.Buf(0.04);
                Synth.Noise(b, 0, 0.018, 0.16, 12000, attack: 0.001, release: 0.012, cutoff: 3500);
                Synth.Tone(b, 0, 0.02, 1700 * p, 1400 * p, Wave.Triangle, 0.05, attack: 0.001, release: 0.012);
                return Synth.Soft(b, 6000);
        }
        return Synth.Buf(0.01);
    }
}

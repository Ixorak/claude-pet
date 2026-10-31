using System.Runtime.InteropServices;
using ClaudePet.Native;
using ClaudePet.Util;

namespace ClaudePet.Audio;

/// <summary>
/// Petit mélangeur audio (waveOut) sur un fil à part. Quand rien ne joue, le fil dort (aucun processeur)
/// et le périphérique est refermé après quelques secondes de silence.
/// </summary>
internal sealed unsafe class Mixer : IDisposable
{
    public const int Rate = 44100;
    private const int Frames = 1024;      // ~23 ms par tampon
    private const int Buffers = 4;

    private struct Voice { public float[] Data; public int Pos; public float Gain; }

    private readonly object lk = new();
    private readonly List<Voice> voices = new();
    private readonly Thread thread;
    private readonly nint wake;
    private nint done;
    private nint device;
    private readonly WAVEHDR*[] headers = new WAVEHDR*[Buffers];
    private readonly short*[] data = new short*[Buffers];
    private readonly float[] mix = new float[Frames];
    private volatile bool stop;
    private float master = 0.5f;

    public Mixer()
    {
        wake = Kernel32.CreateEvent(0, 0, 0, null);
        done = Kernel32.CreateEvent(0, 0, 0, null);
        for (int i = 0; i < Buffers; i++)
        {
            headers[i] = (WAVEHDR*)NativeMemory.AllocZeroed((nuint)sizeof(WAVEHDR));
            data[i] = (short*)NativeMemory.AllocZeroed((nuint)(Frames * 2));
        }
        thread = new Thread(Loop) { IsBackground = true, Name = "Audio", Priority = ThreadPriority.AboveNormal };
        thread.Start();
    }

    /// <summary>Volume général (0 à 1, déjà « courbé » pour l'oreille).</summary>
    public void SetMaster(float v) => master = Math.Clamp(v, 0, 1);

    public void Play(float[] samples, float gain)
    {
        if (samples.Length == 0 || gain <= 0) return;
        lock (lk)
        {
            if (voices.Count > 24) voices.RemoveAt(0);
            voices.Add(new Voice { Data = samples, Gain = gain });
        }
        Kernel32.SetEvent(wake);
    }

    private bool HasVoices { get { lock (lk) return voices.Count > 0; } }

    private void Loop()
    {
        try
        {
            while (!stop)
            {
                // attendre un son (sans timeout : zéro processeur au repos)
                Kernel32.WaitForSingleObject(wake, C.INFINITE);
                if (stop) break;
                if (!HasVoices) continue;
                if (device == 0 && !Open()) { lock (lk) voices.Clear(); continue; }
                double idleSince = -1;
                while (!stop)
                {
                    bool any = false;
                    for (int i = 0; i < Buffers; i++)
                    {
                        var h = headers[i];
                        bool free = h->dwFlags == 0 || (h->dwFlags & C.WHDR_DONE) != 0;
                        if (!free) { any = true; continue; }
                        if (!HasVoices) continue;
                        Fill(i);
                        any = true;
                    }
                    if (!any && !HasVoices)
                    {
                        if (idleSince < 0) idleSince = Clock.Now;
                        // rien à jouer : on attend un nouveau son, et on ferme après 3 s de silence
                        // (une sortie audio ouverte fait travailler Windows en continu, même sans son)
                        if (Kernel32.WaitForSingleObject(wake, 250) == C.WAIT_OBJECT_0)
                        {
                            if (HasVoices) { idleSince = -1; continue; }
                        }
                        if (Clock.Now - idleSince > 3) { Close(); break; }
                        continue;
                    }
                    idleSince = -1;
                    Kernel32.WaitForSingleObject(done, 30);
                }
            }
        }
        catch (Exception e)
        {
            Log.Write("Audio : " + e.Message);
        }
    }

    private bool Open()
    {
        WAVEFORMATEX fmt = new()
        {
            wFormatTag = C.WAVE_FORMAT_PCM, nChannels = 1, nSamplesPerSec = Rate,
            wBitsPerSample = 16, nBlockAlign = 2, nAvgBytesPerSec = Rate * 2,
        };
        nint h;
        uint r = WinMM.waveOutOpen(&h, C.WAVE_MAPPER, &fmt, done, 0, C.CALLBACK_EVENT);
        if (r != 0) { Log.Write($"Audio : waveOutOpen a échoué ({r})"); return false; }
        device = h;
        for (int i = 0; i < Buffers; i++) headers[i]->dwFlags = 0;
        return true;
    }

    private void Close()
    {
        if (device == 0) return;
        WinMM.waveOutReset(device);
        for (int i = 0; i < Buffers; i++)
        {
            if ((headers[i]->dwFlags & 0x2 /* WHDR_PREPARED */) != 0)
                WinMM.waveOutUnprepareHeader(device, headers[i], (uint)sizeof(WAVEHDR));
            headers[i]->dwFlags = 0;
        }
        WinMM.waveOutClose(device);
        device = 0;
    }

    private void Fill(int i)
    {
        Array.Clear(mix);
        float m = master;
        lock (lk)
        {
            for (int v = voices.Count - 1; v >= 0; v--)
            {
                var voice = voices[v];
                int n = Math.Min(Frames, voice.Data.Length - voice.Pos);
                float g = voice.Gain * m;
                for (int k = 0; k < n; k++) mix[k] += voice.Data[voice.Pos + k] * g;
                voice.Pos += n;
                if (voice.Pos >= voice.Data.Length) voices.RemoveAt(v);
                else voices[v] = voice;
            }
        }
        short* d = data[i];
        for (int k = 0; k < Frames; k++)
        {
            float x = mix[k];
            // écrêtage doux
            if (x > 1f || x < -1f) x = MathF.Tanh(x);
            d[k] = (short)(x * 30000);
        }
        var h = headers[i];
        if ((h->dwFlags & 0x2) != 0) WinMM.waveOutUnprepareHeader(device, h, (uint)sizeof(WAVEHDR));
        h->lpData = (nint)d;
        h->dwBufferLength = Frames * 2;
        h->dwFlags = 0;
        h->dwLoops = 0;
        WinMM.waveOutPrepareHeader(device, h, (uint)sizeof(WAVEHDR));
        WinMM.waveOutWrite(device, h, (uint)sizeof(WAVEHDR));
    }

    public void Dispose()
    {
        stop = true;
        Kernel32.SetEvent(wake);
        thread.Join(500);
        Close();
        for (int i = 0; i < Buffers; i++)
        {
            NativeMemory.Free(headers[i]);
            NativeMemory.Free(data[i]);
        }
        Kernel32.CloseHandle(wake);
        Kernel32.CloseHandle(done);
    }
}

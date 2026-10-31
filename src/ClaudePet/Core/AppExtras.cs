using System.Globalization;
using ClaudePet.Util;

namespace ClaudePet.Core;

// Parties de l'application qui grandissent au fil des étapes (bulles, baie, sons...).
internal sealed unsafe partial class App
{
    public const string BubbleClass = "ClaudePet.Bubbles";
    public const string BerryClass = "ClaudePet.Berry";

    private partial void InitExtras(string[] args) { }

    private partial void StartPet(string[] args) => Pet.Spawn();

    private partial double ExtrasInterval() => double.MaxValue;

    private partial void BeforePetUpdate(double dt, double now) { }

    private partial void AfterPetUpdate(double dt, double now) { }

    private partial void RenderExtras(double now) { }

    private partial void BringExtrasToTop() { }

    private partial void SaveState() => Cfg.Save();

    private partial bool HandleExtras(nint hwnd, uint msg, nuint wp, nint lp, ref nint result) => false;

    private partial void OnPetMouseDown(double now) => Pet.OnMouseDown(Cursor.X, Cursor.Y, now);

    private partial void PlaySound(Snd s, double volume) { }

    private partial void OnMuteChanged() { }

    private partial void ShutdownExtras() { }

    /// <summary>Commandes de test envoyées par « ClaudePet.exe --cmd ... » (outil de développement).</summary>
    private partial void DebugCommand(string text)
    {
        Log.Write("Commande : " + text);
        var a = text.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (a.Length == 0) return;
        double D(int i, double def = 0) => a.Length > i && double.TryParse(a[i], NumberStyles.Float, CultureInfo.InvariantCulture, out var v) ? v : def;
        switch (a[0])
        {
            case "throw":
                Pet.Debug_Throw(D(1), D(2));
                break;
            case "goto":
                Pet.Debug_Goto(D(1), D(2));
                break;
            case "state":
                Log.Write(Pet.Describe());
                break;
            case "sit":
                Pet.Enter(St.Sit, D(1, 30));
                break;
            case "quit":
                Quit();
                break;
            default:
                DebugCommandMore(a);
                break;
        }
        Wake();
    }

    private void DebugCommandMore(string[] a) { }
}

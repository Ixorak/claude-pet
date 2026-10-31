using System.Globalization;
using ClaudePet.Util;

namespace ClaudePet.Core;

internal sealed partial class Pet
{
    /// <summary>Les écrans ont changé (branchement, résolution, barre des tâches...).</summary>
    public void OnScreensChanged()
    {
        Scr = Desk.At(X, Y - HH * 0.5) ?? Desk.Nearest(X, Y - HH * 0.5);
        Scale = TargetScale(Scr);
        if (State == St.Dragged) return;
        if (!FreeAt(X, Y))
        {
            Resolve();
            if (!Supported()) StartFall(0, 0);
        }
    }

    public void OnCalmChanged()
    {
        if (State == St.Walk && !running) speed = app.Cfg.Calm ? BodyW * 0.25 : BodyW * 0.55;
    }

    /// <summary>Rentrer à la maison (étape 1 : on quitte simplement).</summary>
    public void GoHome() => app.Quit();

    public void Debug_Throw(double vx, double vy)
    {
        if (State == St.Hidden) return;
        StartFall(vx * K, vy * K, AirKind.Thrown);
    }

    public void Debug_Goto(double x, double y)
    {
        X = x; Y = y;
        StartFall(0, 0);
    }

    public string Describe() => string.Create(CultureInfo.InvariantCulture,
        $"état={State} t={T:F2} X={X:F1} Y={Y:F1} VX={VX:F0} VY={VY:F0} échelle={Scale:F2} écran={Scr} face={Facing}");
}

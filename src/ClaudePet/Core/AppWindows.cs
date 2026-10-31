using ClaudePet.Util;

namespace ClaudePet.Core;

// Relevé des fenêtres (étape 5) : plates-formes et écrans en plein écran.
internal sealed unsafe partial class App
{
    public readonly WindowScanner Windows = new();
    private double nextWinScan;

    /// <summary>Relit les fenêtres tout de suite (avant de choisir un saut, par exemple).</summary>
    public void ScanWindowsNow()
    {
        nextWinScan = Clock.Now + WinScanInterval();
        if (Windows.Scan(Desk)) Pet.OnFullscreenChanged();
    }

    /// <summary>Plus souvent quand Pet bouge (pour se poser sur des fenêtres à jour), rarement quand il dort.</summary>
    private double WinScanInterval() => Pet.State switch
    {
        St.Air or St.Walk or St.JumpPrep or St.Climb or St.Dragged or St.Land => 0.4,
        St.Sleep => 2.0,
        _ => 1.0,
    };

    private void MaybeScanWindows(double now)
    {
        if (now >= nextWinScan) ScanWindowsNow();
    }
}

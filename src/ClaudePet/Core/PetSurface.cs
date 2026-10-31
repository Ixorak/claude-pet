namespace ClaudePet.Core;

// Surfaces sur lesquelles Pet se tient. Étape 1 : seulement le sol des écrans.
// L'étape 5 ajoute le haut des fenêtres.
internal sealed partial class Pet
{
    private partial bool PlatformCross(double fromY, double toY, out double top)
    {
        top = 0;
        return false;
    }

    private partial bool OnPlatform() => false;

    private partial bool PlatformSupportsAt(double x) => false;

    private partial void LeaveSurface() { }

    private partial void AttachPlatformFromLanding() { }

    private partial void AttachFloor() { }

    private partial void OnLanded() { }

    private partial bool CanBeGrabbed() => (Physical || State == St.Dragged) && !homeBound;

    private partial void OnClick(double cx, double cy, double now)
    {
        // étape 3 : taper / double-clic
    }

    private partial void OnReleased() { }

    private partial void OnGoHomeStarted() { }

    private partial void OnUnderground() { }

    private partial void OnHiddenAll() { }

    private partial ClaudePet.Art.PetPose ExtraPose(ClaudePet.Art.PetPose p, ref double clipY) => p;

    /// <summary>Surcouches supplémentaires dans la fenêtre d'effets (bulles...).</summary>
    public void ExtraOverlays(Fx fx, int k) { }

    public unsafe void ExtraDraw(uint* bits, int stride, int dw, int dh, int left, int top) { }
}

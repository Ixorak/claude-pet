namespace ClaudePet.Art;

// Pet : la mascotte de Claude, un bloc orange avec deux yeux fendus, deux petits bras et quatre pattes.
// Tout est dessiné par le code, en pixels « art » (agrandis ensuite au plus proche voisin).
// Repère : l'origine (0,0) est au sol, au milieu des pieds ; y négatif vers le haut.

internal static class Pal
{
    public static readonly uint Outline = Col.Rgb(0x4A, 0x22, 0x14);
    public static readonly uint Body = Col.Rgb(0xD9, 0x77, 0x57);
    public static readonly uint Hi = Col.Rgb(0xEC, 0x9C, 0x7E);
    public static readonly uint Shade = Col.Rgb(0xB9, 0x5C, 0x3F);
    public static readonly uint Leg = Col.Rgb(0xC4, 0x66, 0x48);
    public static readonly uint Eye = Col.Rgb(0x1F, 0x12, 0x0D);
    public static readonly uint Shine = Col.Rgb(0xFF, 0xF4, 0xEC);
    public static readonly uint MouthIn = Col.Rgb(0x5C, 0x1A, 0x17);
    public static readonly uint Tongue = Col.Rgb(0xEA, 0x73, 0x7C);
    public static readonly uint Blush = Col.Rgb(0xEF, 0x5F, 0x58);

    public static readonly uint AngryBody = Col.Rgb(0xE2, 0x3E, 0x2E);
    public static readonly uint AngryHi = Col.Rgb(0xF2, 0x6B, 0x58);
    public static readonly uint AngryShade = Col.Rgb(0xA8, 0x2A, 0x20);
    public static readonly uint AngryLeg = Col.Rgb(0xC2, 0x33, 0x26);
}

internal enum Eyes : byte { Open, Closed, Happy, Wide, Dizzy, Angry, Squeeze, Half, Swirl }
internal enum Mouth : byte { None, Smile, SmallO, Open, Wide, Frown, Yawn, Chew, Grin }
internal enum Legs : byte { Stand, Sit, Dangle, Tuck, None, Spread, Kneel }
internal enum ArmK : byte { Side, SideUp, Raised, Up, Down, Hold, Out, Reach, Dig }

internal readonly record struct Arm(ArmK Kind, sbyte DX = 0, sbyte DY = 0)
{
    public static readonly Arm Side = new(ArmK.Side);
    public static readonly Arm SideUp = new(ArmK.SideUp);
    public static readonly Arm Raised = new(ArmK.Raised);
    public static readonly Arm Up = new(ArmK.Up);
    public static readonly Arm Down = new(ArmK.Down);
    public static readonly Arm Hold = new(ArmK.Hold);
    public static readonly Arm Out = new(ArmK.Out);
    public static readonly Arm Reach = new(ArmK.Reach);
    public Arm Shift(int dx, int dy) => this with { DX = (sbyte)(DX + dx), DY = (sbyte)(DY + dy) };
}

internal record struct PetPose
{
    public int BodyW, BodyH, BodyBottom, DX;
    public Legs Legs;
    public int LiftA, LiftB, Kick;
    public Arm L, R;
    public Eyes Eyes;
    public int LookX, LookY;
    public Mouth Mouth;
    public bool Blush, Brows;
    public float Red;

    public static PetPose Stand => new()
    {
        BodyW = 24, BodyH = 16, BodyBottom = -6, Legs = Legs.Stand,
        L = Arm.Side, R = Arm.Side, Eyes = Eyes.Open, Mouth = Mouth.None,
    };

    public static PetPose Sitting => Stand with { BodyBottom = -3, Legs = Legs.Sit, L = Arm.Down, R = Arm.Down };

    public PetPose Arms(Arm both) => this with { L = both, R = both };
}

internal static class PetPainter
{
    // Dimensions utiles au moteur (en pixels art) : demi-largeur du corps et hauteur totale debout.
    public const int HalfWidth = 13;
    public const int Height = 22;
    public const int MouthY = -12;   // hauteur de la bouche, debout
    public const int HeadTop = -22;  // haut de la tête (liseré compris), debout

    public static ArtCanvas NewCanvas() => new(48, 48, 24, 46);

    public static void Draw(ArtCanvas c, in PetPose p)
    {
        c.Clear();
        int bw = Math.Max(8, p.BodyW), bh = Math.Max(6, p.BodyH);
        int left = -(bw / 2) + p.DX, right = left + bw - 1;
        int bottom = p.BodyBottom, top = bottom - bh + 1;
        int mir = left + right;

        double red = Math.Clamp(p.Red, 0, 1);
        uint body = Col.Lerp(Pal.Body, Pal.AngryBody, red);
        uint hi = Col.Lerp(Pal.Hi, Pal.AngryHi, red);
        uint shade = Col.Lerp(Pal.Shade, Pal.AngryShade, red);
        uint leg = Col.Lerp(Pal.Leg, Pal.AngryLeg, red);

        DrawLegs(c, p, left, right, bottom, leg);
        DrawArm(c, p.L, left, right, top, false, body, 0);
        DrawArm(c, p.R, left, right, top, true, body, 0);

        // Corps
        c.Rect(left, top, right, bottom, body);
        for (int x = left + 2; x <= right - 2; x++) c.Set(x, top, hi);
        for (int x = left + 1; x <= left + 2; x++) c.Set(x, top + 1, hi);
        for (int x = left + 1; x <= right - 1; x++) c.Set(x, bottom, shade);
        for (int y = top + 2; y < bottom; y++) c.Set(right, y, shade);
        // coins arrondis
        foreach (var (cx, cy, sx, sy) in new[] { (left, top, 1, 1), (right, top, -1, 1), (left, bottom, 1, -1), (right, bottom, -1, -1) })
        {
            c.Set(cx, cy, 0);
            c.Set(cx + sx, cy, 0);
            c.Set(cx, cy + sy, 0);
        }

        c.Outline(Pal.Outline);

        DrawFace(c, p, left, right, top, body);
    }

    private static void DrawLegs(ArtCanvas c, in PetPose p, int left, int right, int bottom, uint col)
    {
        if (p.Legs == Legs.None) return;
        int mir = left + right;
        // pattes gauches (extérieure, intérieure) ; les droites en miroir
        int[] lx = { left + 2, left + 6 };
        for (int i = 0; i < 4; i++)
        {
            bool rightSide = i >= 2;
            int baseX = lx[i % 2];
            bool outer = i % 2 == 0;
            // paire A : extérieure gauche + intérieure droite ; paire B : intérieure gauche + extérieure droite
            bool pairA = (i == 0) || (i == 3);
            int lift = pairA ? p.LiftA : p.LiftB;
            int y0 = bottom + 1, y1;
            int bend = 0;
            switch (p.Legs)
            {
                case Legs.Sit: y0 = Math.Max(y0, -2); y1 = -2; break;
                case Legs.Kneel: y1 = Math.Min(-2, bottom + 2); break;
                case Legs.Tuck: y1 = bottom + 2; break;
                case Legs.Dangle:
                    y1 = bottom + 5 - ((p.Kick != 0) == pairA ? 1 : 0);
                    if (outer) bend = -1;
                    break;
                case Legs.Spread:
                    y1 = Math.Max(bottom + 1, -2);
                    if (outer) bend = -2;
                    break;
                default: y1 = -2 - lift; break;
            }
            if (y1 < y0) y1 = y0;
            for (int y = y0; y <= y1; y++)
            {
                int off = (bend != 0 && y > y0 + (y1 - y0) / 2) ? bend : 0;
                if (p.Legs == Legs.Spread && outer) off = bend;
                for (int k = 0; k < 2; k++)
                {
                    int x = baseX + k + off;
                    c.Set(rightSide ? mir - x : x, y, col);
                }
            }
        }
    }

    /// <summary>Point d'accroche de la main « Hold » (bras levé qui tient le pointeur), relatif à l'origine.</summary>
    public static (int x, int y) HoldHand(in PetPose p)
    {
        int bw = Math.Max(8, p.BodyW);
        int left = -(bw / 2) + p.DX;
        int top = p.BodyBottom - Math.Max(6, p.BodyH) + 1;
        return (left + 2, top - 8);
    }

    private static void DrawArm(ArtCanvas c, Arm a, int left, int right, int top, bool mirror, uint col, int grow)
    {
        int sx = left, sy = top + 8;
        int rx = sx + 1, ry = sy + 1, hx, hy, size = 3;
        switch (a.Kind)
        {
            case ArmK.Side: hx = sx - 3; hy = sy + 1; size = 4; break;
            case ArmK.SideUp: hx = sx - 4; hy = sy - 2; break;
            case ArmK.Raised: hx = sx - 4; hy = sy - 5; break;
            case ArmK.Up: hx = sx - 2; hy = sy - 11; break;
            case ArmK.Reach: hx = sx - 3; hy = sy - 9; break;
            case ArmK.Down: hx = sx - 2; hy = sy + 3; break;
            case ArmK.Out: hx = sx - 5; hy = sy; break;
            case ArmK.Hold: rx = sx + 2; ry = top + 3; hx = sx + 2; hy = top - 7; break;
            case ArmK.Dig: hx = sx - 3; hy = sy + 7; break;
            default: hx = sx - 3; hy = sy + 1; break;
        }
        hx += a.DX; hy += a.DY;
        if (a.Kind == ArmK.Side) { ry += a.DY; }
        int mir = left + right;
        Stroke(c, rx, ry, hx, hy, size + grow, col, mirror ? mir : int.MinValue);
    }

    private static void Stroke(ArtCanvas c, int x0, int y0, int x1, int y1, int size, uint col, int mir)
    {
        int dx = Math.Abs(x1 - x0), sxs = x0 < x1 ? 1 : -1;
        int dy = -Math.Abs(y1 - y0), sys = y0 < y1 ? 1 : -1;
        int err = dx + dy;
        int lo = -((size - 1) / 2), hi = lo + size - 1;
        while (true)
        {
            for (int yy = lo; yy <= hi; yy++)
                for (int xx = lo; xx <= hi; xx++)
                {
                    int x = x0 + xx;
                    c.Set(mir == int.MinValue ? x : mir - x, y0 + yy, col);
                }
            if (x0 == x1 && y0 == y1) break;
            int e2 = 2 * err;
            if (e2 >= dy) { err += dy; x0 += sxs; }
            if (e2 <= dx) { err += dx; y0 += sys; }
        }
    }

    private static void DrawFace(ArtCanvas c, in PetPose p, int left, int right, int top, uint body)
    {
        int lx = Math.Clamp(p.LookX, -1, 1), ly = Math.Clamp(p.LookY, -1, 1);
        int x0 = left + 4 + lx, x1 = right - 5 + lx, y0 = top + 4 + ly;
        uint e = Pal.Eye;

        void Both(int dx, int dy) { c.Set(x0 + dx, y0 + dy, e); c.Set(x1 + 1 - dx, y0 + dy, e); }

        switch (p.Eyes)
        {
            case Eyes.Open:
                for (int y = 0; y < 4; y++) { Both(0, y); Both(1, y); }
                break;
            case Eyes.Wide:
                for (int y = 0; y < 4; y++) { Both(0, y); Both(1, y); }
                c.Set(x0 + 1, y0, Pal.Shine); c.Set(x1 + 1, y0, Pal.Shine);
                break;
            case Eyes.Half:
                for (int y = 2; y < 4; y++) { Both(0, y); Both(1, y); }
                break;
            case Eyes.Closed:
                Both(0, 2); Both(1, 2);
                break;
            case Eyes.Happy:
                Both(-1, 2); Both(0, 1); Both(1, 1); Both(2, 2);
                break;
            case Eyes.Squeeze:
                // > <
                c.Set(x0, y0, e); c.Set(x0 + 1, y0 + 1, e); c.Set(x0, y0 + 2, e);
                c.Set(x1 + 1, y0, e); c.Set(x1, y0 + 1, e); c.Set(x1 + 1, y0 + 2, e);
                break;
            case Eyes.Dizzy:
                foreach (int cx in new[] { x0, x1 + 1 })
                {
                    c.Set(cx - 1, y0 + 1, e); c.Set(cx + 1, y0 + 1, e); c.Set(cx, y0 + 2, e);
                    c.Set(cx - 1, y0 + 3, e); c.Set(cx + 1, y0 + 3, e);
                }
                break;
            case Eyes.Swirl:
                foreach (int cx in new[] { x0, x1 + 1 })
                {
                    c.Set(cx - 1, y0 + 1, e); c.Set(cx, y0 + 1, e); c.Set(cx + 1, y0 + 1, e);
                    c.Set(cx + 1, y0 + 2, e); c.Set(cx - 1, y0 + 2, e);
                    c.Set(cx - 1, y0 + 3, e); c.Set(cx, y0 + 3, e);
                }
                break;
            case Eyes.Angry:
                for (int y = 1; y < 4; y++) { Both(0, y); Both(1, y); }
                break;
        }

        if (p.Brows || p.Eyes == Eyes.Angry)
        {
            c.Set(x0 - 2, y0 - 1, e); c.Set(x0 - 1, y0 - 1, e); c.Set(x0, y0, e); c.Set(x0 + 1, y0, e);
            c.Set(x1 + 3, y0 - 1, e); c.Set(x1 + 2, y0 - 1, e); c.Set(x1 + 1, y0, e); c.Set(x1, y0, e);
        }

        int ml = (left + right - 1) / 2 + lx; // colonne gauche de l'axe
        int my = top + 9 + ly;
        uint m = Pal.MouthIn;
        switch (p.Mouth)
        {
            case Mouth.Smile:
                c.Set(ml - 1, my, e); c.Set(ml, my + 1, e); c.Set(ml + 1, my + 1, e); c.Set(ml + 2, my, e);
                break;
            case Mouth.Grin:
                for (int x = ml - 1; x <= ml + 2; x++) c.Set(x, my + 1, m);
                c.Set(ml - 2, my, e); c.Set(ml + 3, my, e);
                c.Set(ml, my + 2, Pal.Tongue); c.Set(ml + 1, my + 2, Pal.Tongue);
                for (int x = ml - 1; x <= ml + 2; x++) c.SetIfOpaque(x, my + 2, (x == ml || x == ml + 1) ? Pal.Tongue : e);
                break;
            case Mouth.Frown:
                c.Set(ml - 1, my + 1, e); c.Set(ml, my, e); c.Set(ml + 1, my, e); c.Set(ml + 2, my + 1, e);
                break;
            case Mouth.SmallO:
                c.Rect(ml, my, ml + 1, my + 1, m);
                break;
            case Mouth.Chew:
                for (int x = ml - 1; x <= ml + 2; x++) c.Set(x, my + 1, e);
                c.Set(ml - 1, my, e); c.Set(ml + 2, my, e);
                break;
            case Mouth.Open:
                c.Rect(ml - 1, my, ml + 2, my + 2, m);
                c.Set(ml, my + 2, Pal.Tongue); c.Set(ml + 1, my + 2, Pal.Tongue);
                break;
            case Mouth.Yawn:
                c.Rect(ml - 1, my - 1, ml + 2, my + 2, m);
                c.Set(ml - 1, my - 1, body); c.Set(ml + 2, my - 1, body);
                c.Set(ml, my + 2, Pal.Tongue); c.Set(ml + 1, my + 2, Pal.Tongue);
                break;
            case Mouth.Wide:
                c.Rect(ml - 2, my - 1, ml + 3, my + 3, m);
                c.Set(ml - 2, my - 1, body); c.Set(ml + 3, my - 1, body);
                c.Set(ml - 2, my + 3, body); c.Set(ml + 3, my + 3, body);
                c.Rect(ml - 1, my + 2, ml + 2, my + 3, Pal.Tongue);
                c.Set(ml - 1, my + 3, m); c.Set(ml + 2, my + 3, m);
                c.Set(ml - 1, my - 1, Pal.Shine); c.Set(ml + 2, my - 1, Pal.Shine);
                break;
        }

        if (p.Blush)
        {
            c.SetIfOpaque(left + 1, my + 1, Pal.Blush); c.SetIfOpaque(left + 2, my + 1, Pal.Blush);
            c.SetIfOpaque(right - 1, my + 1, Pal.Blush); c.SetIfOpaque(right - 2, my + 1, Pal.Blush);
        }
    }
}

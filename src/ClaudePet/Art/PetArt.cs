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
internal enum Mouth : byte { None, Smile, SmallO, Open, Wide, Frown, Yawn, Chew, Grin, Flat }
/// <summary>
/// Objet qu'il tient : une tasse de café, son ordinateur portable (posé devant lui), un casque audio,
/// un livre ouvert, une baguette à bulles.
/// </summary>
internal enum Prop : byte { None, Mug, Laptop, Headphones, Book, Wand }
/// <summary>Jambes. Swing : elles pendent et se balancent (Kick = la phase, de 0 à 3), comme un enfant assis.</summary>
internal enum Legs : byte { Stand, Sit, Dangle, Tuck, None, Spread, Kneel, Swing }
/// <summary>
/// Formes de bras. Aim : bras tendu dans une direction, la main à (DX, DY) de l'épaule ;
/// (RX, RY) déplace l'épaule (x positif : vers le corps).
/// </summary>
internal enum ArmK : byte { Side, SideUp, Raised, Up, Down, Hold, Out, Reach, Dig, Aim, Hidden }

internal readonly record struct Arm(ArmK Kind, sbyte DX = 0, sbyte DY = 0, sbyte RX = 0, sbyte RY = 0)
{
    public static readonly Arm Side = new(ArmK.Side);
    public static readonly Arm SideUp = new(ArmK.SideUp);
    public static readonly Arm Raised = new(ArmK.Raised);
    public static readonly Arm Up = new(ArmK.Up);
    public static readonly Arm Down = new(ArmK.Down);
    public static readonly Arm Hold = new(ArmK.Hold);
    public static readonly Arm Out = new(ArmK.Out);
    public static readonly Arm Reach = new(ArmK.Reach);
    /// <summary>Pas dessiné (ses mains sont dessinées avec l'objet qu'il tient).</summary>
    public static readonly Arm Hidden = new(ArmK.Hidden);
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
    /// <summary>
    /// Objet tenu. Tasse : <see cref="PropF"/> = hauteur (0 sur les genoux, 1 à mi-chemin, 2 à la bouche).
    /// Ordinateur : <see cref="PropF"/> = écran (0 fermé, 1 à moitié, 2 ouvert), <see cref="PropG"/> = mains qui tapent (0 à 3).
    /// </summary>
    public Prop Prop;
    public int PropF, PropG;

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
        // les bras qui visent le pointeur passent devant le corps (dessinés à la fin, avec leur contour)
        bool frontL = p.L.Kind == ArmK.Aim, frontR = p.R.Kind == ArmK.Aim;
        if (!frontL) DrawArm(c, p.L, left, right, top, false, body, 0);
        if (!frontR) DrawArm(c, p.R, left, right, top, true, body, 0);

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
        if (frontL) DrawArmFront(c, p.L, left, right, top, false, body);
        if (frontR) DrawArmFront(c, p.R, left, right, top, true, body);
        switch (p.Prop)
        {
            case Prop.Mug: DrawMug(c, p, left, right, top, body); break;
            case Prop.Laptop: DrawLaptop(c, p, left, right, top, body); break;
            case Prop.Headphones: DrawHeadphones(c, left, right, top); break;
            case Prop.Book: DrawBook(c, p, left, right, body); break;
            case Prop.Wand: DrawWand(c, p, left, right, top, body); break;
        }
    }

    private static readonly uint PhoneBand = Col.Rgb(0x8A, 0x92, 0xA0), PhoneCup = Col.Rgb(0x48, 0x4F, 0x5D),
        PhoneDot = Col.Rgb(0xF0, 0x9A, 0x70);

    /// <summary>Un casque audio : l'arceau par-dessus la tête, un écouteur de chaque côté.</summary>
    private static void DrawHeadphones(ArtCanvas c, int left, int right, int top)
    {
        (int x, int y)[] arc =
        {
            (left - 2, top + 3), (left - 2, top), (left, top - 2), (right, top - 2), (right + 2, top), (right + 2, top + 3),
        };
        for (int i = 0; i + 1 < arc.Length; i++) Stroke(c, arc[i].x, arc[i].y, arc[i + 1].x, arc[i + 1].y, 3, Slate, int.MinValue);
        for (int i = 0; i + 1 < arc.Length; i++) Stroke(c, arc[i].x, arc[i].y, arc[i + 1].x, arc[i + 1].y, 1, PhoneBand, int.MinValue);
        int mir = left + right;
        for (int side = 0; side < 2; side++)
        {
            void Put(int x, int y, uint col) => c.Set(side == 0 ? x : mir - x, y, col);
            int x0 = left - 4, x1 = left, y0 = top + 3, y1 = top + 9;
            for (int x = x0 + 1; x < x1; x++) { Put(x, y0, Slate); Put(x, y1, Slate); }
            for (int y = y0 + 1; y < y1; y++) { Put(x0, y, Slate); Put(x1, y, Slate); }
            for (int y = y0 + 1; y < y1; y++)
                for (int x = x0 + 1; x < x1; x++) Put(x, y, PhoneCup);
            for (int y = y0 + 2; y <= y0 + 4; y++) Put(x0 + 2, y, PhoneDot);
        }
    }

    private static readonly uint Page = Col.Rgb(0xF7, 0xF3, 0xEA), Ink = Col.Rgb(0xA9, 0xA3, 0x9A),
        BookCover = Col.Rgb(0x3E, 0x7C, 0xB1);

    // un livre ouvert, posé devant lui : deux pages, la reliure au milieu, la couverture en bas
    private static readonly string[] BookArt =
    {
        ".KKKKK..KKKKK.",
        "KWWWWWKKWWWWWK",
        "KWsssWKKWsssWK",
        "KWWWWWKKWWWWWK",
        "KWssWWKKWssWWK",
        "KWWWWWKKWWWWWK",
        "KccccccccccccK",
        ".KKKKKKKKKKKK.",
    };

    /// <summary>Le livre ouvert sur ses genoux ; <see cref="PetPose.PropF"/> = 1 : une page en train de tourner.</summary>
    private static void DrawBook(ArtCanvas c, in PetPose p, int left, int right, uint body)
    {
        int bx = -7, by = p.BodyBottom - 3;
        c.Sprite(bx, by, BookArt, ch => ch switch
        {
            'K' => Pal.Outline, 'W' => Page, 's' => Ink, 'c' => BookCover, _ => 0u,
        });
        if (p.PropF == 1)
        {
            // la page tourne : elle se dresse au-dessus de la reliure
            for (int y = by - 4; y <= by; y++) { c.Set(-2, y, Pal.Outline); c.Set(1, y, Pal.Outline); }
            c.Set(-1, by - 5, Pal.Outline); c.Set(0, by - 5, Pal.Outline);
            for (int y = by - 4; y <= by; y++) { c.Set(-1, y, Page); c.Set(0, y, Page); }
        }
        int mir = left + right;
        Paw(c, bx - 2, by + 3, body);
        Paw(c, mir - (bx - 2) - 1, by + 3, body);
    }

    private static readonly uint Soap = Col.Rgb(0x8F, 0xE3, 0xFF), SoapFilm = Col.Rgb(0xD8, 0xF6, 0xFF);

    /// <summary>
    /// Une baguette à bulles devant sa bouche (l'anneau autour de la bouche, le manche jusqu'à sa main) ;
    /// <see cref="PetPose.PropF"/> = 1 : un film de savon dans l'anneau, prêt à souffler.
    /// </summary>
    private static void DrawWand(ArtCanvas c, in PetPose p, int left, int right, int top, uint body)
    {
        int lx = Math.Clamp(p.LookX, -1, 1), ly = Math.Clamp(p.LookY, -1, 1);
        int ml = (left + right - 1) / 2 + lx, my = top + 9 + ly;     // la bouche, comme dans DrawFace
        if (p.PropF == 1)
            for (int y = my - 1; y <= my + 2; y++)
                for (int x = ml - 1; x <= ml + 2; x++) c.Set(x, y, SoapFilm);
        for (int x = ml - 1; x <= ml + 2; x++) { c.Set(x, my - 2, Soap); c.Set(x, my + 3, Soap); }
        for (int y = my - 1; y <= my + 2; y++) { c.Set(ml - 2, y, Soap); c.Set(ml + 3, y, Soap); }
        if (p.PropF == 1) c.Set(ml + 1, my - 1, Pal.Shine);
        Stroke(c, ml + 3, my + 3, ml + 6, my + 6, 1, Pal.Outline, int.MinValue); // le manche
        Paw(c, ml + 6, my + 6, body);
    }

    /// <summary>L'anneau de la baguette (d'où partent les bulles), dans le dessin.</summary>
    public static (int x, int y) WandRing(in PetPose p)
    {
        int bw = Math.Max(8, p.BodyW), bh = Math.Max(6, p.BodyH);
        int left = -(bw / 2) + p.DX, right = left + bw - 1, top = p.BodyBottom - bh + 1;
        int lx = Math.Clamp(p.LookX, -1, 1), ly = Math.Clamp(p.LookY, -1, 1);
        return ((left + right - 1) / 2 + lx, top + 9 + ly);
    }

    // ------------------------------------------------------------------ objets

    private static readonly uint MugWhite = Col.Rgb(0xF4, 0xEE, 0xE6), MugShine = Col.Rgb(0xFF, 0xFF, 0xFF),
        Coffee = Col.Rgb(0x6E, 0x40, 0x27);

    // la tasse (anse à droite) : liseré, café, porcelaine, reflet
    private static readonly string[] MugArt =
    {
        "KKKKKK..",
        "KccccK..",
        "KWwwwKKK",
        "KwwwwK.K",
        "KwwwwK.K",
        "KwwwwKKK",
        ".KKKK...",
    };

    /// <summary>Coin haut gauche de la tasse dans le dessin (origine aux pieds).</summary>
    private static (int x, int y) MugAt(in PetPose p, int left, int right, int top)
    {
        int lx = Math.Clamp(p.LookX, -1, 1), ly = Math.Clamp(p.LookY, -1, 1);
        int ml = (left + right - 1) / 2 + lx, my = top + 9 + ly;     // la bouche, comme dans DrawFace
        int dy = p.PropF switch { >= 2 => -3, 1 => -1, _ => 1 };
        return (ml - 2, my + dy);
    }

    /// <summary>Le dessus de la tasse (d'où monte la vapeur), dans le dessin.</summary>
    public static (int x, int y) MugTop(in PetPose p)
    {
        int bw = Math.Max(8, p.BodyW), bh = Math.Max(6, p.BodyH);
        int left = -(bw / 2) + p.DX, right = left + bw - 1, top = p.BodyBottom - bh + 1;
        var (x, y) = MugAt(p, left, right, top);
        return (x + 3, y);
    }

    private static void DrawMug(ArtCanvas c, in PetPose p, int left, int right, int top, uint body)
    {
        var (x, y) = MugAt(p, left, right, top);
        c.Sprite(x, y, MugArt, ch => ch switch
        {
            'K' => Pal.Outline, 'c' => Coffee, 'w' => MugWhite, 'W' => MugShine, _ => 0u,
        });
        // ses deux petites mains autour de la tasse (la droite tient l'anse)
        Paw(c, x - 2, y + 3, body);
        Paw(c, x + 6, y + 3, body);
    }

    /// <summary>Une petite main de 2 × 2 avec son liseré (coins arrondis).</summary>
    private static void Paw(ArtCanvas c, int x, int y, uint body)
    {
        for (int i = 0; i < 2; i++)
        {
            c.Set(x + i, y - 1, Pal.Outline); c.Set(x + i, y + 2, Pal.Outline);
            c.Set(x - 1, y + i, Pal.Outline); c.Set(x + 2, y + i, Pal.Outline);
        }
        c.Rect(x, y, x + 1, y + 1, body);
    }

    private static readonly uint LidSilver = Col.Rgb(0xC9, 0xCE, 0xD6), LidShade = Col.Rgb(0xA7, 0xAE, 0xB9),
        Keys = Col.Rgb(0x6B, 0x73, 0x80), Slate = Col.Rgb(0x2F, 0x34, 0x40);

    // le logo au dos de l'écran : une petite étoile orange
    private static readonly string[] SparkArt =
    {
        "..o..",
        "o.o.o",
        ".ooo.",
        "o.o.o",
        "..o..",
    };

    /// <summary>
    /// L'ordinateur posé devant lui, vu de dos (écran tourné vers Pet, logo vers nous) ; ses bras passent derrière
    /// l'écran, sur le clavier.
    /// </summary>
    private static void DrawLaptop(ArtCanvas c, in PetPose p, int left, int right, int top, uint body)
    {
        int open = Math.Clamp(p.PropF, 0, 2);
        int lidTop = open switch { 2 => -8, 1 => -5, _ => -3 };     // rangée du liseré du haut de l'écran
        // bras : du flanc vers le clavier (le bout disparaît derrière l'écran) ; ils tapent chacun leur tour
        if (open > 0)
        {
            int g = p.PropG & 3;
            int ly = -5 - (g == 1 || g == 3 ? 1 : 0), ry = -5 - (g == 2 || g == 3 ? 1 : 0);
            FrontArm(c, left - 1, top + 9, -9, ly, body, int.MinValue);
            FrontArm(c, left - 1, top + 9, -9, ry, body, left + right);   // le bras droit, en miroir
        }
        // socle (clavier vu de face) et son liseré
        c.Rect(-12, -2, 11, 0, Slate);
        c.Rect(-11, -1, 10, -1, Keys);
        // écran
        if (open == 0) return;
        c.Rect(-10, lidTop, 9, -2, Slate);
        c.Rect(-9, lidTop + 1, 8, -3, LidSilver);
        for (int x = -9; x <= 8; x++) c.Set(x, -3, LidShade);
        if (open == 2)
            c.Sprite(-3, lidTop + 1, SparkArt, ch => ch == 'o' ? Pal.Body : 0u);
    }

    /// <summary>Épaule d'un bras qui vise : juste à côté du corps (sur son liseré), en haut du flanc.</summary>
    private static (int x, int y) AimRoot(int left, int top) => (left - 1, top + 6);

    /// <summary>
    /// Décalage de l'épaule d'un bras qui passe devant le corps (pointeur sur le côté) : plus bas et un peu
    /// dans le corps, pour que le bras passe sous l'œil, comme avant.
    /// </summary>
    private const int CrossShiftX = 2, CrossShiftY = 3;

    /// <summary>
    /// Épaule d'un bras qui vise, dans le dessin (origine aux pieds). <paramref name="right"/> : le bras droit ;
    /// <paramref name="cross"/> : le bras passe devant le corps.
    /// </summary>
    public static (int x, int y) AimShoulder(in PetPose p, bool right = false, bool cross = false)
    {
        int bw = Math.Max(8, p.BodyW);
        int l = -(bw / 2) + p.DX, r = l + bw - 1;
        int top = p.BodyBottom - Math.Max(6, p.BodyH) + 1;
        var (x, y) = AimRoot(l, top);
        if (cross) { x += CrossShiftX; y += CrossShiftY; }
        return (right ? l + r - x : x, y);
    }

    /// <summary>Un bras levé de son côté penche toujours au moins un peu vers l'extérieur (≈ 17° de la verticale).</summary>
    public const double AimMinOutward = 0.3;

    /// <summary>
    /// Le pointeur, vu de l'épaule à (vx, vy), est-il assez sur le côté pour que ce bras passe devant le corps ?
    /// Au-dessus de lui, le bras se lève de son côté. Un peu d'hystérésis évite qu'il hésite à la limite.
    /// </summary>
    public static bool AimCrosses(double vx, double vy, bool right, bool wasCrossing)
    {
        double inward = right ? -vx : vx, up = -vy;
        double deg = Math.Atan2(inward, up) * (180 / Math.PI); // 0 : droit au-dessus ; 90 : de l'autre côté, à l'horizontale
        return deg > (wasCrossing ? 55 : 65);
    }

    /// <summary>
    /// Bras tendu vers un point situé à (vx, vy) de son épaule, dans le dessin (vx positif vers la droite du dessin).
    /// <paramref name="cross"/> faux : le bras reste de son côté (si le point est au-dessus de lui ou de l'autre côté,
    /// il monte le long de son flanc). Vrai : il pointe droit vers le point, devant le corps.
    /// <paramref name="len"/> : longueur en pixels art.
    /// </summary>
    public static Arm AimArm(double vx, double vy, double len, bool right, bool cross)
    {
        int outward = right ? 1 : -1;
        double d = Math.Sqrt(vx * vx + vy * vy);
        if (d < 1e-6) { vx = 0; vy = -1; } else { vx /= d; vy /= d; }
        if (!cross && vx * outward < AimMinOutward)
        {
            vx = AimMinOutward * outward;
            vy = -Math.Sqrt(1 - vx * vx);
        }
        int hx = (int)Math.Round(vx * len), hy = (int)Math.Round(vy * len);
        if (right) hx = -hx; // le bras droit est dessiné en miroir du gauche
        return cross
            ? new Arm(ArmK.Aim, (sbyte)hx, (sbyte)hy, CrossShiftX, CrossShiftY)
            : new Arm(ArmK.Aim, (sbyte)hx, (sbyte)hy);
    }

    /// <summary>Un bras dessiné devant le corps : d'abord son contour (trait plus épais), puis sa couleur.</summary>
    private static void DrawArmFront(ArtCanvas c, Arm a, int left, int right, int top, bool mirror, uint col)
    {
        var (rx, ry) = AimRoot(left, top);
        rx += a.RX; ry += a.RY;
        FrontArm(c, rx, ry, rx + a.DX, ry + a.DY, col, mirror ? left + right : int.MinValue);
    }

    /// <summary>
    /// Bras dessiné devant le corps, de l'épaule à la main : son contour, qui part juste après l'épaule (le bras
    /// sort du corps sans trait sombre à la jointure), puis sa couleur. <paramref name="mir"/> : axe du miroir, ou MinValue.
    /// </summary>
    private static void FrontArm(ArtCanvas c, int rx, int ry, int hx, int hy, uint col, int mir)
    {
        int sx = rx + Math.Sign(hx - rx), sy = ry + Math.Sign(hy - ry);
        Stroke(c, sx, sy, hx, hy, 5, Pal.Outline, mir);
        Stroke(c, rx, ry, hx, hy, 3, col, mir);
    }

    private static readonly int[] SwingRaise = { 0, 1, 2, 1 };

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
                case Legs.Swing:
                {
                    // chaque paire passe devant (elle paraît plus courte), puis repart en arrière
                    int ph = p.Kick & 3;
                    int raise = SwingRaise[pairA ? ph : (ph + 2) & 3];
                    y1 = bottom + 5 - raise;
                    if (outer) bend = -1;
                    break;
                }
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
        if (a.Kind == ArmK.Hidden) return;
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
            case ArmK.Aim: hx = rx; hy = ry; break; // la main est à (DX, DY) de l'épaule (ajouté juste après)
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
        // le petit reflet blanc, en haut à droite de chaque œil ouvert
        void Shine(int dy) { c.Set(x0 + 1, y0 + dy, Pal.Shine); c.Set(x1 + 1, y0 + dy, Pal.Shine); }

        switch (p.Eyes)
        {
            case Eyes.Open:
            case Eyes.Wide:
                for (int y = 0; y < 4; y++) { Both(0, y); Both(1, y); }
                Shine(0);
                break;
            case Eyes.Half:
                for (int y = 2; y < 4; y++) { Both(0, y); Both(1, y); }
                Shine(2);
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
                Shine(1);
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
            case Mouth.Flat:
                c.Set(ml, my + 1, e); c.Set(ml + 1, my + 1, e);
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

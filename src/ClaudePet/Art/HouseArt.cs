namespace ClaudePet.Art;

/// <summary>La maison de Pet (icône de l'appli), en pixel art, avec sa porte au milieu.</summary>
internal static class HouseArt
{
    // 24 x 24 : la grande maison (48, 72, 96 px... et 240 dans 256).
    private static readonly string[] Big =
    {
        "........................",
        "...........kk..kkk......",
        "..........krrk.kck......",
        ".........krrRRkkck......",
        "........krrrRRRkck......",
        ".......khrrrRRRRkk......",
        "......khrrrrRRRRRk......",
        ".....khrrrrrRRRRRRk.....",
        "....khrrrrrrRRRRRRRk....",
        "...khrrrrrrrRRRRRRRRk...",
        "..khrrrrrrrrRRRRRRRRRk..",
        ".kkkkkkkkkkkkkkkkkkkkkk.",
        "...kWWWWWWWWWWWWWWWWk...",
        "...kwkkkkwwwwwwkkkkwk...",
        "...kwkBbkwkkkkwkBbkwk...",
        "...kwkbbkkddddkkbbkwk...",
        "...kwkkkkkdDddkkkkkwk...",
        "...kwwwwwkdDddkwwwwwk...",
        "...kwwwwwkdDddkwwwwwk...",
        "...kwwwwwkdDdokwwwwwk...",
        "...kwwwwwkdDddkwwwwwk...",
        "...kwwwwwkdDddkwwwwwk...",
        "...kWWWWWkdDddkWWWWWk...",
        "...kkkkkkkkkkkkkkkkkk...",
    };

    // 16 x 16 : la petite maison (16, 32, 64, 128 px).
    private static readonly string[] Small =
    {
        "................",
        ".......kk.kkk...",
        "......krrkkck...",
        ".....krrRRkck...",
        "....khrrRRRkk...",
        "...khrrrRRRRk...",
        "..khrrrrRRRRRk..",
        ".kkkkkkkkkkkkkk.",
        "..kWWWWWWWWWWk..",
        "..kwbbwkkwbbwk..",
        "..kwbbkddkbbwk..",
        "..kwwwkdokwwwk..",
        "..kwwwkddkwwwk..",
        "..kwwwkddkwwwk..",
        "..kWWWkddkWWWk..",
        "..kkkkkkkkkkkk..",
    };

    public static uint Color(char ch) => ch switch
    {
        'k' => Col.Rgb(0x3A, 0x1E, 0x14),
        'r' => Col.Rgb(0xD9, 0x77, 0x57),
        'R' => Col.Rgb(0xB4, 0x58, 0x3B),
        'h' => Col.Rgb(0xF0, 0xA4, 0x87),
        'w' => Col.Rgb(0xF5, 0xE7, 0xC9),
        'W' => Col.Rgb(0xDB, 0xC5, 0x9C),
        'd' => Col.Rgb(0x7A, 0x41, 0x27),
        'D' => Col.Rgb(0x5A, 0x2C, 0x18),
        'o' => Col.Rgb(0xF5, 0xC8, 0x4C),
        'b' => Col.Rgb(0x8F, 0xD0, 0xF0),
        'B' => Col.Rgb(0xDD, 0xF3, 0xFF),
        'c' => Col.Rgb(0x9A, 0x8F, 0x86),
        _ => 0,
    };

    /// <summary>Comment une taille d'icône est dessinée : quelle grille, quel facteur, quelle marge.</summary>
    private static (string[] rows, double k, double off) Layout(int size)
    {
        if (size >= 200) { int k = size / 24; return (Big, k, (size - 24 * k) / 2.0); }
        if (size >= 48 && size % 24 == 0) return (Big, size / 24, 0);
        if (size % 16 == 0) return (Small, size / 16, 0);
        if (size >= 24) return (Big, size / 24.0, 0);
        return (Small, size / 16.0, 0);
    }

    /// <summary>Porte (centre x, bas, hauteur), en fraction de la taille affichée de l'icône.</summary>
    public static (double cx, double bottom, double height) Door(int size)
    {
        var (rows, k, off) = Layout(size);
        bool big = rows == Big;
        double cx = big ? 12 : 8, bot = big ? 23 : 15, h = big ? 8 : 5;
        return ((off + cx * k) / size, (off + bot * k) / size, h * k / size);
    }

    /// <summary>Pixels ARGB (non prémultipliés) de l'icône, agrandie au plus proche voisin.</summary>
    public static uint[] Render(int size)
    {
        var (rows, k, off) = Layout(size);
        int art = rows.Length;
        var px = new uint[size * size];
        for (int y = 0; y < size; y++)
        {
            int sy = (int)Math.Floor((y - off) / k);
            if (sy < 0 || sy >= art) continue;
            string row = rows[sy];
            for (int x = 0; x < size; x++)
            {
                int sx = (int)Math.Floor((x - off) / k);
                if (sx < 0 || sx >= art) continue;
                px[y * size + x] = Color(row[sx]);
            }
        }
        return px;
    }
}

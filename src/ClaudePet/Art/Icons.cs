namespace ClaudePet.Art;

/// <summary>Petites icônes 12 x 12 en pixel art, pour les bulles et la baie.</summary>
internal static class Icons
{
    public sealed class Icon
    {
        public readonly int W, H;
        public readonly uint[] Px;
        public Icon(string[] rows)
        {
            H = rows.Length;
            W = rows[0].Length;
            Px = new uint[W * H];
            for (int y = 0; y < H; y++)
                for (int x = 0; x < W; x++)
                    Px[y * W + x] = Color(rows[y][x]);
        }
    }

    public static uint Color(char c) => c switch
    {
        'k' => Col.Rgb(0x3A, 0x2A, 0x22),
        'g' => Col.Rgb(0x6C, 0xC2, 0x4A),
        'G' => Col.Rgb(0xA8, 0xE2, 0x7A),
        'd' => Col.Rgb(0x3E, 0x8A, 0x2E),
        'r' => Col.Rgb(0xE5, 0x39, 0x50),
        'R' => Col.Rgb(0xFF, 0x86, 0x96),
        'D' => Col.Rgb(0xA8, 0x20, 0x3A),
        'w' => Col.Rgb(0xFF, 0xFF, 0xFF),
        'y' => Col.Rgb(0xFF, 0xD3, 0x4D),
        'Y' => Col.Rgb(0xFF, 0xF0, 0xA0),
        'S' => Col.Rgb(0xC9, 0xCE, 0xD6),
        's' => Col.Rgb(0x8E, 0x95, 0x9E),
        'h' => Col.Rgb(0xD9, 0x77, 0x57),
        'H' => Col.Rgb(0xB4, 0x58, 0x3B),
        'c' => Col.Rgb(0xF5, 0xE7, 0xC9),
        'n' => Col.Rgb(0x7A, 0x41, 0x27),
        'b' => Col.Rgb(0x8F, 0xD0, 0xF0),
        'x' => Col.Rgb(0xE5, 0x39, 0x35),
        'z' => Col.Rgb(0x5B, 0x6E, 0x9E),
        _ => 0,
    };

    public static readonly Icon Leaf = new(new[]
    {
        "............",
        "........kkk.",
        "......kkGGk.",
        ".....kGGggk.",
        "....kGgggdk.",
        "...kGgggddk.",
        "...kgggddk..",
        "..kgggddkk..",
        "..kggdkk....",
        ".kkdkk......",
        ".k..........",
        "k...........",
    });

    public static readonly Icon Berry = new(new[]
    {
        ".....kk.....",
        "....kddkkk..",
        ".....kdkGgk.",
        "....kkkkggk.",
        "...kRRrkkk..",
        "..kRwRrrrk..",
        "..kRRrrrrk..",
        "..krrrrrrk..",
        "..krrrrrDk..",
        "..krrrrDDk..",
        "...kDDDDk...",
        "....kkkk....",
    });

    public static readonly Icon SoundOn = new(new[]
    {
        "............",
        "....kk......",
        "...kSk...k..",
        ".kkSSk.k..k.",
        "kSSSSk..k.k.",
        "kSSSSk..k.k.",
        "kSSSSk..k.k.",
        ".kkSSk.k..k.",
        "...kSk...k..",
        "....kk......",
        "............",
        "............",
    });

    public static readonly Icon SoundOff = new(new[]
    {
        "............",
        "....kk......",
        "...kSk......",
        ".kkSSk.x...x",
        "kSSSSk..x.x.",
        "kSSSSk...x..",
        "kSSSSk..x.x.",
        ".kkSSk.x...x",
        "...kSk......",
        "....kk......",
        "............",
        "............",
    });

    public static readonly Icon Moon = new(new[]
    {
        "....kkkk....",
        "..kkyyyk....",
        ".kyyykk.....",
        ".kyyk...zzz.",
        "kyyk......z.",
        "kyyk.....z..",
        "kyyk....zzz.",
        ".kyyk.......",
        ".kyyykk.....",
        "..kkyyyyk...",
        "....kkkkk...",
        "............",
    });

    public static readonly Icon House = new(new[]
    {
        ".....kk.....",
        "....khHk....",
        "...khhHHk...",
        "..khhhHHHk..",
        ".khhhhHHHHk.",
        "kkkkkkkkkkkk",
        ".kcccccccck.",
        ".kcbbkkkkck.",
        ".kcbbknnkck.",
        ".kcccknnkck.",
        ".kcccknnkck.",
        ".kkkkkkkkkk.",
    });

    public static readonly Icon Gear = new(new[]
    {
        "....kkkk....",
        ".kk.kSSk.kk.",
        ".kSkkSSkkSk.",
        "..kSSSSSSk..",
        "kkkSSkkSSkkk",
        "kSSSk..kSSSk",
        "kSSSk..kSSSk",
        "kkkSSkkSSkkk",
        "..kSSSSSSk..",
        ".kSkkSSkkSk.",
        ".kk.kSSk.kk.",
        "....kkkk....",
    });
}

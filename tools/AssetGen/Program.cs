using System.IO.Compression;
using ClaudePet.Art;

namespace AssetGen;

// Outil de fabrication : l'icône maison (.ico multi-tailles) et des planches d'aperçu (PNG).
//   dotnet run --project tools/AssetGen -- ico assets/house.ico
//   dotnet run --project tools/AssetGen -- sheet out/poses.png
//   dotnet run --project tools/AssetGen -- house out/house.png
internal static class Program
{
    private static int Main(string[] args)
    {
        if (args.Length < 2) { Console.WriteLine("usage: ico|sheet|house <fichier>"); return 1; }
        string path = Path.GetFullPath(args[1]);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        switch (args[0])
        {
            case "ico": WriteIco(path); break;
            case "sheet": Sheets.Poses(path); break;
            case "house": Sheets.Houses(path); break;
            case "aim": Sheets.Aim(path); break;
            case "props": Sheets.Props(path); break;
            default: Console.WriteLine("commande inconnue"); return 1;
        }
        Console.WriteLine("écrit : " + path);
        return 0;
    }

    /// <summary>Icône Windows : BMP 32 bits pour les petites tailles, PNG pour les grandes.</summary>
    private static void WriteIco(string path)
    {
        int[] sizes = { 16, 20, 24, 32, 40, 48, 60, 64, 72, 80, 96, 128, 256 };
        var images = new List<byte[]>();
        foreach (int s in sizes)
        {
            uint[] px = HouseArt.Render(s);
            images.Add(s >= 64 ? Png.Encode(px, s, s) : IcoBmp(px, s));
        }
        using var f = File.Create(path);
        using var w = new BinaryWriter(f);
        w.Write((ushort)0); w.Write((ushort)1); w.Write((ushort)sizes.Length);
        int offset = 6 + 16 * sizes.Length;
        for (int i = 0; i < sizes.Length; i++)
        {
            int s = sizes[i];
            w.Write((byte)(s >= 256 ? 0 : s));
            w.Write((byte)(s >= 256 ? 0 : s));
            w.Write((byte)0); w.Write((byte)0);
            w.Write((ushort)1); w.Write((ushort)32);
            w.Write(images[i].Length);
            w.Write(offset);
            offset += images[i].Length;
        }
        foreach (var img in images) w.Write(img);
    }

    private static byte[] IcoBmp(uint[] px, int s)
    {
        using var ms = new MemoryStream();
        using var w = new BinaryWriter(ms);
        int maskStride = ((s + 31) / 32) * 4;
        w.Write(40); w.Write(s); w.Write(s * 2); w.Write((ushort)1); w.Write((ushort)32);
        w.Write(0); w.Write(s * s * 4 + maskStride * s); w.Write(0); w.Write(0); w.Write(0); w.Write(0);
        for (int y = s - 1; y >= 0; y--)
            for (int x = 0; x < s; x++) w.Write(px[y * s + x]); // BGRA en mémoire = ARGB little-endian
        for (int y = s - 1; y >= 0; y--)
        {
            var row = new byte[maskStride];
            for (int x = 0; x < s; x++)
                if ((px[y * s + x] >> 24) == 0) row[x / 8] |= (byte)(0x80 >> (x % 8));
            w.Write(row);
        }
        return ms.ToArray();
    }
}

internal static class Png
{
    private static readonly uint[] Crc = MakeCrc();

    private static uint[] MakeCrc()
    {
        var t = new uint[256];
        for (uint n = 0; n < 256; n++)
        {
            uint c = n;
            for (int k = 0; k < 8; k++) c = (c & 1) != 0 ? 0xEDB88320u ^ (c >> 1) : c >> 1;
            t[n] = c;
        }
        return t;
    }

    public static byte[] Encode(uint[] argb, int w, int h)
    {
        using var raw = new MemoryStream();
        for (int y = 0; y < h; y++)
        {
            raw.WriteByte(0);
            for (int x = 0; x < w; x++)
            {
                uint c = argb[y * w + x];
                raw.WriteByte((byte)(c >> 16)); raw.WriteByte((byte)(c >> 8)); raw.WriteByte((byte)c); raw.WriteByte((byte)(c >> 24));
            }
        }
        using var z = new MemoryStream();
        using (var zs = new ZLibStream(z, CompressionLevel.SmallestSize, true)) raw.WriteTo(zs);
        using var o = new MemoryStream();
        o.Write(new byte[] { 137, 80, 78, 71, 13, 10, 26, 10 });
        var ihdr = new byte[13];
        BE(ihdr, 0, (uint)w); BE(ihdr, 4, (uint)h);
        ihdr[8] = 8; ihdr[9] = 6;
        Chunk(o, "IHDR", ihdr);
        Chunk(o, "IDAT", z.ToArray());
        Chunk(o, "IEND", Array.Empty<byte>());
        return o.ToArray();
    }

    private static void BE(byte[] b, int i, uint v) { b[i] = (byte)(v >> 24); b[i + 1] = (byte)(v >> 16); b[i + 2] = (byte)(v >> 8); b[i + 3] = (byte)v; }

    private static void Chunk(Stream o, string type, byte[] data)
    {
        var len = new byte[4]; BE(len, 0, (uint)data.Length); o.Write(len);
        var td = new byte[4 + data.Length];
        for (int i = 0; i < 4; i++) td[i] = (byte)type[i];
        Array.Copy(data, 0, td, 4, data.Length);
        o.Write(td);
        uint c = 0xFFFFFFFF;
        foreach (byte b in td) c = Crc[(c ^ b) & 255] ^ (c >> 8);
        var crc = new byte[4]; BE(crc, 0, c ^ 0xFFFFFFFF); o.Write(crc);
    }

    public static void Save(string path, uint[] argb, int w, int h) => File.WriteAllBytes(path, Encode(argb, w, h));
}

/// <summary>Planches d'aperçu pour vérifier le dessin.</summary>
internal static class Sheets
{
    public static void Poses(string path)
    {
        var poses = new List<PetPose>();
        var s = PetPose.Stand;
        poses.Add(s);
        poses.Add(s with { LiftA = 1, L = Arm.Side.Shift(0, -1), R = Arm.Side.Shift(0, 1), LookX = 1 });
        poses.Add(s with { LiftB = 1, L = Arm.Side.Shift(0, 1), R = Arm.Side.Shift(0, -1), LookX = 1 });
        poses.Add(s with { LiftA = 2, BodyBottom = -7, DX = 1, L = Arm.Side.Shift(0, -2), R = Arm.Side.Shift(0, 2), LookX = 1, Mouth = Mouth.SmallO });
        poses.Add(PetPose.Sitting);
        poses.Add(PetPose.Sitting with { Eyes = Eyes.Closed });
        poses.Add(s with { BodyW = 22, BodyH = 18, L = Arm.Up, R = Arm.Up, Eyes = Eyes.Closed, Mouth = Mouth.Yawn });
        poses.Add(s with { BodyW = 26, BodyH = 13, BodyBottom = -4, Legs = Legs.Kneel, L = Arm.Down, R = Arm.Down, LookY = -1 });
        poses.Add(s with { BodyW = 22, BodyH = 18, Legs = Legs.Tuck, L = Arm.Raised, R = Arm.Raised, Eyes = Eyes.Happy, Mouth = Mouth.Smile });
        poses.Add(s with { Legs = Legs.Dangle, L = Arm.Up, R = Arm.Raised, Eyes = Eyes.Wide, Mouth = Mouth.SmallO });
        poses.Add(s with { BodyW = 28, BodyH = 12, BodyBottom = -4, Legs = Legs.Spread, L = Arm.Out, R = Arm.Out, Eyes = Eyes.Squeeze });
        poses.Add(PetPose.Sitting with { Eyes = Eyes.Dizzy, Mouth = Mouth.Frown });
        poses.Add(PetPose.Sitting with { Eyes = Eyes.Swirl, Mouth = Mouth.SmallO, DX = 1 });
        poses.Add(s with { L = Arm.Hold, R = Arm.Raised, Legs = Legs.Dangle, Eyes = Eyes.Happy, LookY = -1 });
        poses.Add(s with { L = Arm.Reach, R = Arm.Reach, Eyes = Eyes.Wide, LookY = -1, Mouth = Mouth.Smile });
        poses.Add(s with { Eyes = Eyes.Angry, Mouth = Mouth.Frown, Red = 1 });
        poses.Add(s with { Eyes = Eyes.Happy, Mouth = Mouth.Wide, L = Arm.Raised, R = Arm.Raised });
        poses.Add(s with { Eyes = Eyes.Happy, Mouth = Mouth.Chew, Blush = true });
        poses.Add(s with { L = new Arm(ArmK.Dig), R = new Arm(ArmK.Dig, 0, -3), Eyes = Eyes.Squeeze, LookY = 1 });
        poses.Add(s with { L = Arm.Up, R = Arm.Side, LiftA = 2, Eyes = Eyes.Open, LookY = -1, LookX = 1 });
        poses.Add(s with { Eyes = Eyes.Squeeze, BodyW = 27, BodyH = 14, BodyBottom = -5, Mouth = Mouth.Open });
        poses.Add(s with { L = Arm.Hold, R = Arm.Out, Legs = Legs.Dangle, Kick = 1, Eyes = Eyes.Wide, Mouth = Mouth.Grin });
        poses.Add(s with { Mouth = Mouth.Grin, Eyes = Eyes.Happy, Blush = true });
        poses.Add(s with { Eyes = Eyes.Half, Mouth = Mouth.None });

        int k = 6, cols = 6;
        var c = PetPainter.NewCanvas();
        int cw = c.W * k, ch = c.H * k;
        int rows = (poses.Count + cols - 1) / cols;
        int W = cols * cw, H = rows * ch;
        var img = new uint[W * H];
        for (int i = 0; i < img.Length; i++)
        {
            int x = i % W, y = i / W;
            bool alt = (((x / cw) + (y / ch)) & 1) == 0;
            img[i] = alt ? 0xFFE9EEF5 : 0xFF2B3140;
        }
        for (int i = 0; i < poses.Count; i++)
        {
            PetPainter.Draw(c, poses[i]);
            int ox = (i % cols) * cw, oy = (i / cols) * ch;
            for (int y = 0; y < ch; y++)
                for (int x = 0; x < cw; x++)
                {
                    uint p = c.Px[(y / k) * c.W + x / k];
                    if ((p >> 24) != 0) img[(oy + y) * W + ox + x] = p;
                }
            // ligne du sol
            for (int x = 0; x < cw; x++) img[(oy + c.OY * k) * W + ox + x] = 0xFF60C060;
        }
        Png.Save(path, img, W, H);
    }

    /// <summary>
    /// Bras levés vers le pointeur (pose « ReachUp ») pour plusieurs positions du pointeur, marqué d'une croix bleue.
    /// Même calcul que l'appli : Pet se tourne vers le pointeur s'il est assez loin sur le côté.
    /// </summary>
    public static void Aim(string path)
    {
        (int x, int y)[] points =
        {
            (24, -51), (0, -33), (-24, -51), (8, -45),
            (20, -40), (30, -34), (40, -28), (48, -22),
            (-48, -22), (50, -12), (-30, -30), (-14, -38),
        };
        const int k = 4, cols = 4, x0 = -60, y0 = -58, aw = 120, ah = 64;
        int cw = aw * k, ch = ah * k;
        int rows = (points.Length + cols - 1) / cols;
        int W = cols * cw, H = rows * ch;
        var img = new uint[W * H];
        for (int i = 0; i < img.Length; i++)
        {
            int x = i % W, y = i / W;
            img[i] = ((((x / cw) + (y / ch)) & 1) == 0) ? 0xFFE9EEF5 : 0xFFD5DCE6;
        }
        var c = PetPainter.NewCanvas();
        var stand = PetPose.Stand;
        for (int i = 0; i < points.Length; i++)
        {
            var (px, py) = points[i];
            int facing = Math.Abs(px) > 24 * 0.4 ? Math.Sign(px) : 1;
            double dxDraw = px * facing; // le pointeur dans le dessin (retourné si Pet regarde à gauche)
            Arm ArmTo(bool right, double len)
            {
                var (sx, sy) = PetPainter.AimShoulder(stand, right);
                bool cross = PetPainter.AimCrosses(dxDraw - sx, py - sy, right, false);
                if (cross) (sx, sy) = PetPainter.AimShoulder(stand, right, cross: true);
                return PetPainter.AimArm(dxDraw - sx, py - sy, len, right, cross);
            }
            var l = ArmTo(false, 7);
            var r = ArmTo(true, 8);
            PetPainter.Draw(c, stand with { L = l, R = r, Eyes = Eyes.Wide, Mouth = Mouth.Smile, LookY = -1, LookX = Math.Sign(dxDraw) });
            int ox = (i % cols) * cw, oy = (i / cols) * ch;
            for (int cy = 0; cy < c.H; cy++)
                for (int cx = 0; cx < c.W; cx++)
                {
                    uint p = c.Px[cy * c.W + cx];
                    if ((p >> 24) == 0) continue;
                    int ax = cx - c.OX, ay = cy - c.OY;
                    if (facing < 0) ax = -1 - ax;
                    Fill(img, W, ox + (ax - x0) * k, oy + (ay - y0) * k, k, k, p);
                }
            for (int x = 0; x < cw; x++) img[(oy + (0 - y0) * k) * W + ox + x] = 0xFF60C060; // le sol
            // le pointeur
            int mx = ox + (px - x0) * k, my = oy + (py - y0) * k;
            Fill(img, W, mx - 2 * k, my, 5 * k, k, 0xFF2050E0);
            Fill(img, W, mx, my - 2 * k, k, 5 * k, 0xFF2050E0);
        }
        Png.Save(path, img, W, H);
    }

    /// <summary>Humeurs (bouche), reflets des yeux, clignement, café et ordinateur.</summary>
    public static void Props(string path)
    {
        var s = PetPose.Stand;
        var sit = PetPose.Sitting;
        var laptop = s with { BodyBottom = -4, Legs = Legs.Kneel, L = Arm.Hidden, R = Arm.Hidden, Prop = Prop.Laptop };
        sit = sit with { L = Arm.Hidden, R = Arm.Hidden };
        var poses = new List<PetPose>
        {
            s with { Mouth = Mouth.Smile }, s with { Mouth = Mouth.Flat }, s with { Mouth = Mouth.Frown },
            s with { Eyes = Eyes.Half, Mouth = Mouth.Flat }, s with { Eyes = Eyes.Closed, Mouth = Mouth.Flat },
            s with { Eyes = Eyes.Angry, Brows = true, Mouth = Mouth.Frown, Red = 1 },
            sit with { Prop = Prop.Mug, PropF = 0, Mouth = Mouth.Smile },
            sit with { Prop = Prop.Mug, PropF = 1, Mouth = Mouth.Smile, LookY = -1 },
            sit with { Prop = Prop.Mug, PropF = 2, Eyes = Eyes.Happy },
            laptop with { PropF = 0, Mouth = Mouth.Smile, L = Arm.Down, R = Arm.Down }, laptop with { PropF = 1, Mouth = Mouth.Smile },
            laptop with { PropF = 2, PropG = 0, Mouth = Mouth.Flat },
            laptop with { PropF = 2, PropG = 1, Mouth = Mouth.Flat }, laptop with { PropF = 2, PropG = 2, Mouth = Mouth.Smile },
            laptop with { PropF = 2, PropG = 3, LookY = -1, LookX = 1, Mouth = Mouth.SmallO },
            s with { Prop = Prop.Headphones, Mouth = Mouth.Smile }, s with { Prop = Prop.Headphones, Eyes = Eyes.Happy, Mouth = Mouth.Smile, BodyBottom = -5, BodyH = 15, LiftA = 1, L = Arm.Raised },
            sit with { Prop = Prop.Book, LookY = 1, Mouth = Mouth.Flat }, sit with { Prop = Prop.Book, PropF = 1, LookY = 1, LookX = 1, Mouth = Mouth.SmallO },
            s with { Prop = Prop.Wand, Mouth = Mouth.SmallO, R = Arm.Hidden }, s with { Prop = Prop.Wand, PropF = 1, Mouth = Mouth.SmallO, R = Arm.Hidden },
            s with { Legs = Legs.Swing, Kick = 0, L = Arm.Down, R = Arm.Down, Mouth = Mouth.Smile },
            s with { Legs = Legs.Swing, Kick = 1, L = Arm.Down, R = Arm.Down, Mouth = Mouth.Smile },
            s with { Legs = Legs.Swing, Kick = 2, L = Arm.Down, R = Arm.Down, Mouth = Mouth.Smile, Eyes = Eyes.Happy },
            s with { Legs = Legs.Swing, Kick = 3, L = Arm.Down, R = Arm.Down, Mouth = Mouth.Smile },
        };
        RenderSheet(path, poses, 5);
    }

    private static void RenderSheet(string path, List<PetPose> poses, int cols)
    {
        int k = 6;
        var c = PetPainter.NewCanvas();
        int cw = c.W * k, ch = c.H * k;
        int rows = (poses.Count + cols - 1) / cols;
        int W = cols * cw, H = rows * ch;
        var img = new uint[W * H];
        for (int i = 0; i < img.Length; i++)
        {
            int x = i % W, y = i / W;
            img[i] = ((((x / cw) + (y / ch)) & 1) == 0) ? 0xFFE9EEF5 : 0xFFD5DCE6;
        }
        for (int i = 0; i < poses.Count; i++)
        {
            PetPainter.Draw(c, poses[i]);
            int ox = (i % cols) * cw, oy = (i / cols) * ch;
            for (int y = 0; y < ch; y++)
                for (int x = 0; x < cw; x++)
                {
                    uint p = c.Px[(y / k) * c.W + x / k];
                    if ((p >> 24) != 0) img[(oy + y) * W + ox + x] = p;
                }
            for (int x = 0; x < cw; x++) img[(oy + c.OY * k + k) * W + ox + x] = 0xFF60C060; // le sol, sous la ligne y = 0
        }
        Png.Save(path, img, W, H);
    }

    private static void Fill(uint[] img, int W, int x, int y, int w, int h, uint col)
    {
        int H = img.Length / W;
        for (int yy = Math.Max(0, y); yy < Math.Min(H, y + h); yy++)
            for (int xx = Math.Max(0, x); xx < Math.Min(W, x + w); xx++)
                img[yy * W + xx] = col;
    }

    public static void Houses(string path)
    {
        int[] sizes = { 16, 24, 32, 48, 60, 72, 96, 256 };
        int W = sizes.Sum() + 10 * sizes.Length, H = 256 + 20;
        var img = new uint[W * H];
        Array.Fill(img, 0xFF3C6E8F);
        int x0 = 5;
        foreach (int s in sizes)
        {
            uint[] px = HouseArt.Render(s);
            for (int y = 0; y < s; y++)
                for (int x = 0; x < s; x++)
                    if ((px[y * s + x] >> 24) != 0) img[(10 + y) * W + x0 + x] = px[y * s + x];
            var (cx, bot, hh) = HouseArt.Door(s);
            int dx = x0 + (int)(cx * s), dy = 10 + (int)(bot * s);
            if (dy < H && dx < W) img[dy * W + dx] = 0xFFFF00FF;
            x0 += s + 10;
        }
        Png.Save(path, img, W, H);
    }
}

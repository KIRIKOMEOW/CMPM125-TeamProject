using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

// Procedural placeholder pixel art for Night Shift. Every sprite is an original design drawn from
// primitives, so the project has no third-party art. Swap the PNGs in Assets/Art/Sprites any time.
public static class PixelArt
{
    public const int PPU = 32;
    public const string Folder = "Assets/Art/Sprites";

    // Cold warehouse palette, warm orange reserved for blades, coins and hits.
    static readonly Color32 Clear = new Color32(0, 0, 0, 0);
    static readonly Color32 Ink = new Color32(20, 18, 32, 255);
    static readonly Color32 NavyDark = new Color32(34, 40, 74, 255);
    static readonly Color32 Navy = new Color32(52, 64, 112, 255);
    static readonly Color32 NavyLight = new Color32(84, 104, 164, 255);
    static readonly Color32 Skin = new Color32(232, 190, 160, 255);
    static readonly Color32 Orange = new Color32(255, 140, 40, 255);
    static readonly Color32 OrangeLight = new Color32(255, 196, 92, 255);
    static readonly Color32 OrangeDark = new Color32(200, 88, 30, 255);
    static readonly Color32 GreyDark = new Color32(58, 62, 76, 255);
    static readonly Color32 Grey = new Color32(100, 106, 122, 255);
    static readonly Color32 GreyLight = new Color32(150, 158, 176, 255);
    static readonly Color32 Denim = new Color32(48, 56, 82, 255);
    static readonly Color32 Steel = new Color32(120, 140, 160, 255);
    static readonly Color32 SteelLight = new Color32(178, 196, 212, 255);
    static readonly Color32 Cyan = new Color32(80, 230, 255, 255);
    static readonly Color32 CyanLight = new Color32(220, 255, 255, 255);
    static readonly Color32 Red = new Color32(232, 60, 70, 255);
    static readonly Color32 Maroon = new Color32(96, 38, 56, 255);
    static readonly Color32 MaroonLight = new Color32(140, 58, 76, 255);
    static readonly Color32 MaroonDark = new Color32(62, 24, 38, 255);
    static readonly Color32 Gold = new Color32(255, 208, 64, 255);
    static readonly Color32 GoldLight = new Color32(255, 240, 160, 255);
    static readonly Color32 GoldDark = new Color32(196, 140, 30, 255);
    static readonly Color32 Floor = new Color32(44, 50, 64, 255);
    static readonly Color32 FloorLight = new Color32(54, 61, 77, 255);
    static readonly Color32 FloorSeam = new Color32(36, 41, 54, 255);
    static readonly Color32 Wall = new Color32(28, 32, 44, 255);
    static readonly Color32 WallLight = new Color32(62, 70, 92, 255);
    static readonly Color32 WallMortar = new Color32(20, 22, 32, 255);
    static readonly Color32 Wood = new Color32(122, 86, 56, 255);
    static readonly Color32 WoodLight = new Color32(162, 120, 78, 255);
    static readonly Color32 WoodDark = new Color32(84, 58, 40, 255);
    static readonly Color32 BarrelRed = new Color32(168, 50, 44, 255);
    static readonly Color32 BarrelLight = new Color32(214, 86, 62, 255);
    static readonly Color32 Hazard = new Color32(240, 200, 60, 255);
    static readonly Color32 White = new Color32(255, 255, 255, 255);

    class Canvas
    {
        public readonly int W, H;
        public readonly Color32[] Px;

        public Canvas(int w, int h)
        {
            W = w; H = h;
            Px = new Color32[w * h];
            for (int i = 0; i < Px.Length; i++) Px[i] = Clear;
        }

        public void Set(int x, int y, Color32 c)
        {
            if (x >= 0 && y >= 0 && x < W && y < H) Px[y * W + x] = c;
        }

        public Color32 Get(int x, int y) => x >= 0 && y >= 0 && x < W && y < H ? Px[y * W + x] : Clear;

        public void Rect(int x, int y, int w, int h, Color32 c)
        {
            for (int j = y; j < y + h; j++)
                for (int i = x; i < x + w; i++)
                    Set(i, j, c);
        }

        public void Ellipse(float cx, float cy, float rx, float ry, Color32 c)
        {
            for (int j = 0; j < H; j++)
                for (int i = 0; i < W; i++)
                {
                    float dx = (i + 0.5f - cx) / rx, dy = (j + 0.5f - cy) / ry;
                    if (dx * dx + dy * dy <= 1f) Set(i, j, c);
                }
        }

        // Rows are listed top to bottom like a picture; '.' is transparent.
        public void Map(string[] rows, Dictionary<char, Color32> palette, int ox = 0, int oy = 0)
        {
            for (int r = 0; r < rows.Length; r++)
            {
                int y = oy + rows.Length - 1 - r;
                for (int x = 0; x < rows[r].Length; x++)
                    if (palette.TryGetValue(rows[r][x], out Color32 c)) Set(ox + x, y, c);
            }
        }

        public void Outline(Color32 c)
        {
            var copy = (Color32[])Px.Clone();
            for (int y = 0; y < H; y++)
                for (int x = 0; x < W; x++)
                {
                    if (copy[y * W + x].a != 0) continue;
                    bool edge = false;
                    for (int k = 0; k < 4 && !edge; k++)
                    {
                        int nx = x + (k == 0 ? 1 : k == 1 ? -1 : 0), ny = y + (k == 2 ? 1 : k == 3 ? -1 : 0);
                        if (nx >= 0 && ny >= 0 && nx < W && ny < H && copy[ny * W + nx].a > 200) edge = true;
                    }
                    if (edge) Px[y * W + x] = c;
                }
        }
    }

    public static Dictionary<string, Sprite> GenerateAll()
    {
        Directory.CreateDirectory(Folder);
        var sprites = new Dictionary<string, Sprite>
        {
            ["kestrel"] = Save("kestrel", Kestrel()),
            ["rusher"] = Save("rusher", Rusher()),
            ["guardbot"] = Save("guardbot", GuardBot()),
            ["brute"] = Save("brute", Brute()),
            ["blade"] = Save("blade", Blade()),
            ["bolt"] = Save("bolt", Bolt()),
            ["coin"] = Save("coin", Coin()),
            ["medkit"] = Save("medkit", Medkit()),
            ["crate"] = Save("crate", Crate()),
            ["barrel"] = Save("barrel", Barrel()),
            ["shadow"] = Save("shadow", Shadow(24, 8)),
            ["shadow_big"] = Save("shadow_big", Shadow(40, 12)),
            ["floor"] = Save("floor", FloorTile(), tiled: true),
            ["wall"] = Save("wall", WallTile(), tiled: true),
            ["pixel"] = Save("pixel", Solid(2, 2, White)),
            ["marker"] = Save("marker", Ring(16)),
            ["aim"] = Save("aim", AimChevron()),
            ["ui_panel"] = Save("ui_panel", Frame(12, new Color32(16, 18, 28, 235), new Color32(84, 104, 164, 255)), border: 4),
            ["ui_button"] = Save("ui_button", Frame(12, new Color32(40, 48, 80, 255), new Color32(120, 140, 200, 255)), border: 4),
            ["ui_fill"] = Save("ui_fill", Solid(4, 4, White), border: 1),
            ["ui_knob"] = Save("ui_knob", Knob()),
            ["ui_check"] = Save("ui_check", Check()),
            ["ui_vignette"] = Save("ui_vignette", Vignette(64, 36), smooth: true),
        };
        return sprites;
    }

    static Canvas Kestrel()
    {
        // Kestrel: hooded night guard with an orange scarf, facing right.
        var c = new Canvas(32, 32);
        c.Rect(12, 2, 3, 5, GreyDark);           // boots
        c.Rect(17, 2, 3, 5, GreyDark);
        c.Rect(9, 6, 14, 3, NavyDark);           // coat hem
        c.Rect(10, 8, 12, 9, Navy);              // coat
        c.Rect(16, 7, 1, 9, NavyLight);          // zipper highlight
        c.Rect(10, 10, 12, 1, Ink);              // belt
        c.Rect(15, 10, 2, 1, Orange);            // buckle
        c.Rect(11, 12, 2, 2, Gold);              // security badge
        c.Rect(8, 9, 2, 6, NavyDark);            // back arm
        c.Rect(21, 10, 3, 5, Navy);              // front arm
        c.Rect(23, 11, 2, 2, Skin);              // hand
        c.Rect(25, 12, 3, 1, Orange);            // blade ready in hand
        c.Rect(10, 16, 12, 2, Orange);           // scarf
        c.Rect(10, 17, 12, 1, OrangeLight);
        c.Rect(6, 13, 4, 3, Orange);             // scarf tail
        c.Rect(5, 12, 2, 2, OrangeDark);
        c.Ellipse(16f, 22.5f, 6.5f, 6f, NavyDark); // hood
        c.Rect(9, 19, 2, 4, NavyDark);           // hood drape
        c.Ellipse(18.5f, 21.5f, 3.5f, 3.2f, Skin); // face opening
        c.Rect(19, 22, 1, 1, Ink);               // eye
        c.Rect(21, 22, 1, 1, Ink);
        c.Rect(12, 27, 7, 1, NavyLight);         // hood rim light
        c.Outline(Ink);
        return c;
    }

    static Canvas Rusher()
    {
        // Masked intruder in a grey hoodie, hunched forward.
        var c = new Canvas(32, 32);
        c.Rect(12, 2, 3, 6, Denim);
        c.Rect(18, 2, 3, 6, Denim);
        c.Rect(11, 2, 4, 1, Ink);
        c.Rect(17, 2, 4, 1, Ink);
        c.Rect(8, 10, 3, 6, GreyDark);           // backpack
        c.Ellipse(16f, 12.5f, 7f, 6f, Grey);     // hoodie body
        c.Rect(11, 8, 11, 2, GreyDark);          // hem shade
        c.Rect(15, 10, 4, 3, GreyLight);         // pocket
        c.Rect(21, 11, 5, 3, Grey);              // reaching arm
        c.Rect(25, 11, 2, 3, GreyDark);          // glove
        c.Ellipse(19f, 20.5f, 5.5f, 5f, GreyDark); // hood
        c.Ellipse(21f, 19.5f, 3.2f, 3f, Ink);    // mask
        c.Rect(21, 20, 1, 1, Red);               // eyes
        c.Rect(23, 20, 1, 1, Red);
        c.Outline(Ink);
        return c;
    }

    static Canvas GuardBot()
    {
        // Boxy security drone on treads with a cyan sensor eye.
        var c = new Canvas(32, 32);
        c.Rect(8, 2, 16, 4, GreyDark);           // treads
        for (int x = 9; x < 24; x += 3) c.Rect(x, 3, 1, 2, Grey);
        c.Rect(9, 6, 14, 11, Steel);             // body
        c.Rect(9, 6, 2, 11, Grey);
        c.Rect(9, 16, 14, 1, SteelLight);
        c.Rect(13, 10, 2, 2, Cyan);              // chest light
        c.Rect(23, 10, 5, 2, GreyDark);          // gun arm
        c.Rect(27, 10, 1, 2, Cyan);
        c.Rect(11, 17, 10, 6, SteelLight);       // head
        c.Rect(12, 19, 8, 2, Ink);               // visor
        c.Rect(17, 19, 3, 2, Cyan);              // eye
        c.Rect(18, 20, 1, 1, CyanLight);
        c.Rect(15, 23, 1, 3, Grey);              // antenna
        c.Rect(15, 26, 1, 1, Red);
        c.Outline(Ink);
        return c;
    }

    static Canvas Brute()
    {
        // Heavy armored intruder, drawn at 48px so it reads bigger without breaking the pixel grid.
        var c = new Canvas(48, 48);
        c.Rect(16, 2, 6, 9, MaroonDark);
        c.Rect(27, 2, 6, 9, MaroonDark);
        c.Rect(15, 2, 8, 2, Ink);
        c.Rect(26, 2, 8, 2, Ink);
        c.Rect(7, 12, 5, 12, Maroon);            // arms
        c.Rect(37, 12, 5, 12, Maroon);
        c.Ellipse(9.5f, 11f, 3.5f, 3f, GreyDark); // fists
        c.Ellipse(39.5f, 11f, 3.5f, 3f, GreyDark);
        c.Ellipse(24.5f, 20f, 13f, 11f, Maroon); // torso
        c.Ellipse(26f, 20f, 8f, 7f, MaroonLight); // chest plate
        c.Rect(14, 12, 21, 2, GreyDark);          // belt chain
        for (int x = 15; x < 34; x += 3) c.Rect(x, 12, 1, 2, Grey);
        c.Ellipse(12f, 27f, 5f, 4f, Grey);       // shoulder pads
        c.Ellipse(37f, 27f, 5f, 4f, Grey);
        c.Ellipse(26f, 33.5f, 5f, 4f, MaroonDark); // head
        c.Rect(27, 34, 1, 1, Red);
        c.Rect(30, 34, 1, 1, Red);
        c.Outline(Ink);
        return c;
    }

    static Canvas Blade()
    {
        var c = new Canvas(8, 8);
        c.Map(new[]
        {
            "...LL...",
            "...OO...",
            "...OO...",
            "LOOWWOOL",
            "LOOWWOOL",
            "...OO...",
            "...OO...",
            "...LL...",
        }, new Dictionary<char, Color32> { ['L'] = OrangeLight, ['O'] = Orange, ['W'] = new Color32(255, 240, 210, 255) });
        return c;
    }

    static Canvas Bolt()
    {
        var c = new Canvas(6, 6);
        c.Map(new[] { "..CC..", ".CWWC.", "CWWWWC", "CWWWWC", ".CWWC.", "..CC.." },
            new Dictionary<char, Color32> { ['C'] = Cyan, ['W'] = CyanLight });
        return c;
    }

    static Canvas Coin()
    {
        var c = new Canvas(8, 8);
        c.Map(new[] { "..DDDD..", ".DGGGGD.", "DGYYGGGD", "DGYGGGGD", "DGGGGGGD", "DGGGGGDD", ".DGGGDD.", "..DDDD.." },
            new Dictionary<char, Color32> { ['D'] = GoldDark, ['G'] = Gold, ['Y'] = GoldLight });
        return c;
    }

    static Canvas Medkit()
    {
        var c = new Canvas(10, 10);
        c.Map(new[]
        {
            "KKKKKKKKKK",
            "KWWWRRWWWK",
            "KWWWRRWWWK",
            "KWRRRRRRWK",
            "KWRRRRRRWK",
            "KWWWRRWWWK",
            "KWWWRRWWWK",
            "KWWWWWWWWK",
            "KSSSSSSSSK",
            "KKKKKKKKKK",
        }, new Dictionary<char, Color32> { ['K'] = Ink, ['W'] = White, ['R'] = Red, ['S'] = GreyLight });
        return c;
    }

    static Canvas Crate()
    {
        var c = new Canvas(32, 32);
        c.Rect(1, 1, 30, 30, Wood);
        for (int y = 8; y < 31; y += 7) c.Rect(1, y, 30, 1, WoodDark);   // planks
        for (int i = 3; i < 29; i++) { c.Set(i, i, WoodLight); c.Set(i + 1, i, WoodLight); } // brace
        c.Rect(1, 1, 30, 2, WoodDark);
        c.Rect(1, 29, 30, 2, WoodLight);
        c.Rect(1, 1, 2, 30, WoodDark);
        c.Rect(29, 1, 2, 30, WoodDark);
        c.Outline(Ink);
        return c;
    }

    static Canvas Barrel()
    {
        var c = new Canvas(24, 28);
        c.Rect(3, 2, 18, 20, BarrelRed);
        c.Rect(3, 2, 3, 20, new Color32(130, 38, 34, 255));
        c.Rect(16, 3, 2, 18, BarrelLight);
        c.Rect(3, 9, 18, 5, Hazard);             // hazard band
        for (int x = 3; x < 21; x += 4) c.Rect(x, 9, 2, 5, Ink);
        c.Ellipse(12f, 22.5f, 9f, 3.5f, BarrelLight); // lid
        c.Ellipse(12f, 22.5f, 6f, 2f, BarrelRed);
        c.Rect(3, 2, 18, 1, new Color32(110, 32, 30, 255));
        c.Outline(Ink);
        return c;
    }

    static Canvas Shadow(int w, int h)
    {
        var c = new Canvas(w, h);
        c.Ellipse(w / 2f, h / 2f, w / 2f, h / 2f, new Color32(0, 0, 0, 90));
        return c;
    }

    static Canvas FloorTile()
    {
        var c = new Canvas(16, 16);
        c.Rect(0, 0, 16, 16, Floor);
        var rng = new System.Random(7);
        for (int i = 0; i < 10; i++) c.Set(rng.Next(1, 15), rng.Next(1, 15), FloorLight);
        c.Rect(0, 0, 16, 1, FloorSeam);
        c.Rect(15, 0, 1, 16, FloorSeam);
        return c;
    }

    static Canvas WallTile()
    {
        var c = new Canvas(16, 16);
        c.Rect(0, 0, 16, 16, Wall);
        c.Rect(0, 7, 16, 1, WallMortar);
        c.Rect(0, 15, 16, 1, WallMortar);
        c.Rect(7, 8, 1, 7, WallMortar);
        c.Rect(3, 0, 1, 7, WallMortar);
        c.Rect(11, 0, 1, 7, WallMortar);
        c.Rect(0, 14, 7, 1, WallLight);
        c.Rect(8, 14, 8, 1, WallLight);
        c.Rect(0, 6, 3, 1, WallLight);
        c.Rect(4, 6, 7, 1, WallLight);
        c.Rect(12, 6, 4, 1, WallLight);
        return c;
    }

    static Canvas Solid(int w, int h, Color32 color)
    {
        var c = new Canvas(w, h);
        c.Rect(0, 0, w, h, color);
        return c;
    }

    static Canvas Ring(int size)
    {
        var c = new Canvas(size, size);
        c.Ellipse(size / 2f, size / 2f, size / 2f, size / 2f, White);
        c.Ellipse(size / 2f, size / 2f, size / 2f - 2f, size / 2f - 2f, Clear);
        return c;
    }

    static Canvas AimChevron()
    {
        var c = new Canvas(6, 6);
        c.Map(new[] { "O.....", "OO....", ".OO...", ".OO...", "OO....", "O....." },
            new Dictionary<char, Color32> { ['O'] = new Color32(255, 160, 60, 200) });
        return c;
    }

    static Canvas Frame(int size, Color32 fill, Color32 edge)
    {
        var c = new Canvas(size, size);
        c.Rect(0, 0, size, size, Ink);
        c.Rect(1, 1, size - 2, size - 2, edge);
        c.Rect(2, 2, size - 4, size - 4, fill);
        return c;
    }

    static Canvas Knob()
    {
        var c = new Canvas(8, 8);
        c.Ellipse(4f, 4f, 4f, 4f, Ink);
        c.Ellipse(4f, 4f, 3f, 3f, OrangeLight);
        return c;
    }

    static Canvas Check()
    {
        var c = new Canvas(8, 8);
        c.Map(new[] { "......OO", ".....OO.", "....OO..", "OO.OO...", ".OOO....", "..O.....", "........", "........" },
            new Dictionary<char, Color32> { ['O'] = Orange });
        return c;
    }

    static Canvas Vignette(int w, int h)
    {
        var c = new Canvas(w, h);
        for (int y = 0; y < h; y++)
            for (int x = 0; x < w; x++)
            {
                float dx = (x + 0.5f) / w * 2f - 1f, dy = (y + 0.5f) / h * 2f - 1f;
                float d = Mathf.Sqrt(dx * dx + dy * dy) / 1.414f;
                float a = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.45f, 1f, d));
                c.Set(x, y, new Color32(220, 30, 40, (byte)(a * 255)));
            }
        return c;
    }

    static Sprite Save(string name, Canvas canvas, bool tiled = false, int border = 0, bool smooth = false)
    {
        string path = $"{Folder}/{name}.png";
        var tex = new Texture2D(canvas.W, canvas.H, TextureFormat.RGBA32, false);
        tex.SetPixels32(canvas.Px);
        tex.Apply();
        File.WriteAllBytes(path, tex.EncodeToPNG());
        Object.DestroyImmediate(tex);
        AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);

        var importer = (TextureImporter)AssetImporter.GetAtPath(path);
        importer.textureType = TextureImporterType.Sprite;
        importer.spriteImportMode = SpriteImportMode.Single;
        importer.spritePixelsPerUnit = PPU;
        importer.filterMode = smooth ? FilterMode.Bilinear : FilterMode.Point;
        importer.textureCompression = TextureImporterCompression.Uncompressed;
        importer.mipmapEnabled = false;
        importer.alphaIsTransparency = true;
        importer.wrapMode = tiled ? TextureWrapMode.Repeat : TextureWrapMode.Clamp;

        var settings = new TextureImporterSettings();
        importer.ReadTextureSettings(settings);
        settings.spriteAlignment = (int)SpriteAlignment.Center;
        settings.spriteMeshType = tiled || border > 0 ? SpriteMeshType.FullRect : SpriteMeshType.Tight;
        settings.spriteBorder = new Vector4(border, border, border, border);
        importer.SetTextureSettings(settings);
        importer.SaveAndReimport();

        return AssetDatabase.LoadAssetAtPath<Sprite>(path);
    }
}

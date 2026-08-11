using System.Drawing.Drawing2D;

namespace FRLG.StarterTool.App;

public static class GameText
{
    private const int CellSize = 16;

    private const int CellsAcross = 16;

    private const int LetterSpacing = 1;

    public const int LineHeight = 14;

    public static void Draw(Graphics g, string text, Point origin, int scale)
    {
        Bitmap? sheet = Assets.Font;
        if (sheet == null) return;

        InterpolationMode interpolation = g.InterpolationMode;
        PixelOffsetMode offset = g.PixelOffsetMode;

        g.InterpolationMode = InterpolationMode.NearestNeighbor;
        g.PixelOffsetMode = PixelOffsetMode.Half;

        int x = 0, y = 0;
        foreach (char character in text)
        {
            if (character == '\n')
            {
                x = 0;
                y += LineHeight;
                continue;
            }

            int id = Glyph(character);
            g.DrawImage(sheet,
                new Rectangle(
                    origin.X + x * scale, origin.Y + y * scale, CellSize * scale, CellSize * scale),
                id % CellsAcross * CellSize, id / CellsAcross * CellSize, CellSize, CellSize,
                GraphicsUnit.Pixel);

            x += Width(id) + LetterSpacing;
        }

        g.InterpolationMode = interpolation;
        g.PixelOffsetMode = offset;
    }

    public static int Measure(string text)
    {
        int widest = 0, line = 0;
        foreach (char character in text)
        {
            if (character == '\n')
            {
                widest = Math.Max(widest, line);
                line = 0;
                continue;
            }

            line += Width(Glyph(character)) + LetterSpacing;
        }

        return Math.Max(widest, line);
    }

    public static int LineCount(string text) => text.Count(c => c == '\n') + 1;

    private static int Width(int id)
    {
        byte[] widths = Assets.GlyphWidths;
        return id < widths.Length ? widths[id] : 0;
    }

    private static int Glyph(char character) => character switch
    {
        >= '0' and <= '9' => 0xA1 + (character - '0'),
        >= 'A' and <= 'Z' => 0xBB + (character - 'A'),
        >= 'a' and <= 'z' => 0xD5 + (character - 'a'),
        'é' => 0x1B,
        '…' => 0xB0,
        '!' => 0xAB,
        '?' => 0xAC,
        '.' => 0xAD,
        '-' => 0xAE,
        ',' => 0xB8,
        '/' => 0xBA,
        ':' => 0xF0,

        '\'' or '’' => 0xB4,
        _ => 0x00,
    };
}

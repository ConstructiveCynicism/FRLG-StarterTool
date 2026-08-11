using System.Drawing.Imaging;
using System.Runtime.InteropServices;

namespace FRLG.StarterTool.App;

public static class GbaColors
{
    private static readonly byte[] AgbColorCurve =
    [
        0, 3, 8, 14, 20, 26, 33, 40, 47, 54, 62, 70, 78, 86, 94, 103,
        112, 120, 129, 138, 147, 157, 166, 176, 185, 195, 205, 215, 225, 235, 245, 255,
    ];

    private const double Gamma = 2.2;

    private static readonly Dictionary<int, int> Cache = new();

    private static int ToAgbColor(int red, int green, int blue)
    {
        byte r = AgbColorCurve[red];
        byte g = AgbColorCurve[green];
        byte b = AgbColorCurve[blue];

        if (g != b)
        {
            g = (byte)Math.Round(
                Math.Pow((Math.Pow(g / 255.0, Gamma) * 5 + Math.Pow(b / 255.0, Gamma)) / 6, 1 / Gamma) * 255,
                MidpointRounding.AwayFromZero);
        }

        return r << 16 | g << 8 | b;
    }

    private static int ToFiveBit(int channel) => (int)Math.Round(channel * 31.0 / 255.0);

    public static Color Correct(Color colour)
    {
        int corrected = Correct(colour.ToArgb() & 0x00FFFFFF);
        return Color.FromArgb(colour.A, corrected >> 16 & 0xFF, corrected >> 8 & 0xFF, corrected & 0xFF);
    }

    private static int Correct(int rgb)
    {
        if (Cache.TryGetValue(rgb, out int cached)) return cached;

        int corrected = ToAgbColor(
            ToFiveBit(rgb >> 16 & 0xFF), ToFiveBit(rgb >> 8 & 0xFF), ToFiveBit(rgb & 0xFF));

        Cache[rgb] = corrected;
        return corrected;
    }

    public static Bitmap Correct(Bitmap bitmap)
    {
        BitmapData data = bitmap.LockBits(
            new Rectangle(0, 0, bitmap.Width, bitmap.Height),
            ImageLockMode.ReadWrite, PixelFormat.Format32bppArgb);
        try
        {
            var row = new int[bitmap.Width];
            for (int y = 0; y < bitmap.Height; y++)
            {
                IntPtr line = data.Scan0 + y * data.Stride;
                Marshal.Copy(line, row, 0, row.Length);

                for (int x = 0; x < row.Length; x++)
                {
                    int alpha = row[x] >> 24 & 0xFF;
                    if (alpha == 0) continue;

                    row[x] = alpha << 24 | Correct(row[x] & 0x00FFFFFF);
                }

                Marshal.Copy(row, 0, line, row.Length);
            }
        }
        finally
        {
            bitmap.UnlockBits(data);
        }

        return bitmap;
    }
}

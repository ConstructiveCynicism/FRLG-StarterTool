using System.Reflection;

namespace FRLG.StarterTool.App;

public static class Assets
{
    private static readonly Dictionary<int, Image?> SpriteCache = new();
    private static Icon? _appIcon;

    public static Icon AppIcon => _appIcon ??= LoadIcon();

    private static Icon LoadIcon()
    {
        Assembly assembly = typeof(Assets).Assembly;
        using Stream stream = assembly.GetManifestResourceStream(
            "FRLG.StarterTool.App.Resources.app.ico")!;
        return new Icon(stream);
    }

    private const int SwitchIconSize = 16;

    private static readonly Color SwitchOn = Color.FromArgb(0x3A, 0x8F, 0xD6);
    private static readonly Color SwitchOff = Color.FromArgb(0x8A, 0x8A, 0x8A);

    public static Image Globe(bool enabled)
        => enabled
            ? _globeOn ??= DrawGlobe(SwitchOn)
            : _globeOff ??= DrawGlobe(SwitchOff);

    private static Image? _globeOn;
    private static Image? _globeOff;

    private static Image DrawGlobe(Color colour)
    {
        var bitmap = NewIcon();
        using (Graphics g = Graphics.FromImage(bitmap))
        {
            g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
            using var pen = new Pen(colour, 1.4f);

            var circle = new RectangleF(1.5f, 1.5f, SwitchIconSize - 3f, SwitchIconSize - 3f);
            g.DrawEllipse(pen, circle);

            g.DrawLine(pen, circle.Left, SwitchIconSize / 2f, circle.Right, SwitchIconSize / 2f);
            g.DrawEllipse(pen, circle.Left + circle.Width * 0.30f, circle.Top,
                circle.Width * 0.40f, circle.Height);
            g.DrawLine(pen, SwitchIconSize / 2f, circle.Top, SwitchIconSize / 2f, circle.Bottom);
        }
        return bitmap;
    }

    public static Image Pin(bool pinned)
        => pinned
            ? _pinOn ??= DrawPin(SwitchOn)
            : _pinOff ??= DrawPin(SwitchOff);

    private static Image? _pinOn;
    private static Image? _pinOff;

    private static readonly PointF[] PinOutline =
    {
        new(640, 480), new(720, 560), new(720, 640), new(520, 640), new(520, 880),
        new(480, 920), new(440, 880), new(440, 640), new(240, 640), new(240, 560),
        new(320, 480), new(320, 200), new(280, 200), new(280, 120), new(680, 120),
        new(680, 200), new(640, 200)
    };

    private static Image DrawPin(Color colour)
    {
        var bitmap = NewIcon();
        using (Graphics g = Graphics.FromImage(bitmap))
        {
            g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;

            using var path = new System.Drawing.Drawing2D.GraphicsPath();
            path.AddPolygon(PinOutline);

            RectangleF ink = path.GetBounds();
            const float Inset = 0.5f;
            float scale = (SwitchIconSize - 2 * Inset) / Math.Max(ink.Width, ink.Height);

            using (var fit = new System.Drawing.Drawing2D.Matrix())
            {
                fit.Translate(
                    (SwitchIconSize - ink.Width * scale) / 2f,
                    (SwitchIconSize - ink.Height * scale) / 2f);
                fit.Scale(scale, scale);
                fit.Translate(-ink.Left, -ink.Top);
                path.Transform(fit);
            }

            using var brush = new SolidBrush(colour);
            g.FillPath(brush, path);
        }
        return bitmap;
    }

    private static Bitmap NewIcon()
        => new(SwitchIconSize, SwitchIconSize, System.Drawing.Imaging.PixelFormat.Format32bppArgb);

    public static Image? Sprite(int dexNumber)
    {
        if (SpriteCache.TryGetValue(dexNumber, out Image? cached)) return cached;

        Image? image = Trim(Load($"pokemon.{dexNumber}.png"));
        SpriteCache[dexNumber] = image;
        return image;
    }

    private static Image? Trim(Image? image)
    {
        if (image is not Bitmap source) return image;

        int width = source.Width;
        int height = source.Height;

        var pixels = new int[width * height];
        System.Drawing.Imaging.BitmapData data = source.LockBits(
            new Rectangle(0, 0, width, height),
            System.Drawing.Imaging.ImageLockMode.ReadOnly,
            System.Drawing.Imaging.PixelFormat.Format32bppArgb);
        try
        {
            for (int y = 0; y < height; y++)
            {
                System.Runtime.InteropServices.Marshal.Copy(
                    data.Scan0 + y * data.Stride, pixels, y * width, width);
            }
        }
        finally
        {
            source.UnlockBits(data);
        }

        int left = width, top = height, right = -1, bottom = -1;
        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                if ((pixels[y * width + x] & unchecked((int)0xFF000000)) == 0) continue;

                if (x < left) left = x;
                if (x > right) right = x;
                if (y < top) top = y;
                if (y > bottom) bottom = y;
            }
        }

        if (right < left || bottom < top) return image;

        var bounds = new Rectangle(left, top, right - left + 1, bottom - top + 1);
        if (bounds.Width == width && bounds.Height == height) return image;

        var trimmed = new Bitmap(bounds.Width, bounds.Height,
            System.Drawing.Imaging.PixelFormat.Format32bppArgb);
        using (Graphics g = Graphics.FromImage(trimmed))
        {
            g.DrawImage(source, new Rectangle(0, 0, bounds.Width, bounds.Height), bounds,
                GraphicsUnit.Pixel);
        }
        source.Dispose();
        return trimmed;
    }

    private static Image? Load(string name)
    {
        Assembly assembly = typeof(Assets).Assembly;
        using Stream? stream = assembly.GetManifestResourceStream($"FRLG.StarterTool.App.Resources.{name}");
        if (stream == null) return null;

        using var original = Image.FromStream(stream);
        return new Bitmap(original);
    }
}

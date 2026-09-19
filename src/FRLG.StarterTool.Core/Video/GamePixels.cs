namespace FRLG.StarterTool.Core.Video;

public static class GamePixels
{
    public const int Width = 240;

    public const int Height = 160;

    public const int Bytes = Width * Height * 4;

    public static bool IsGameShaped(int width, int height) =>
        width > 0 && height > 0 && Math.Abs(width * (double)Height / (height * (double)Width) - 1.0) <= 0.03;

    public static int SourceX(int gx, int sourceWidth) => Math.Min((int)((gx + 0.5) * sourceWidth / Width), sourceWidth - 1);

    public static int SourceY(int gy, int sourceHeight) => Math.Min((int)((gy + 0.5) * sourceHeight / Height), sourceHeight - 1);

    public static void Sample(ReadOnlySpan<byte> source, int sourceStride, int x, int y, int width, int height, Span<byte> target)
    {
        Span<int> columns = stackalloc int[Width];
        for (int gx = 0; gx < Width; gx++) columns[gx] = (x + SourceX(gx, width)) * 4;

        int o = 0;
        for (int gy = 0; gy < Height; gy++)
        {
            int row = (y + SourceY(gy, height)) * sourceStride;
            for (int gx = 0; gx < Width; gx++, o += 4)
            {
                int i = row + columns[gx];
                target[o] = source[i];
                target[o + 1] = source[i + 1];
                target[o + 2] = source[i + 2];
                target[o + 3] = 255;
            }
        }
    }
}

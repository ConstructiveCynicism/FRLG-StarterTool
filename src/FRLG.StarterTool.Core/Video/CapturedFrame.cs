namespace FRLG.StarterTool.Core.Video;

public sealed record CapturedFrame(long Seq, double StampMs, int Width, int Height, byte[] Bgra)
{
    public int Stride => Width * 4;

    public long Bytes => Bgra.LongLength;
}

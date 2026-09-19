namespace FRLG.StarterTool.Core.Video;

public static class WhiteFade
{
    public const int WhiteLevel = 224;

    public const double WhiteShare = 0.5;

    public const double PlateauMs = 300.0;

    public const int ReachedTolerance = 4;

    public const double CompleteShare = 0.85;

    public const double MovedShare = 0.25;

    private const int Samples = 48;

    public static bool IsWhite(CapturedFrame frame)
    {
        int white = 0, total = 0;
        foreach (int darkest in Sample(frame))
        {
            total++;
            if (darkest >= WhiteLevel) white++;
        }
        return total > 0 && white >= total * WhiteShare;
    }

    public static int LastBeforeComplete(IReadOnlyList<CapturedFrame> frames, double fromMs)
    {
        int start = -1, first = -1;
        for (int i = 0; i < frames.Count; i++)
        {
            if (frames[i].StampMs < fromMs) continue;
            if (start < 0) start = i;
            if (IsWhite(frames[i]))
            {
                first = i;
                break;
            }
        }
        if (first < 0) return -1;

        int[] final = Levels(frames[first]);
        for (int i = first + 1; i < frames.Count && frames[i].StampMs <= frames[first].StampMs + PlateauMs; i++)
        {
            int[] levels = Levels(frames[i]);
            if (levels.Length != final.Length) continue;
            for (int k = 0; k < final.Length; k++) final[k] = Math.Max(final[k], levels[k]);
        }

        int[] before = Levels(frames[start]);
        if (before.Length != final.Length) return -1;
        var moved = new List<int>(final.Length);
        for (int k = 0; k < final.Length; k++)
        {
            if (final[k] - before[k] > 2 * ReachedTolerance) moved.Add(k);
        }
        if (moved.Count < final.Length * MovedShare) return -1;

        for (int i = start; i < frames.Count; i++)
        {
            int[] levels = Levels(frames[i]);
            if (levels.Length != final.Length) continue;
            int reached = moved.Count(k => final[k] - levels[k] <= ReachedTolerance);
            if (reached >= moved.Count * CompleteShare) return i > 0 ? i - 1 : -1;
        }
        return -1;
    }

    private static int[] Levels(CapturedFrame frame) => Sample(frame).ToArray();

    private static IEnumerable<int> Sample(CapturedFrame frame)
    {
        if (frame.Width <= 0 || frame.Height <= 0) yield break;

        int stepsX = Math.Min(Samples, frame.Width);
        int stepsY = Math.Min(Samples, frame.Height);
        byte[] bgra = frame.Bgra;
        for (int sy = 0; sy < stepsY; sy++)
        {
            int y = (int)((sy + 0.5) * frame.Height / stepsY);
            int row = y * frame.Stride;
            for (int sx = 0; sx < stepsX; sx++)
            {
                int x = (int)((sx + 0.5) * frame.Width / stepsX);
                int i = row + x * 4;
                yield return i + 2 < bgra.Length ? Math.Min(bgra[i], Math.Min(bgra[i + 1], bgra[i + 2])) : 0;
            }
        }
    }
}

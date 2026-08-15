namespace FRLG.StarterTool.Core.Npc;

public static class FrameWindow
{
    public const double MinimumContextMs = 10.0;

    public const double ContextFloor = 0.05;

    public static double HitChance(double deltaMs, double fps, double contextMs)
    {
        if (fps <= 0.0) return 0.0;

        double frameMs = 1000.0 / fps;
        double window = frameMs / 2.0 + Math.Max(0.0, contextMs) + MinimumContextMs;

        int first = (int)Math.Ceiling((deltaMs - window) / frameMs);
        int last = (int)Math.Floor((deltaMs + window) / frameMs);
        if (first > 0 || last < 0) return 0.0;

        int leading = (int)Math.Floor(deltaMs / frameMs + 0.5);
        if (leading != 0) return Math.Max(Share(deltaMs, frameMs, 0), ContextFloor);

        double taken = 0.0;
        for (int offset = first; offset <= last; offset++)
        {
            if (offset != leading) taken += Math.Max(Share(deltaMs, frameMs, offset), ContextFloor);
        }
        return Math.Clamp(1.0 - taken, 0.0, 1.0);
    }

    private static double Share(double deltaMs, double frameMs, int offset)
        => Math.Max(0.0, 1.0 - Math.Abs(deltaMs - offset * frameMs) / frameMs);

    public static IReadOnlyList<int> Candidates(double elapsedMs, double fps, double contextMs)
    {
        double frameMs = 1000.0 / fps;
        double window = frameMs / 2.0 + Math.Max(0.0, contextMs) + MinimumContextMs;

        int first = (int)Math.Ceiling((elapsedMs - window) / frameMs);
        int last = (int)Math.Floor((elapsedMs + window) / frameMs);
        if (first < 0) first = 0;

        var found = new List<int>();
        for (int frame = first; frame <= last; frame++) found.Add(frame);

        if (found.Count == 0) found.Add(LikelyFrame(elapsedMs, fps));

        found.Sort((a, b) =>
        {
            int byDelta = Math.Abs(elapsedMs - a * frameMs).CompareTo(Math.Abs(elapsedMs - b * frameMs));
            return byDelta != 0 ? byDelta : a.CompareTo(b);
        });
        return found;
    }

    public static double DeltaMs(double elapsedMs, double fps)
    {
        double frameMs = 1000.0 / fps;
        return elapsedMs - LikelyFrame(elapsedMs, fps) * frameMs;
    }

    public static double Weight(double elapsedMs, double fps, double contextMs, int frame)
    {
        double frameMs = 1000.0 / fps;
        double spread = frameMs + Math.Max(0.0, contextMs) + MinimumContextMs;
        return Math.Max(0.0, 1.0 - Math.Abs(elapsedMs - frame * frameMs) / spread);
    }

    public static IReadOnlyList<double> Likelihoods(double elapsedMs, double fps, double contextMs,
        IEnumerable<int> frames)
    {
        var weights = frames.Select(f => Weight(elapsedMs, fps, contextMs, f)).ToList();
        double total = weights.Sum();

        if (weights.Count == 0) return weights;
        if (total <= 0.0) return weights.Select(_ => 1.0 / weights.Count).ToList();

        return weights.Select(w => w / total).ToList();
    }

    public static int LikelyFrame(double elapsedMs, double fps)
    {
        double frame = Math.Floor(elapsedMs / 1000.0 * fps + 0.5);
        return frame < 0.0 ? 0 : (int)frame;
    }
}

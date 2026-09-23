namespace FRLG.StarterTool.Core.Audio;

public sealed class ScheduledBeep
{
    private readonly short[] _clip;

    public double TargetMs { get; }
    public bool Protected { get; }
    public int SourceFrames => _clip.Length / 2;
    public ReadOnlySpan<short> Clip => _clip;

    public ScheduledBeep(double targetMs, ReadOnlySpan<short> clip, bool protect = false)
    {
        TargetMs = targetMs;
        Protected = protect;
        _clip = clip.ToArray();
    }
}

public sealed class DeviceBeepMixer
{
    private const int SourceRate = 48000;
    private readonly int _sampleRate;
    private readonly List<Voice> _voices = new();

    public DeviceBeepMixer(int sampleRate) => _sampleRate = sampleRate;

    public static long TargetFrame(double targetMs, double clockMs, double clockFrame, double framesPerMs)
        => (long)Math.Round(clockFrame + (targetMs - clockMs) * framesPerMs);

    public static double FrameTime(long frame, double clockMs, double clockFrame, double framesPerMs)
        => clockMs + (frame - clockFrame) / framesPerMs;

    public bool IsPlaying(long frame) => _voices.Any(v => v.StartFrame is long start && start <= frame && EndFrame(v) > frame);

    public void Replace(IReadOnlyList<ScheduledBeep> beeps, double playedFrame, bool finishStarted = true)
    {
        _voices.RemoveAll(v => !finishStarted || v.StartFrame == null || EndFrame(v) <= playedFrame);
        long protectedEnd = _voices.Where(v => v.Beep.Protected).Select(EndFrame).DefaultIfEmpty(long.MinValue).Max();
        long earliest = protectedEnd == long.MinValue ? long.MinValue : protectedEnd + _sampleRate / 100;
        foreach (ScheduledBeep beep in beeps) _voices.Add(new Voice(beep, earliest));
    }

    public void Mix(Span<float> stereo, long firstFrame, double clockMs, double clockFrame,
        double framesPerMs, Action<double>? late = null)
    {
        stereo.Clear();
        int frames = stereo.Length / 2;
        long endFrame = firstFrame + frames;
        foreach (Voice voice in _voices)
        {
            if (voice.StartFrame == null)
            {
                long target = TargetFrame(voice.Beep.TargetMs, clockMs, clockFrame, framesPerMs);
                long start = Math.Max(Math.Max(target, firstFrame), voice.EarliestFrame);
                if (start >= endFrame) continue;
                voice.StartFrame = start;
                if (start > target) late?.Invoke((start - target) / framesPerMs);
            }

            long from = Math.Max(firstFrame, voice.StartFrame.Value);
            long to = Math.Min(endFrame, EndFrame(voice));
            ReadOnlySpan<short> clip = voice.Beep.Clip;
            for (long frame = from; frame < to; frame++)
            {
                double source = (frame - voice.StartFrame.Value) * (SourceRate / (double)_sampleRate);
                int whole = (int)source;
                int next = Math.Min(whole + 1, voice.Beep.SourceFrames - 1);
                double fraction = source - whole;
                int dest = (int)(frame - firstFrame) * 2;
                for (int channel = 0; channel < 2; channel++)
                {
                    double value = clip[whole * 2 + channel]
                        + (clip[next * 2 + channel] - clip[whole * 2 + channel]) * fraction;
                    stereo[dest + channel] += (float)(value / 32768.0);
                }
            }
        }

        for (int i = 0; i < stereo.Length; i++) stereo[i] = Math.Clamp(stereo[i], -1f, 32767f / 32768f);
        _voices.RemoveAll(v => v.StartFrame != null && EndFrame(v) <= clockFrame);
    }

    private long EndFrame(Voice voice) => voice.StartFrame!.Value
        + (long)Math.Ceiling(voice.Beep.SourceFrames * (double)_sampleRate / SourceRate);

    private sealed class Voice
    {
        public ScheduledBeep Beep { get; }
        public long EarliestFrame { get; }
        public long? StartFrame { get; set; }

        public Voice(ScheduledBeep beep, long earliestFrame)
        {
            Beep = beep;
            EarliestFrame = earliestFrame;
        }
    }
}

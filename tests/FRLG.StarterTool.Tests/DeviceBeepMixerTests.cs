using FRLG.StarterTool.Core.Audio;
using Xunit;

namespace FRLG.StarterTool.Tests;

public sealed class DeviceBeepMixerTests
{
    private static ScheduledBeep Beep(double target, int frames = 48, short value = 16384, bool protect = false)
        => new(target, Enumerable.Repeat(value, frames * 2).ToArray(), protect);

    [Fact]
    public void StartsAtTheTargetFrameInsideAChunk()
    {
        var mixer = new DeviceBeepMixer(48000);
        mixer.Replace(new[] { Beep(12) }, 0);
        var samples = new float[480 * 2];
        mixer.Mix(samples, 480, 0, 0, 48);
        Assert.All(samples.Take(96 * 2), sample => Assert.Equal(0, sample));
        Assert.All(samples.Skip(96 * 2).Take(48 * 2), sample => Assert.Equal(0.5f, sample));
        Assert.All(samples.Skip(144 * 2), sample => Assert.Equal(0, sample));
    }

    [Fact]
    public void ContinuesAcrossChunksWithoutMovingItsStart()
    {
        var mixer = new DeviceBeepMixer(48000);
        short[] ramp = Enumerable.Range(0, 96).SelectMany(i => new[] { (short)(i * 100), (short)(i * 100) }).ToArray();
        mixer.Replace(new[] { new ScheduledBeep(9.5, ramp) }, 0);
        var first = new float[480 * 2];
        var second = new float[480 * 2];
        mixer.Mix(first, 0, 0, 0, 48);
        mixer.Mix(second, 480, 200, 480, 48);
        Assert.Equal(2300 / 32768f, first[^1]);
        Assert.Equal(2400 / 32768f, second[0]);
        Assert.Equal(9500 / 32768f, second[71 * 2]);
        Assert.Equal(0, second[72 * 2]);
    }

    [Fact]
    public void AddsOverlappingBeepsAndClipsOnce()
    {
        var mixer = new DeviceBeepMixer(48000);
        mixer.Replace(new[] { Beep(0, value: 20000), Beep(0, value: 20000), Beep(0, value: -10000) }, 0);
        var samples = new float[48 * 2];
        mixer.Mix(samples, 0, 0, 0, 48);
        Assert.All(samples, sample => Assert.Equal(30000 / 32768f, sample));
        mixer.Replace(new[] { Beep(1, value: 20000), Beep(1, value: 20000) }, 48, false);
        mixer.Mix(samples, 48, 0, 0, 48);
        Assert.All(samples, sample => Assert.Equal(32767 / 32768f, sample));
    }

    [Fact]
    public void LateBeepPlaysWholeFromFirstWritableFrame()
    {
        var mixer = new DeviceBeepMixer(48000);
        mixer.Replace(new[] { Beep(0, 96) }, 0);
        var samples = new float[48 * 2];
        double lateness = 0;
        mixer.Mix(samples, 480, 0, 0, 48, late => lateness = late);
        Assert.Equal(10, lateness);
        Assert.All(samples, sample => Assert.Equal(0.5f, sample));
        mixer.Mix(samples, 528, 0, 0, 48);
        Assert.All(samples, sample => Assert.Equal(0.5f, sample));
    }

    [Fact]
    public void ResamplesEachBeepIndependentlyAt44100Hz()
    {
        var mixer = new DeviceBeepMixer(44100);
        short[] ramp = Enumerable.Range(0, 480).SelectMany(i => new[] { (short)(i * 50), (short)(i * 50) }).ToArray();
        mixer.Replace(new[] { new ScheduledBeep(1, ramp), new ScheduledBeep(2, ramp) }, 0);
        var samples = new float[200 * 2];
        mixer.Mix(samples, 0, 0, 0, 44.1);
        Assert.Equal(0, samples[44 * 2]);
        Assert.Equal((float)(50 * 48000.0 / 44100 / 32768), samples[45 * 2], 6);
        double expected = ((100 - 44) + (100 - 88)) * 48000.0 / 44100 * 50 / 32768;
        Assert.Equal((float)expected, samples[100 * 2], 6);
        mixer.Mix(samples, 200, 0, 0, 44.1);
        expected = ((200 - 44) + (200 - 88)) * 48000.0 / 44100 * 50 / 32768;
        Assert.Equal((float)expected, samples[0], 6);
    }

    [Fact]
    public void RefreshesClockMappingForADeviceRunning100PpmFast()
    {
        const double actualRate = 48000 * 1.0001;
        const double targetMs = 60000;
        var mixer = new DeviceBeepMixer(48000);
        mixer.Replace(new[] { Beep(targetMs) }, 0);
        var samples = new float[480 * 2];
        long detected = -1;
        for (long frame = 0; frame < actualRate * 61; frame += 480)
        {
            double clockFrame = Math.Max(0, frame - 960);
            double clockMs = clockFrame / actualRate * 1000;
            mixer.Mix(samples, frame, clockMs, clockFrame, 48);
            int index = Array.FindIndex(samples, sample => sample != 0);
            if (index < 0) continue;
            detected = frame + index / 2;
            break;
        }
        Assert.True(detected >= 0);
        Assert.InRange(Math.Abs(detected / actualRate * 1000 - targetMs), 0, 1000 / actualRate);
    }

    [Fact]
    public void CancelFinishesStartedBeepAndDropsFutureBeeps()
    {
        var mixer = new DeviceBeepMixer(48000);
        mixer.Replace(new[] { Beep(0, 960), Beep(100) }, 0);
        var samples = new float[480 * 2];
        mixer.Mix(samples, 0, 0, 0, 48);
        mixer.Replace(Array.Empty<ScheduledBeep>(), 100);
        mixer.Mix(samples, 480, 0, 0, 48);
        Assert.All(samples, sample => Assert.Equal(0.5f, sample));
        mixer.Mix(samples, 4800, 0, 0, 48);
        Assert.All(samples, sample => Assert.Equal(0, sample));
    }

    [Fact]
    public void ProtectedBeepDelaysReplacementUntilItFinishes()
    {
        var mixer = new DeviceBeepMixer(48000);
        mixer.Replace(new[] { Beep(0, 960, protect: true) }, 0);
        var samples = new float[480 * 2];
        mixer.Mix(samples, 0, 0, 0, 48);
        mixer.Replace(new[] { Beep(10) }, 100);
        mixer.Mix(samples, 480, 0, 0, 48);
        Assert.All(samples, sample => Assert.Equal(0.5f, sample));
        mixer.Mix(samples, 960, 0, 0, 48);
        Assert.All(samples, sample => Assert.Equal(0, sample));
        mixer.Mix(samples, 1440, 0, 0, 48);
        Assert.Equal(0.5f, samples[0]);
    }

    [Fact]
    public void PendingBeepUsesTheNewClockMappingBeforeItsFirstSample()
    {
        var mixer = new DeviceBeepMixer(48000);
        mixer.Replace(new[] { Beep(20) }, 0);
        var samples = new float[480 * 2];
        mixer.Mix(samples, 0, 0, 0, 48);
        Assert.All(samples, sample => Assert.Equal(0, sample));
        mixer.Mix(samples, 480, 15, 480, 48);
        Assert.All(samples.Take(240 * 2), sample => Assert.Equal(0, sample));
        Assert.Equal(0.5f, samples[240 * 2]);
    }

    [Fact]
    public void ProtectedBeepStillFinishesWhenItsSamplesAreAlreadyQueued()
    {
        var mixer = new DeviceBeepMixer(48000);
        mixer.Replace(new[] { Beep(0, 480, protect: true) }, 0);
        var samples = new float[480 * 2];
        mixer.Mix(samples, 0, 0, 0, 48);
        mixer.Mix(new float[120 * 2], 480, 0, 0, 48);
        mixer.Replace(new[] { Beep(0) }, 100);
        mixer.Mix(samples, 600, 0, 0, 48);
        Assert.All(samples.Take(360 * 2), sample => Assert.Equal(0, sample));
        Assert.Equal(0.5f, samples[360 * 2]);
    }

    [Fact]
    public void StopDiscardsStartedBeep()
    {
        var mixer = new DeviceBeepMixer(48000);
        mixer.Replace(new[] { Beep(0, 960) }, 0);
        var samples = new float[480 * 2];
        mixer.Mix(samples, 0, 0, 0, 48);
        mixer.Replace(Array.Empty<ScheduledBeep>(), 100, false);
        mixer.Mix(samples, 480, 0, 0, 48);
        Assert.All(samples, sample => Assert.Equal(0, sample));
    }
}

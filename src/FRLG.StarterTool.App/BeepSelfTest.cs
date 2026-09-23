using System.Collections.Concurrent;
using System.Globalization;
using System.Text;
using System.Text.Json;
using FRLG.StarterTool.Core.Settings;

namespace FRLG.StarterTool.App;

internal sealed class BeepSelfTest
{
    private sealed record Measurement(int Press, int Beep, double Target, double? Onset)
    {
        public double? Lateness => Onset - Target;
    }

    public static int Run(string[] args)
    {
        string directory = AppContext.BaseDirectory;
        var summary = new StringBuilder();
        int result = 0;
        bool timing = false;
        try
        {
            string mode = "both";
            int presses = 25;
            for (int i = 1; i < args.Length; i++)
            {
                string option = args[i];
                if (++i >= args.Length) throw new ArgumentException($"{option} requires a value.");
                switch (option)
                {
                    case "--mode": mode = args[i].ToLowerInvariant(); break;
                    case "--presses": presses = int.Parse(args[i], CultureInfo.InvariantCulture); break;
                    case "--out": directory = Path.GetFullPath(args[i]); break;
                    default: throw new ArgumentException($"Unknown option {option}.");
                }
            }
            if (presses < 1) throw new ArgumentException("The press count must be positive.");
            AudioScheduling[] modes = mode switch
            {
                "legacy" => new[] { AudioScheduling.Legacy },
                "deviceclock" => new[] { AudioScheduling.DeviceClock },
                "both" => new[] { AudioScheduling.Legacy, AudioScheduling.DeviceClock },
                _ => throw new ArgumentException("The mode must be legacy, deviceclock, or both.")
            };
            Directory.CreateDirectory(directory);
            AppSettings settings = File.Exists(SettingsStore.DefaultPath)
                ? JsonSerializer.Deserialize<AppSettings>(File.ReadAllText(SettingsStore.DefaultPath), SettingsStore.Options)?.Normalize() ?? new()
                : new();
            Win32.InitTiming();
            timing = true;
            Win32.SetDrift(settings.ClockDrift);
            summary.AppendLine("Loopback measures when audio leaves the audio engine.");
            summary.AppendLine("It excludes the constant DAC and earbud latency.");
            summary.AppendLine("Close other audio sources before testing. Endpoint volume and audio effects can affect onset detection.");
            summary.AppendLine("An onset is the first sample above 25% of the loaded clip's peak after 20 ms of near-silence.");
            summary.AppendLine("The hit rate assumes a perfectly tuned offset and a uniformly random game-frame phase.");
            summary.AppendLine("Missing onsets have empty onset and lateness fields in the CSV. Statistics use detected beeps.");
            foreach (AudioScheduling scheduling in modes)
            {
                if (!RunMode(scheduling, presses, directory, settings, summary)) result = 1;
            }
        }
        catch (Exception e)
        {
            summary.AppendLine($"The self-test failed. {e.GetType().Name}: {e.Message}");
            result = 1;
        }
        finally
        {
            if (timing) Win32.EndTiming();
        }
        try
        {
            Directory.CreateDirectory(directory);
            File.WriteAllText(Path.Combine(directory, "beep-selftest-summary.txt"), summary.ToString());
        }
        catch (Exception e)
        {
            summary.AppendLine($"The summary could not be written. {e.Message}");
            result = 1;
        }
        Console.Write(summary.ToString());
        return result;
    }

    private static bool RunMode(AudioScheduling mode, int presses, string directory, AppSettings settings, StringBuilder summary)
    {
        var logs = new ConcurrentQueue<string>();
        using var player = new BeepPlayer(logs.Enqueue) { Sound = settings.BeepSound, Volume = settings.Volume };
        player.Configure(AudioOutput.Wasapi, settings.AudioPeriodMs, mode);
        WasapiOutput output = player.Wasapi ?? throw new InvalidOperationException("The self-test requires WASAPI output.");
        using var capture = WasapiOutput.LoopbackCapture.Open();
        double threshold = player.Clip.Max(sample => Math.Abs((int)sample)) / 32768.0 * 0.25;
        if (threshold <= 0) throw new InvalidOperationException("The beep clip is silent at the saved volume.");
        var measurements = new List<Measurement>();
        double[] offsets = { 1000, 1500, 2000, 2500, 3000 };
        WaitUntil(Win32.GetTime() + 100, capture, threshold);
        for (int press = 1; press <= presses; press++)
        {
            Thread.Sleep(Random.Shared.Next(61));
            double pressMs = Win32.GetTime();
            player.QueueBeeps(pressMs, offsets);
            WaitUntil(pressMs + offsets[^1] + player.ClipDurationMs + 300, capture, threshold);
            if (!ReferenceEquals(output, player.Wasapi) || output.NeedsReopen)
                throw new InvalidOperationException("The output changed during the self-test. Run it again with a stable device.");
            for (int beep = 0; beep < offsets.Length; beep++)
            {
                double target = pressMs + offsets[beep];
                double? onset = capture.Onsets.Where(time => time >= target - 100 && time < target + 250)
                    .OrderBy(time => Math.Abs(time - target)).Select(time => (double?)time).FirstOrDefault();
                measurements.Add(new Measurement(press, beep + 1, target, onset));
                if (onset is double detected) capture.Onsets.Remove(detected);
            }
            capture.Onsets.Clear();
        }
        string modeName = mode.ToString().ToLowerInvariant();
        string file = Path.Combine(directory, $"beep-selftest-{modeName}-{DateTime.Now:yyyyMMdd-HHmmss-fff}.csv");
        using (var writer = new StreamWriter(file))
        {
            writer.WriteLine("press,beep index,target ms,onset ms,lateness ms");
            foreach (Measurement row in measurements)
                writer.WriteLine($"{row.Press},{row.Beep},{Number(row.Target)},{Number(row.Onset)},{Number(row.Lateness)}");
        }
        int missing = measurements.Count(row => row.Onset == null);
        summary.AppendLine();
        summary.AppendLine($"Mode {mode} used {output.DeviceName}.");
        summary.AppendLine($"The output mix format was {output.FormatDescription}. The engine period was {Number(output.EnginePeriodMs)} ms.");
        summary.AppendLine($"Loopback captured {capture.DeviceName} at {capture.FormatDescription}.");
        summary.AppendLine($"The clip was {settings.BeepSound} at {settings.Volume}% volume. The threshold was {Number(threshold)}.");
        summary.AppendLine($"The test did not detect {missing} of {measurements.Count} beeps.");
        summary.AppendLine($"Capture reported {capture.InvalidPackets} packets with invalid timestamps and {capture.Discontinuities} discontinuities.");
        AppendStats(summary, "Final beeps", measurements.Where(row => row.Beep == 5));
        AppendStats(summary, "All beeps", measurements);
        summary.AppendLine($"Measurements were written to {file}.");
        foreach (string line in logs) summary.AppendLine(line);
        return measurements.Count - missing >= measurements.Count * 0.9;
    }

    private static void WaitUntil(double deadline, WasapiOutput.LoopbackCapture capture, double threshold)
    {
        while (Win32.GetTime() < deadline)
        {
            capture.Drain(threshold);
            Thread.Sleep(2);
        }
        capture.Drain(threshold);
    }

    private static void AppendStats(StringBuilder summary, string name, IEnumerable<Measurement> rows)
    {
        double[] errors = rows.Where(row => row.Lateness.HasValue).Select(row => row.Lateness!.Value).Order().ToArray();
        if (errors.Length == 0)
        {
            summary.AppendLine($"{name} had no detected onsets.");
            return;
        }
        double median = Percentile(errors, 0.5);
        double hitRate = errors.Average(error => Math.Max(0, 1 - Math.Abs(error - median) / 16.743));
        summary.AppendLine(name);
        summary.AppendLine("Count | Min ms | Mean ms | P5 ms | P95 ms | Max ms | Spread ms | Estimated hit rate");
        summary.AppendLine(FormattableString.Invariant(
            $"{errors.Length} | {errors[0]:F3} | {errors.Average():F3} | {Percentile(errors, 0.05):F3} | {Percentile(errors, 0.95):F3} | {errors[^1]:F3} | {errors[^1] - errors[0]:F3} | {hitRate:P2}"));
    }

    private static double Percentile(double[] sorted, double fraction)
    {
        double at = (sorted.Length - 1) * fraction;
        int lower = (int)at;
        return sorted[lower] + (sorted[Math.Min(lower + 1, sorted.Length - 1)] - sorted[lower]) * (at - lower);
    }

    private static string Number(double? value) => value?.ToString("F6", CultureInfo.InvariantCulture) ?? "";
}

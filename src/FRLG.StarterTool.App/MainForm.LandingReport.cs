using System.Globalization;
using FRLG.StarterTool.Core.Timing;

namespace FRLG.StarterTool.App;

public partial class MainForm
{
    private readonly Dictionary<string, LandingCorrection> _corrections = new(StringComparer.Ordinal);

    private const string StarterCorrectionKey = "starter";

    private LandingCorrection CorrectionFor(string key, int delayMs, int offsetMs, double fps)
    {
        if (!_corrections.TryGetValue(key, out LandingCorrection? correction))
        {
            correction = new LandingCorrection(delayMs, offsetMs, fps);
            _corrections[key] = correction;
        }
        return correction;
    }

    internal void ObserveEncounterAttempt(EncounterRun run, EncounterRun.Target target)
    {
        if (target.DeltaMs is not { } deltaMs) return;

        int delayNow = run.Route.DelayMs;
        int offsetNow = run.Route.OffsetMs ?? StarterTool.VariableOffset?.OffsetMs ?? 0;
        CorrectionFor("manip\n" + run.Route.Name + "\n" + target.Press.Name, delayNow, offsetNow, run.Fps)
            .ObserveAttempt(deltaMs, offsetNow);
    }

    private void ReportLandedFrame()
    {
        if (!int.TryParse(TextBoxLandedFrame.Text.Trim(), NumberStyles.Integer,
                CultureInfo.InvariantCulture, out int frame) || frame < 0)
        {
            return;
        }

        bool reported = !_encounterGrid && _landingTarget != null
            ? ReportStarterLanding(frame)
            : ReportTitlePress(frame);
        if (reported) TextBoxLandedFrame.Text = "";
    }

    private bool ReportStarterLanding(int frame)
    {
        if (_landingTarget is not { } targetFrame || _landingReport is not { } landing) return false;
        if (StarterTool.VariableOffset is not { } timer) return false;

        ContextSession.Log(string.Format(CultureInfo.InvariantCulture,
            "landing reported: starter press landed on frame {0} (tool said {1})",
            frame,
            _landingFrame is { } landed ? landed.ToString(CultureInfo.InvariantCulture) : "nothing"));

        int delayNow = timer.DelayOffsetMs;
        int offsetNow = timer.OffsetMs;
        LandingCorrection correction = CorrectionFor(
            StarterCorrectionKey, delayNow, offsetNow, landing.Fps);
        correction.Add(new LandingReport(
            targetFrame, frame, _landingFrame, landing.DeltaMs, delayNow, offsetNow, landing.Step));
        ShowAdvice(correction, $"Landed {frame}", delayNow, offsetNow);
        return true;
    }

    private bool ReportTitlePress(int frame)
    {
        if (StarterTool.VariableOffset?.EncounterReport is not { } run || run.Targets.Count == 0) return false;

        EncounterRun.Target target = run.TitleTarget ?? run.Targets[^1];
        target.Report(frame);
        StarterTool.VariableOffset.RefreshEncounterRows();
        ContextSession.Log(string.Format(CultureInfo.InvariantCulture,
            "landing reported: route \"{0}\" {1} press landed on frame {2} (tool said {3})",
            run.Route.Name, target.Press.Name, frame,
            target.LandedFrame is { } landed ? landed.ToString(CultureInfo.InvariantCulture) : "nothing"));

        int delayNow = run.Route.DelayMs;
        int offsetNow = run.Route.OffsetMs ?? StarterTool.VariableOffset?.OffsetMs ?? 0;
        if (target.AsReport(delayNow, offsetNow) is not { } report) return true;

        LandingCorrection correction = CorrectionFor(
            "manip\n" + run.Route.Name + "\n" + target.Press.Name, delayNow, offsetNow, run.Fps);
        correction.Add(report);
        ShowAdvice(correction, $"{target.Press.Name} landed {frame}", delayNow, offsetNow);
        return true;
    }

    private void ShowAdvice(LandingCorrection correction, string what, int delayNow, int offsetNow)
    {
        string reports = $"{correction.Count} report{(correction.Count == 1 ? "" : "s")}";
        string advice = correction.Advise(delayNow, offsetNow) is { } shift && shift.Any
            ? AdviceText(shift, delayNow, offsetNow)
            : "nothing to change yet";

        Label readout = ActiveLandingLabel;
        readout.ForeColor = Theme.DimText;
        readout.Text = $"{what} · {reports} - {advice}";
        ContextSession.Log("landing advice: " + readout.Text);
    }

    private static string AdviceText(in LandingAdvice advice, int delayNow, int offsetNow)
    {
        var parts = new List<string>();
        if (advice.DelayShiftMs != 0)
        {
            parts.Add($"Delay {delayNow} → {delayNow + advice.DelayShiftMs} ms");
        }
        if (advice.OffsetShiftMs != 0)
        {
            parts.Add($"Offset {offsetNow} → {offsetNow + advice.OffsetShiftMs} ms");
        }
        string text = "suggest " + string.Join(", ", parts);
        if (advice.Scattered)
        {
            text += string.Format(CultureInfo.InvariantCulture,
                " (presses spread ±{0:F1} f)", advice.SpreadFrames);
        }
        return text;
    }
}

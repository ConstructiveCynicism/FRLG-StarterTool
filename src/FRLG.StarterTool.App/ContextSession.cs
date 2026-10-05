using System.Globalization;
using System.Text;
using FRLG.StarterTool.Core.Encounters;
using FRLG.StarterTool.Core.Npc;
using FRLG.StarterTool.Core.Settings;
using FRLG.StarterTool.Core.Tips;

namespace FRLG.StarterTool.App;

public enum ContextStage
{
    Idle,

    Fence,

    Lab,
}

public sealed class ContextSession
{
    private double? _exitMs;
    private double? _oakMs;
    private double? _labMs;

    private double? _ballMs;

    private bool _adapter;

    private int? _ballTarget;
    private bool _ballNeedWalking;

    private int _missOakFrame;

    private int _seed;

    private int _houseAdvances;

    private bool _armed;

    private double? _lastAnchorMs;

    private bool _labCued;

    private bool _fenceUnfinished;

    private string? _fenceSalvage;

    private int _salvageStage = -1;

    private List<Task<FenceTracker>>? _salvageBuilds;

    private IReadOnlyList<SalvageStage>? _salvageStages;

    private Func<int[], FenceTracker>? _salvageBuilder;

    private bool _tracking;

    private bool _hit;

    private bool _unpressed;

    private bool _missed;

    private int? _missCorrection;

    private FenceCandidate? _missFence;

    private MissedLab? _missLab;

    private bool _missAutomatic;

    private HiddenMoves? _missEastward;

    private int? _hitCountdownFrame;

    private string _tip = "";

    private double? _closeDeltaMs;

    private double _closeHitChance;

    private readonly Random _random = new();

    private int _closeGeneration;

    public event EventHandler? Changed;

    public bool Tracking
    {
        get => _tracking;
        set
        {
            if (_tracking == value) return;

            _tracking = value;
            Reset();
        }
    }

    public FenceTracker? Tracker { get; private set; }

    public LabTracker? Lab { get; private set; }

    public RouteAnchor? LastAnchor { get; private set; }

    public int HouseAdvances => _houseAdvances;

    private int RouteAdvances =>
        _houseAdvances + RouteAdvancesOnly
        + (_adapter ? RouteTimeline.AdapterTrainerCardAdvances(SaveButtons) : 0);

    private int RouteAdvancesOnly =>
        RouteTimeline.RivalNameAdvances(_adapter, StarterTool.Settings?.NpcNameRival ?? true);

    private static TitleButtonMode SaveButtons =>
        StarterTool.Settings?.EncounterButtons == "la" ? TitleButtonMode.LEqualsA : TitleButtonMode.Help;

    private static int TakeHouseAdvances() =>
        -(StarterTool.VariableOffset?.TakeFrameAdjustment() ?? 0);

    public bool ReportPcVisit() => Declare("PC Potion", RouteTimeline.PcVisitAdvances(Adapter), anyStage: true);

    public bool ReportDoubleHelp()
    {
        if (!Adapter)
        {
            Log("Double Help declared on the plain build, where it costs nothing - ignored");
            return false;
        }
        return Declare("Double Help", RouteTimeline.DoubleHelpAdvances(Adapter), anyStage: true);
    }

    private int _preLabAdvances;

    private int _postLabAdvances;

    public int LateDeclaredAdvances => _preLabAdvances + _postLabAdvances;

    private FenceCandidate ForLab(FenceCandidate fence) =>
        _preLabAdvances == 0 ? fence : fence with { AdvancesBeforeLabLoad = fence.AdvancesBeforeLabLoad + _preLabAdvances };

    private int Reach(int frame) => Lab?.ReachTarget(frame) ?? frame - _postLabAdvances;

    public bool NudgesAreAdvances =>
        _adapter && _tracking && _armed && Stage == ContextStage.Lab && _labMs != null;

    public bool NudgeAdvances(int frames)
    {
        if (!NudgesAreAdvances || frames == 0) return false;
        return DeclareLate(frames > 0 ? "+ nudge" : "- nudge", -frames);
    }

    private bool Declare(string what, int shift, bool anyStage = false)
    {
        if (anyStage && _tracking && _armed && _oakMs != null)
        {
            return DeclareLate(what, shift);
        }

        if (!_tracking || !_armed || _missed || _oakMs != null
            || StarterTool.VariableOffset is not { } timer)
        {
            Log(string.Format(CultureInfo.InvariantCulture,
                "{0} declared too late to spend ({1:+#;-#;0} advances) - ignored", what, shift));
            return false;
        }

        timer.ChangeAudio(-shift);

        Log(string.Format(CultureInfo.InvariantCulture,
            "{0} declared: {1:+#;-#;0} advances ({2})", what, shift, Adapter ? "adapter" : "plain"));
        return true;
    }

    private bool DeclareLate(string what, int shift)
    {
        if (_labMs == null)
        {
            _preLabAdvances += shift;
            Log(string.Format(CultureInfo.InvariantCulture,
                "{0} declared after the Oak anchor: {1:+#;-#;0} advances on the lab's stream ({2:+#;-#;0} in all)",
                what, shift, _preLabAdvances));
        }
        else
        {
            _postLabAdvances += shift;
            Lab?.SetManualAdvances(_postLabAdvances);
            Log(string.Format(CultureInfo.InvariantCulture,
                "{0} declared after the lab anchor{1}: {2:+#;-#;0} advances on the count ({3:+#;-#;0} in all)",
                what, _ballMs == null ? " (taken as after the ball)" : "", shift, _postLabAdvances));
        }

        StarterTool.VariableOffset?.RefreshFrameBox();
        Retarget();
        Changed?.Invoke(this, EventArgs.Empty);
        return true;
    }

    public int AnchorCount => (_exitMs == null ? 0 : 1) + (_oakMs == null ? 0 : 1) + (_labMs == null ? 0 : 1)
        + (_ballMs == null ? 0 : 1);

    public int AnchorTotal => Adapter ? 4 : 3;

    public bool Adapter => _armed ? _adapter : StarterTool.Settings?.NpcAdapter ?? false;

    public RouteAnchor? NextAnchor =>
        !_tracking || !_armed || _missed || !StarterTool.IsTimerRunning ? null
        : _exitMs == null ? RouteAnchor.ExitHouse
        : _oakMs == null ? RouteAnchor.CloseOakText
        : _labMs == null ? RouteAnchor.CloseLabText
        : _adapter && _ballMs == null && Lab != null && !HitConfirmed ? RouteAnchor.PressBall
        : null;

    public int? BallAnchorFrame => _ballMs is { } ms
        ? FrameWindow.LikelyFrame(ms, StarterTool.VariableOffset?.SelectedFps ?? 60.0)
        : null;

    public int? OakAnchorFrame => _oakMs is { } ms
        ? FrameWindow.LikelyFrame(ms, StarterTool.VariableOffset?.SelectedFps ?? 60.0)
        : null;

    public IReadOnlyList<int?> AnchorFrames
    {
        get
        {
            double fps = StarterTool.VariableOffset?.SelectedFps ?? 60.0;
            return new[]
            {
                _exitMs is { } exit ? FrameWindow.LikelyFrame(exit, fps) : (int?)null,
                _oakMs is { } oak ? FrameWindow.LikelyFrame(oak, fps) : (int?)null,
                _labMs is { } lab ? FrameWindow.LikelyFrame(lab, fps) : (int?)null,
            };
        }
    }

    private const double CueTailMs = 250.0;

    private const double AnchorLockoutMs = 500.0;

    public double? CuePressMs
    {
        get
        {
            if (!_tracking || !_armed || _missed || _labMs != null
                || !StarterTool.IsTimerRunning) return null;
            if (StarterTool.Settings is not { NpcCuedLabPress: true } settings) return null;
            if (_oakMs is not { } oak) return null;

            double fps = StarterTool.VariableOffset?.SelectedFps ?? 60.0;
            return oak + (RouteTimeline.OakTextToLabLoadFrames + RouteTimeline.LabTextFloorFrames
                + settings.NpcCuedLabPressOffsetFrames) * 1000.0 / fps;
        }
    }

    public bool FireCue(double elapsedMs)
    {
        if (CuePressMs is not { } stamp) return false;
        if (elapsedMs < stamp + CueTailMs) return false;

        _labCued = true;
        if (MarkNextAnchor(StarterTool.TimerStart + stamp)) return true;

        _labCued = false;
        return false;
    }

    public ContextStage Stage => AnchorCount switch
    {
        >= 3 => ContextStage.Lab,
        2 => ContextStage.Fence,
        _ => ContextStage.Idle,
    };

    public void Reset()
    {
        _exitMs = _oakMs = _labMs = _ballMs = null;
        _ballLateSteps = 0;
        _preLabAdvances = _postLabAdvances = 0;
        StarterTool.VariableOffset?.RefreshFrameBox();
        _ballTarget = null;
        _lastAnchorMs = null;
        _adapter = false;
        _missOakFrame = 0;
        LastAnchor = null;
        Tracker = null;
        Lab = null;
        _seed = 0;
        _houseAdvances = 0;
        _armed = false;
        _labCued = false;
        _fenceUnfinished = false;
        _fenceSalvage = null;
        _salvageStage = -1;
        _salvageBuilds = null;
        _salvageStages = null;
        _salvageBuilder = null;
        _adviceLogged = null;
        _hit = false;
        _unpressed = false;
        _missed = false;
        _missAutomatic = false;
        _missCorrection = null;
        _missFence = null;
        _missLab = null;
        _missEastward = null;
        _hitCountdownFrame = null;
        _tip = "";
        _closeDeltaMs = null;
        _closeHitChance = 0.0;
        _closeGeneration++;
        Changed?.Invoke(this, EventArgs.Empty);
    }

    public void Start()
    {
        Reset();
        _armed = _tracking;

        _adapter = _armed && (StarterTool.Settings?.NpcAdapter ?? false);

        RunLog.StartRun();

        Log(_armed ? "--- run started ---" : "--- run started, tracker off ---");
        LogTolerances();
        Changed?.Invoke(this, EventArgs.Empty);
    }

    private static void LogTolerances()
    {
        AppSettings? settings = StarterTool.Settings;

        double context = settings?.NpcContextWindowMs ?? 0.0;
        bool cued = settings?.NpcCuedLabPress ?? false;

        Log(string.Format(CultureInfo.InvariantCulture,
            "  context window {0:0.###} ms, lab cue {1}{2}", context,
            cued
                ? string.Format(CultureInfo.InvariantCulture, "on: {0:+#;-#;+0} frames, window {1:0.###} ms",
                    settings?.NpcCuedLabPressOffsetFrames ?? AppSettings.DefaultCuedLabPressOffsetFrames,
                    settings?.NpcCuedPressWindowMs ?? AppSettings.DefaultCuedPressWindowMs)
                : "off",
            settings?.NpcAdapter == true
                ? (SaveButtons == TitleButtonMode.Help ? ", wireless adapter on (HELP save" : ", wireless adapter on (L=A save")
                    + ((StarterTool.Settings?.NpcNameRival ?? true) ? ")" : ", preset rival name)")
                : ""));
    }

    public bool MarkNextAnchor(double pressTimeMs)
    {
        if (!_tracking || !_armed || _missed || !StarterTool.IsTimerRunning) return false;

        double elapsedMs = pressTimeMs - StarterTool.TimerStart;

        if (_lastAnchorMs is { } lastAnchor && elapsedMs - lastAnchor < AnchorLockoutMs)
        {
            Log(string.Format(CultureInfo.InvariantCulture,
                "anchor press ignored at {0:F1} ms - {1:F0} ms after the previous anchor",
                elapsedMs, elapsedMs - lastAnchor));
            return true;
        }

        string? fenceNote = null;

        if (_labMs != null)
        {
            if (NextAnchor != RouteAnchor.PressBall) return false;

            double walkFps = StarterTool.VariableOffset?.SelectedFps ?? 60.0;
            int window = FrameWindow.LikelyFrame(elapsedMs, walkFps) - FrameWindow.LikelyFrame(_labMs.Value, walkFps);
            if (window < RouteTimeline.LabObservableFrames)
            {
                int back = RouteTimeline.LabObservableFrames - window + 1;
                _labMs -= back * 1000.0 / walkFps;
                Log(string.Format(CultureInfo.InvariantCulture,
                    "ball press {0} frames after the lab press, under any walk - lab anchor taken {1} frames earlier",
                    window, back));
            }

            _ballMs = elapsedMs;
            _ballLateSteps = 0;
            _ballTarget = null;
            _lastAnchorMs = elapsedMs;
            LastAnchor = RouteAnchor.PressBall;
            BuildLab(Lab?.Lateness ?? LabLateness.Fast);

            double ballFps = StarterTool.VariableOffset?.SelectedFps ?? 60.0;
            Log(string.Format(CultureInfo.InvariantCulture,
                "anchor ball at {0:F1} ms, frame {1} - window {2} frames after the lab press",
                elapsedMs, FrameWindow.LikelyFrame(elapsedMs, ballFps),
                FrameWindow.LikelyFrame(elapsedMs, ballFps) - FrameWindow.LikelyFrame(_labMs.Value, ballFps)));
            LogLabField();
            Retarget();
            Changed?.Invoke(this, EventArgs.Empty);
            return true;
        }

        if (_exitMs == null)
        {
            _exitMs = elapsedMs;
            LastAnchor = RouteAnchor.ExitHouse;
        }
        else if (_oakMs == null)
        {
            _oakMs = elapsedMs;
            LastAnchor = RouteAnchor.CloseOakText;

            _houseAdvances = TakeHouseAdvances();

            StarterTool.MainForm.LockTrainerId();

            _seed = StarterTool.MainForm.TrackerSeed;
            BuildFence();

            if (CuePressMs != null) Retarget();
        }
        else
        {
            _labMs = elapsedMs;
            LastAnchor = RouteAnchor.CloseLabText;

            DropLabEdgeTaps(elapsedMs);

            fenceNote = AssumeFenceFinished();
            BuildLab();
        }

        _lastAnchorMs = elapsedMs;

        double fps = StarterTool.VariableOffset?.SelectedFps ?? 60.0;
        Log(string.Format(CultureInfo.InvariantCulture, "anchor {0} at {1:F1} ms, frame {2}{3}{4}{5}",
            AnchorName(LastAnchor.Value), elapsedMs, FrameWindow.LikelyFrame(elapsedMs, fps),
            LastAnchor == RouteAnchor.CloseOakText && _houseAdvances != 0
                ? string.Format(CultureInfo.InvariantCulture, ", {0:+#;-#;0} manual", _houseAdvances)
                : "",
            LastAnchor == RouteAnchor.CloseLabText && _labCued ? ", cued" : "",
            LastAnchor == RouteAnchor.CloseLabText && _adapter && Advice() is { } advice
                ? " - " + advice.Text
                : ""));
        if (fenceNote != null) Log(fenceNote);
        if (LastAnchor == RouteAnchor.CloseOakText) LogFenceField();

        if (LastAnchor == RouteAnchor.CloseLabText)
        {
            LogLabField();
            Retarget();
        }

        Changed?.Invoke(this, EventArgs.Empty);
        return true;
    }

    private const double LabEdgeTapMs = 50.0;

    private const double FenceLateReportGraceMs = 1000.0;

    private void DropLabEdgeTaps(double labMs)
    {
        if (Tracker is not { } tracker) return;

        var kept = tracker.Inputs.Where(t => !(labMs - t.ElapsedMs is >= 0.0 and < LabEdgeTapMs)).ToList();
        if (kept.Count == tracker.Inputs.Count) return;

        foreach (FenceInput dropped in tracker.Inputs.Except(kept))
        {
            Log(string.Format(CultureInfo.InvariantCulture,
                "tap {0} at {1:F1} ms dropped - {2:F0} ms before the lab anchor, not the fence guy",
                Directions.Letter(dropped.Direction), dropped.ElapsedMs, labMs - dropped.ElapsedMs));
        }

        tracker.Observe(kept, tracker.Complete);
    }

    public void TimerStopped()
    {
        if (!_tracking || !_armed) return;

        Changed?.Invoke(this, EventArgs.Empty);
    }

    public bool Tap(Direction direction, double pressTimeMs)
    {
        if (Stage != ContextStage.Fence || Tracker == null || _missed
            || direction == Direction.None || !StarterTool.IsTimerRunning) return false;

        double elapsedMs = pressTimeMs - StarterTool.TimerStart;
        double fps0 = StarterTool.VariableOffset?.SelectedFps ?? 60.0;

        if (Tracker.All.Count > 0
            && elapsedMs < Tracker.All.Min(c => c.LeadWalkStartFrame) * 1000.0 / fps0)
        {
            Log(string.Format(CultureInfo.InvariantCulture,
                "tap {0} at {1:F1} ms, frame {2} - ignored, before the fence guy respawns",
                Directions.Letter(direction), elapsedMs, FrameWindow.LikelyFrame(elapsedMs, fps0)));
            return true;
        }

        if (Tracker.All.Count > 0)
        {
            double closedMs = Tracker.All.Max(c => c.LeadWalkStartFrame + c.Motion.Count) * 1000.0 / fps0;
            if (elapsedMs > closedMs + FenceLateReportGraceMs)
            {
                Log(string.Format(CultureInfo.InvariantCulture,
                    "tap {0} at {1:F1} ms, frame {2} - ignored, {3:F1} s after the fence guy's window closed",
                    Directions.Letter(direction), elapsedMs, FrameWindow.LikelyFrame(elapsedMs, fps0),
                    (elapsedMs - closedMs) / 1000.0));
                return true;
            }
        }

        int alive = Tracker.Tap(direction, elapsedMs);

        string? salvaged = null;
        if (Tracker.LastTapRefused)
        {
            salvaged = SalvageFence(direction, elapsedMs);
            if (salvaged != null) alive = Tracker.Alive.Count;
        }

        double fps = StarterTool.VariableOffset?.SelectedFps ?? 60.0;
        Log(string.Format(CultureInfo.InvariantCulture, "tap {0} at {1:F1} ms, frame {2} - {3}",
            Directions.Letter(direction), elapsedMs, FrameWindow.LikelyFrame(elapsedMs, fps),
            salvaged != null
                ? $"fitted nothing, salvaged on {salvaged} (shifts {SurvivingShifts}) - {alive} left"
                : Tracker.LastTapRefused
                    ? $"REFUSED, fits nothing, with the stream up to {MaxSalvageShift} off or a PC Potion - {alive} left"
                    : $"{alive} left"));

        Changed?.Invoke(this, EventArgs.Empty);
        return true;
    }

    public bool Undo()
    {
        if (Stage == ContextStage.Lab)
        {
            return Lab is { Lateness: var lateness } && lateness != LabLateness.Fast
                && SetLateness(lateness - 1);
        }

        if (Tracker == null || _missed || Tracker.Inputs.Count == 0) return false;

        Tracker.Undo();
        Changed?.Invoke(this, EventArgs.Empty);
        return true;
    }

    public bool Clear()
    {
        if (Tracker == null || _missed || (Tracker.Inputs.Count == 0 && !Tracker.Complete)) return false;

        _fenceUnfinished = false;

        if (_fenceSalvage != null)
        {
            _fenceSalvage = null;
            _salvageStage = -1;
            BuildFence();
            Log("fence report cleared - field rebuilt on the unshifted stream");
        }

        Tracker.Clear();
        Changed?.Invoke(this, EventArgs.Empty);
        return true;
    }

    public bool SetComplete(bool complete)
    {
        if (Tracker == null || _missed) return false;

        _fenceUnfinished = !complete;

        int left = Tracker.SetComplete(complete, byRunner: true);
        Log(string.Format(CultureInfo.InvariantCulture, "fence guy reported {0} - {1} of {2} left",
            complete ? "finished" : "not finished", left, Tracker.All.Count));
        Changed?.Invoke(this, EventArgs.Empty);
        return true;
    }

    private string? AssumeFenceFinished()
    {
        if (Tracker is not { Complete: false } tracker || _fenceUnfinished) return null;

        int before = tracker.Alive.Count;
        int after = tracker.SetComplete(true);

        if (after == 0 && before > 0)
        {
            tracker.SetComplete(false);
            return "fence guy taken as finished - refused, fits nothing";
        }

        return string.Format(CultureInfo.InvariantCulture,
            "fence guy taken as finished - {0} of {1} left", after, tracker.All.Count);
    }

    public bool Next() => Stage switch
    {
        ContextStage.Lab when _adapter => SetBallLateSteps(_ballLateSteps >= MaxBallLateSteps ? 0 : _ballLateSteps + 1),
        ContextStage.Lab => Lab is { Lateness: var lateness }
            && SetLateness(lateness == LabLateness.VeryLate ? LabLateness.Fast : lateness + 1),
        _ => SetNext(!NextReported),
    };

    public bool NextReported => Stage switch
    {
        ContextStage.Fence => Tracker is { Complete: true },
        ContextStage.Lab => _adapter ? _ballLateSteps > 0 : Lab is { Late: true },
        _ => false,
    };

    public bool SetNext(bool reported) => Stage switch
    {
        ContextStage.Fence => Tracker != null && SetComplete(reported),
        ContextStage.Lab when _adapter => SetBallLateSteps(reported ? Math.Max(1, _ballLateSteps) : 0),
        ContextStage.Lab => SetLateness(reported ? LabLateness.Late : LabLateness.Fast),
        _ => false,
    };

    private int _ballLateSteps;

    public bool BallLate => _ballLateSteps > 0;

    public int BallLateSteps => _ballLateSteps;

    public const int MaxBallLateSteps = 3;

    public bool SetBallLateSteps(int steps)
    {
        steps = Math.Clamp(steps, 0, MaxBallLateSteps);
        if (Stage != ContextStage.Lab || !_adapter || Lab == null || HitConfirmed || _ballLateSteps == steps) return false;

        int before = Lab.All.Count;
        _ballLateSteps = steps;
        RebuildLabCarryingFocus();
        Log(steps > 0
            ? string.Format(CultureInfo.InvariantCulture,
                "late to the ball: {0} visible change{1} after the anchor - {2} boxes (was {3})",
                steps, steps == 1 ? "" : "s", Lab?.All.Count ?? 0, before)
            : string.Format(CultureInfo.InvariantCulture, "late to the ball: taken back - {0} boxes", Lab?.All.Count ?? 0));

        Retarget();
        Changed?.Invoke(this, EventArgs.Empty);
        return true;
    }

    private void RebuildLabCarryingFocus(LabLateness lateness = LabLateness.Fast)
    {
        bool pinned = Lab is { FocusPinned: true };
        int focus = Lab?.FocusedIndex ?? -1;
        LabCandidate? held = pinned ? Lab?.Focused?.Representative : null;

        BuildLab(lateness);

        if (Lab != null && pinned && focus >= 0)
        {
            int found = -1;
            if (held is { } candidate)
            {
                found = Lab.IndexOf(candidate);
                if (found < 0) found = Lab.IndexOfFence(candidate.Fence);
            }

            Lab.FocusedIndex = found >= 0 ? found : focus;
        }
    }

    public bool SetLateness(LabLateness lateness)
    {
        if (Stage != ContextStage.Lab || Lab is not { } lab || lab.Lateness == lateness || _adapter) return false;

        RebuildLabCarryingFocus(lateness);

        Log(string.Format(CultureInfo.InvariantCulture,
            "late to the ball: {0} - window {1} frames, {2} boxes",
            LateName(lateness), LabTracker.Window(lateness), Lab?.All.Count ?? 0));

        Retarget();
        Changed?.Invoke(this, EventArgs.Empty);
        return true;
    }

    private static string LateName(LabLateness lateness) => lateness switch
    {
        LabLateness.VeryLate => "very",
        LabLateness.Late => "yes",
        _ => "no",
    };

    public bool MoveFocus(int delta)
    {
        if (Stage == ContextStage.Lab)
        {
            if (Lab == null) return false;

            Lab.MoveFocus(delta);
            LogFocus();
            Retarget();
            Changed?.Invoke(this, EventArgs.Empty);
            return true;
        }

        if (Tracker == null || _missed) return false;

        Tracker.MoveFocus(delta);
        Changed?.Invoke(this, EventArgs.Empty);
        return true;
    }

    public bool FocusBox(int index)
    {
        if (Stage != ContextStage.Lab || Lab == null || index < 0 || index >= Lab.All.Count) return false;

        Lab.FocusedIndex = index;
        LogFocus();
        Retarget();
        Changed?.Invoke(this, EventArgs.Empty);
        return true;
    }

    private void LogFocus()
    {
        if (Lab?.Focused is not { } box) return;

        Log(string.Format(CultureInfo.InvariantCulture,
            "box {0} [{1}]: {2} candidates, advances {3} at the close{4}",
            Lab.FocusedIndex + 1, box, box.Members.Count,
            box.Representative.AdvancesAtTextClose,
            box.Representative.StreamShift == 0 ? ""
                : string.Format(CultureInfo.InvariantCulture, " (stream {0:+#;-#})",
                    box.Representative.StreamShift)));
    }

    public int? Correction(int targetFrame)
    {
        if (Lab is { } lab)
        {
            NoteBallSide(lab);

            int before = lab.Target;
            if (lab.SetTarget(targetFrame))
            {
                LogBallSide(before, targetFrame);
                Changed?.Invoke(this, EventArgs.Empty);
            }
        }
        return CorrectionAt(targetFrame);
    }

    public int? CorrectionAt(int targetFrame)
    {
        if (Lab is { } lab) return lab.Correction(targetFrame);

        if (_missLab is { } missLab) return missLab.Lab.CountdownFrame(targetFrame) - targetFrame;
        if (_missFence is { } rescued) return rescued.MissedCorrection(_missOakFrame, targetFrame);

        return Adapter && !_missed
            ? RouteTimeline.AdapterPlainFrame(targetFrame - _preLabAdvances - _postLabAdvances) - targetFrame
            : null;
    }

    public int? LandedFrame(int countdownFrame) =>
        Lab?.AdvancesAtCountdownFrame(countdownFrame)
        ?? _missLab?.Lab.AdvancesAtCountdownFrame(countdownFrame)
        ?? (_missFence is { } rescued ? rescued.MissedAdvancesAt(_missOakFrame, countdownFrame)
            : Adapter && !_missed ? RouteTimeline.AdapterPlainAdvances(countdownFrame)
            : null);

    public int StreamStep => Lab?.Focused?.Representative.Rate
        ?? RouteTimeline.AdvancesPerFrame(Adapter);

    public bool Reachable(int landedFrame, int frame) =>
        StreamStep <= 1 || (frame - landedFrame) % StreamStep == 0;

    public bool OffParity(int frame) =>
        _adapter && !HitConfirmed
        && Lab?.Focused?.Representative is { Rate: > 1 } box
        && !box.ParityOk(Reach(frame));

    private void NoteBallSide(LabTracker lab)
    {
        if (_ballTarget != null || !lab.Adapter || lab.BallMeasured || lab.Target <= 0) return;
        if (_labMs is not { } labMs || !StarterTool.IsTimerRunning) return;
        if (lab.Focused?.Representative is not { Rate: > 1 } box) return;

        double fps = StarterTool.VariableOffset?.SelectedFps ?? 60.0;
        double since = (Win32.GetTime() - StarterTool.TimerStart - labMs) * fps / 1000.0;
        if (since <= box.ObservableFrames) return;

        _ballTarget = lab.ReachTarget(lab.Target);
        _ballNeedWalking = !box.ParityOk(lab.ReachTarget(lab.Target), 0);
        Log(string.Format(CultureInfo.InvariantCulture,
            "ball side taken as the instruction for {0} ({1})", lab.Target,
            _ballNeedWalking ? "Lady interrupted" : "Lady left standing"));
    }

    private bool AgainstBall(int frame) => _ballTarget is { } aimed && ((aimed ^ Reach(frame)) & 1) != 0;

    private void LogBallSide(int before, int after)
    {
        if (_ballTarget == null || AgainstBall(before) == AgainstBall(after)) return;

        Log(AgainstBall(after)
            ? string.Format(CultureInfo.InvariantCulture,
                "target {0} is off the ball's parity - assumed the Lady was {1}; field moved to that side of her step",
                after, _ballNeedWalking ? "NOT interrupted" : "interrupted")
            : string.Format(CultureInfo.InvariantCulture,
                "target {0} is back on the ball's parity - the instruction taken as obeyed", after));
    }

    public bool CanMiss => _tracking && _armed && !_hit && !_missed && Stage != ContextStage.Lab;

    public bool Miss(bool automatic = false)
    {
        if (!CanMiss) return false;

        _missed = true;
        _missAutomatic = automatic;

        if (Tracker?.Focused is { } candidate && OakAnchorFrame is { } oakFrame)
        {
            _missFence = candidate;
            _missOakFrame = oakFrame;
            int target = (int)(StarterTool.VariableOffset?.Info.Frame ?? 0u);

            if (_seed != 0) _missLab = LabRun.Missed(_seed, candidate, oakFrame, target);
            _missCorrection = _missLab is { } missLab
                ? missLab.Lab.CountdownFrame(target) - target
                : candidate.MissedCorrection(oakFrame, target);
        }
        else
        {
            _missEastward = Eastward();
        }

        string how = automatic ? "countdown started with anchor" : "missed anchor";

        Log(_missCorrection is { } correction
            ? string.Format(CultureInfo.InvariantCulture,
                "{0} at {1}/{4} - {2} advances at the lab load, correction {3:+#;-#;0} frames",
                how, AnchorCount, _missFence?.TotalAdvances ?? 0, correction, AnchorTotal)
            : string.Format(CultureInfo.InvariantCulture,
                "{0} at {1}/{4} - no field, countdown left uncorrected; {2:+#;-#;0} manual, eastward {3}",
                how, AnchorCount, _houseAdvances,
                _missEastward?.ToString() ?? "not simulated (no exit anchor)", AnchorTotal));

        if (_missLab is { } missed) Log("    scientists guaranteed: " + string.Join("; ", missed.Scientists));

        CloseRun(null, 0.0, 0);

        Retarget();
        Changed?.Invoke(this, EventArgs.Empty);
        return true;
    }

    private HiddenMoves? Eastward()
    {
        if (_exitMs is not { } exitMs) return null;

        int seed = _seed != 0 ? _seed : StarterTool.MainForm.TrackerSeed;
        if (seed == 0) return null;

        if (_houseAdvances == 0)
        {
            _houseAdvances = TakeHouseAdvances();
        }

        double fps = StarterTool.VariableOffset?.SelectedFps ?? 60.0;

        double shiftMs = RouteTimeline.AnchorCorrection(_adapter) * 1000.0 / fps;
        double window = (StarterTool.Settings?.NpcContextWindowMs ?? 0.0) + FenceRun.StartUncertaintyMs;

        int exitFrame = FrameWindow.Candidates(exitMs - shiftMs, fps, window)[0];

        return FenceRun.SimulateEastward(seed, exitFrame, RouteAdvances, SpawnRead.PostVBlank, _adapter);
    }

    public bool RecordHit(int countdownFrame, double deltaMs, double hitChance, int offsetMs)
    {
        if (Stage != ContextStage.Lab || _hit) return false;

        _hit = true;
        _hitCountdownFrame = countdownFrame;
        if (Lab?.Focused is { } box) LogHidden(box.Representative);

        CloseRun(deltaMs, hitChance, offsetMs);

        Changed?.Invoke(this, EventArgs.Empty);
        return true;
    }

    public bool HitTaken => (_hit || _unpressed) && !_missed && Stage == ContextStage.Lab;

    public bool ReopenHit()
    {
        if (!_hit && !_unpressed) return false;

        Log(_hit
            ? "starter press marked missed - landing withdrawn, the field is back for a later frame"
            : "closed run reopened - no starter picked yet, the field is back for a later frame");
        _hit = false;
        _unpressed = false;
        _hitCountdownFrame = null;
        _closeDeltaMs = null;
        _tip = "";

        Changed?.Invoke(this, EventArgs.Empty);
        return true;
    }

    public bool Unpressed()
    {
        if (!_tracking || !_armed || _hit || _unpressed || _missed) return false;
        if (Stage != ContextStage.Lab) return false;

        _unpressed = true;

        Log("landing window closed with no press - account off the focused box");
        if (Lab?.Focused is { } box) LogHidden(box.Representative);

        CloseRun(null, 0.0, 0);

        Changed?.Invoke(this, EventArgs.Empty);
        return true;
    }

    public bool HitConfirmed => ((_hit || _unpressed) && Stage == ContextStage.Lab) || _missed;

    public IReadOnlyList<HiddenMoves> Hidden =>
        _missed ? MissedAccount
        : (_hit || _unpressed) && Lab?.Focused is { } box ? box.Representative.Hidden
        : Array.Empty<HiddenMoves>();

    private IReadOnlyList<HiddenMoves> MissedAccount => new[]
    {
        _missFence?.Hidden ?? _missEastward ?? HiddenMoves.Unknown(NpcId.FatMan),
        HiddenMoves.Unknown(NpcId.Aide),
        _missLab?.Scientists[0] ?? HiddenMoves.Unknown(NpcId.ScientistLeft),
        _missLab?.Scientists[1] ?? HiddenMoves.Unknown(NpcId.ScientistRight),
    };

    public string Tip => _tip;

    public bool TipIsShiny => _tip == RunTip.ShinyTip;

    private void CloseRun(double? deltaMs, double hitChance, int offsetMs)
    {
        AppSettings? settings = StarterTool.Settings;
        if (settings == null) return;

        var attempt = new TipAttempt
        {
            DeltaMs = deltaMs,
            OffsetMs = offsetMs,
            HitChance = hitChance,
            ClosedAt = DateTime.Now,
            Setup = StarterTool.CurrentAudioSetup
        };

        settings.TipAttempts++;
        if (attempt.LikelyHit) settings.TipLikelyHits++;

        foreach (HiddenMoves hidden in Hidden)
        {
            if (hidden.Known) settings.TipHiddenRolls += hidden.Total;
        }

        RunLog.LogAttempt(attempt);

        _closeDeltaMs = deltaMs;
        _closeHitChance = hitChance;

        int generation = _closeGeneration;
        Task.Run(() =>
        {
            IReadOnlyList<TipAttempt> recent = RunLog.RecentAttempts(RunTip.RecentWindow);
            IReadOnlyList<TipAttempt> offsetWindow = RunLog.RecentAttempts(RunTip.OffsetWindow);
            DateTime? lastSeen = RunLog.TrainerIdLastSeen(RunLog.CurrentTrainerId);

            StarterTool.Post(() =>
            {
                if (generation != _closeGeneration) return;

                if (recent.Count == 0 || !SameAttempt(recent[^1], attempt))
                {
                    recent = recent.Append(attempt).TakeLast(RunTip.RecentWindow).ToList();
                }

                _tip = RunTip.Pick(Facts(settings, recent, offsetWindow, lastSeen), _random);
                Log("tip: " + _tip);

                StarterTool.StatServer?.PublishPostRun(BuildPostRunCard());
                Changed?.Invoke(this, EventArgs.Empty);
            });
        });
    }

    private PostRunCard BuildPostRunCard()
    {
        string hit = _hit && _closeDeltaMs is { } delta
            ? string.Format(CultureInfo.InvariantCulture, "{0}  {1:+0;-0;0} ms",
                MainForm.FormatChance(_closeHitChance), delta)
            : "";

        int offScreen = 0;
        bool partial = false;
        foreach (HiddenMoves hidden in Hidden)
        {
            if (hidden.Known) offScreen += hidden.OffScreen;
            else partial = true;
        }

        string anchors = string.Join(" · ", AnchorFrames.Select(frame =>
            frame?.ToString(CultureInfo.InvariantCulture) ?? "-"));

        return new PostRunCard(
            hit,
            offScreen.ToString(CultureInfo.InvariantCulture) + (partial ? "+?" : ""),
            anchors);
    }

    private static bool SameAttempt(TipAttempt read, TipAttempt closed) =>
        read.OffsetMs == closed.OffsetMs
        && read.DeltaMs.HasValue == closed.DeltaMs.HasValue
        && (read.DeltaMs is not { } delta || Math.Abs(delta - closed.DeltaMs!.Value) < 0.05)
        && Math.Abs(read.HitChance - closed.HitChance) < 0.0005;

    private TipFacts Facts(AppSettings settings, IReadOnlyList<TipAttempt> recent,
        IReadOnlyList<TipAttempt> offsetWindow, DateTime? trainerIdLastSeen)
    {
        VariableOffsetTimer? timer = StarterTool.VariableOffset;

        int streak = 0;
        for (int i = recent.Count - 1; i >= 0 && !recent[i].LikelyHit; i--) streak++;

        int likely = 0;
        foreach (TipAttempt attempt in recent)
        {
            if (attempt.LikelyHit) likely++;
        }

        int? suggested = null;
        if (streak >= RunTip.MissStreakTip && timer != null)
        {
            suggested = RunTip.SuggestedOffsetMs(offsetWindow,
                timer.TrainingUsesVisualOffset ? timer.VisualOffsetMs : timer.OffsetMs,
                timer.SelectedFps, StarterTool.CurrentAudioSetup);
        }

        double? sinceLast = recent.Count >= 2 && recent[^2].ClosedAt is { } previous
            ? (DateTime.Now - previous).TotalMinutes
            : null;

        bool rapid = recent.Count >= RunTip.RapidTripleRuns
            && recent[^1].ClosedAt is { } last
            && recent[^RunTip.RapidTripleRuns].ClosedAt is { } first
            && (last - first).TotalMinutes <= RunTip.RapidTripleMinutes;

        return new TipFacts
        {
            BallPressUnseen = _unpressed,
            TrainerUsed = settings.TipTrainerUsed,
            OddsCalculated = settings.TipOddsCalculated,

            FenceStopReported = Tracker is null or { Complete: true },

            CuedLabPress = settings.NpcCuedLabPress,
            ContextWindowMs = settings.NpcContextWindowMs,
            DefaultWindowSize = settings.ZoomPercent == 100,
            DefaultStatBoxColors = settings.StatBoxColorsAreDefault,
            OffsetsShared = timer?.OffsetsShared ?? false,
            MissStreak = streak,
            SuggestedOffsetMs = suggested,
            RecentAttempts = recent.Count,
            RecentLikelyHits = likely,
            LastLikelyHit = recent.Count > 0 && recent[^1].LikelyHit,
            HiddenRolls = settings.TipHiddenRolls,
            Attempts = settings.TipAttempts,
            LikelyHits = settings.TipLikelyHits,

            CaptureOn = settings.StatServerEnabled,
            MinutesSinceLastRun = sinceLast,
            RapidTriple = rapid,
            HitChance = recent.Count > 0 ? recent[^1].HitChance : 0.0,

            TrainerIdLastSeen = trainerIdLastSeen
        };
    }

    private static void LogHidden(LabCandidate candidate)
    {
        foreach (HiddenMoves hidden in candidate.Hidden)
        {
            Log(string.Format(CultureInfo.InvariantCulture,
                "  hidden {0}: {1} off screen, {2} bonks, {3} silent",
                hidden.Npc.Name(), hidden.OffScreen, hidden.Bonks, hidden.SilentTurns));
        }
    }

    private static void Retarget() => StarterTool.VariableOffset?.ApplyContextCorrection();

    public string Summary
    {
        get
        {
            if (!_tracking) return "";

            if (!_armed) return "Waiting for the timer to start.";

            if (_missed)
            {
                string what = _missAutomatic ? "Countdown started · anchor" : "Missed anchor";

                return _missCorrection is { } correction
                    ? string.Format(CultureInfo.InvariantCulture,
                        "{0} {1}/{4} · guessed {2:+#;-#;0} frames from the fence field{3}",
                        what, AnchorCount + 1, correction,
                        ParityNote(_missFence?.CompatibleReads ?? SpawnReadSides.None), AnchorTotal)
                    : string.Format(CultureInfo.InvariantCulture,
                        _missEastward is null
                            ? "{0} {1}/{2} · nothing to guess from - countdown uncorrected"
                            : "{0} {1}/{2} · countdown uncorrected - only his walk to Oak survives",
                        what, AnchorCount + 1, AnchorTotal);
            }

            if ((_hit || _unpressed) && Stage == ContextStage.Lab)
            {
                string box = Lab is { All.Count: > 0 } lab
                    ? string.Format(CultureInfo.InvariantCulture, " · box {0}/{1}",
                        lab.FocusedIndex + 1, lab.All.Count)
                    : "";

                string parity = ParityNote(Lab?.Focused?.CompatibleReads ?? SpawnReadSides.None);

                if (_unpressed) return "Window closed · no landing taken" + box + parity;

                return (_hitCountdownFrame is { } pressed && LandedFrame(pressed) is { } frame
                    ? string.Format(CultureInfo.InvariantCulture, "Hit · frame {0}", frame)
                    : "Hit · frame not anchored") + box + parity;
            }

            if (LastAnchor == null) return "Waiting for the house exit - press Start as you leave.";

            double fps = StarterTool.VariableOffset?.SelectedFps ?? 60.0;
            double elapsed = LastAnchor switch
            {
                RouteAnchor.ExitHouse => _exitMs ?? 0.0,
                RouteAnchor.CloseOakText => _oakMs ?? 0.0,
                RouteAnchor.PressBall => _ballMs ?? 0.0,
                _ => _labMs ?? 0.0
            };

            string line = string.Format(CultureInfo.InvariantCulture,
                "Anchor {0}/{3} · {1}, frame {2}",
                AnchorCount, AnchorName(LastAnchor.Value), FrameWindow.LikelyFrame(elapsed, fps), AnchorTotal);

            if (_houseAdvances != 0)
            {
                line += string.Format(CultureInfo.InvariantCulture, " · {0:+#;-#;0} manual",
                    _houseAdvances);
            }

            if (CuePressMs is { } cue)
            {
                line += string.Format(CultureInfo.InvariantCulture, " · lab press cued, frame {0}",
                    FrameWindow.LikelyFrame(cue, fps));
            }

            if (Stage != ContextStage.Lab) return line;
            if (Lab == null || Lab.All.Count == 0) return line + " · no boxes";

            return string.Format(CultureInfo.InvariantCulture, "{0}{1} · box {2}/{3}{4}",
                line,
                _labCued ? " · cued" : "",
                Lab.FocusedIndex + 1, Lab.All.Count,
                _adapter
                    ? _ballMs != null ? " · ball measured" : " · ball not yet pressed"
                    : Lab.Lateness switch
                    {
                        LabLateness.VeryLate => " · very late to the ball",
                        LabLateness.Late => " · late to the ball",
                        _ => "",
                    });
        }
    }

    private static string ParityNote(SpawnReadSides sides) =>
        SpawnReadSet.Resolved(sides) is { } side ? " · parity " + side : "";

    public string Report
    {
        get
        {
            if (Stage != ContextStage.Fence || Tracker == null) return "";

            IReadOnlyList<int> advances = Tracker.TotalAdvances;
            string totals = advances.Count switch
            {
                0 => "nothing fits",
                1 => $"advances {advances[0]}",
                _ => $"advances {advances[0]}–{advances[^1]}"
            };

            string tapped = Directions.Format(Tracker.Inputs.Select(i => i.Direction));
            if (tapped.Length == 0) tapped = Tracker.Complete ? "nothing" : "nothing yet";
            if (Tracker.Complete) tapped += ", done";

            string line = string.Format(CultureInfo.InvariantCulture,
                "{0} of {1} left · {2}\r\nSeen: {3}",
                Tracker.Alive.Count, Tracker.All.Count, totals, tapped);

            if (_fenceSalvage != null) return line + "\r\nYour taps fit only with " + _fenceSalvage + ".";

            return Tracker.LastTapRefused ? line + "\r\nLast tap fits nothing, however the stream is shifted - ignored." : line;
        }
    }

    internal static void Log(string line) => RunLog.Log(line);

    public void LogStop()
    {
        if (!_armed)
        {
            Log(_tracking ? "--- run stopped, not tracked ---" : "--- run stopped, tracker off ---");
            return;
        }

        string outcome = _hit ? "hit"
            : _missed ? (_missAutomatic ? "missed, given up by the countdown" : "missed")
            : CanMiss ? string.Format(CultureInfo.InvariantCulture,
                "anchor chain left open at {0}/{1}", AnchorCount, AnchorTotal)
            : string.Format(CultureInfo.InvariantCulture, "{0}/{1} anchors", AnchorCount, AnchorTotal);

        Log("--- run stopped: " + outcome + " ---");
    }

    private void LogFenceField()
    {
        if (Tracker == null) return;

        Log(string.Format(CultureInfo.InvariantCulture, "  seed {0}, {1:+#;-#;0} manual, {2} candidates:",
            _seed, _houseAdvances, Tracker.All.Count));
        foreach (FenceCandidate candidate in Tracker.All)
        {
            string events = string.Join(" ", candidate.LeadWalk
                .Select(e => $"{Directions.Letter(e.Direction)}@{e.Frame}"));

            Log(string.Format(CultureInfo.InvariantCulture,
                "    exit {0} oak {1}  respawn {2}  visible {3}  advances {4}  [{5}]{6}",
                candidate.ExitFrame, candidate.OakFrame, candidate.LeadWalkStartFrame,
                candidate.LeadWalkVisibleFrame, candidate.TotalAdvances, events,
                candidate.ParitySuffix));
        }
    }

    private void LogLabField()
    {
        if (Lab == null) return;

        string window = LabTracker.Window(Lab.Lateness).ToString(CultureInfo.InvariantCulture);
        if (Lab.BallMeasured && Lab.All.Count > 0)
        {
            int min = Lab.All.Min(o => o.Representative.ObservableFrames);
            int max = Lab.All.Max(o => o.Representative.ObservableFrames);
            window = (min == max ? min.ToString(CultureInfo.InvariantCulture) : $"{min}-{max}") + " (measured)";
        }

        Log(string.Format(CultureInfo.InvariantCulture,
            "  {0} lab boxes, window {1} frames, press frame {2}:",
            Lab.All.Count, window,
            Lab.All.Count == 0 ? 0 : Lab.All[0].Representative.LabPressFrame));

        for (int i = 0; i < Lab.All.Count; i++)
        {
            LabOption option = Lab.All[i];
            LabCandidate shown = option.Representative;

            string events = string.Join(" ", shown.Observable
                .Select(e => $"{e.Npc.ShortName()}{Directions.Letter(e.Direction)}@{e.Frame}"
                    + (shown.Completes(e) ? "" : "~")));

            Log(string.Format(CultureInfo.InvariantCulture,
                "    {0}{1}  x{2}  lab {3} frozen {4} (cue {5:+#;-#;+0})  advances {6}{7}  [{8}]",
                i == Lab.FocusedIndex ? "* " : "  ", option, option.Members.Count, shown.LabFrame,
                shown.FrozenFrames, shown.FrozenFrames - RouteTimeline.LabTextFloorFrames,
                shown.AdvancesAtTextClose,
                shown.StreamShift == 0 ? ""
                    : string.Format(CultureInfo.InvariantCulture, " (stream {0:+#;-#})",
                        shown.StreamShift),
                events));
        }
    }

    private static string AnchorName(RouteAnchor anchor) => anchor switch
    {
        RouteAnchor.ExitHouse => "house exit",
        RouteAnchor.CloseOakText => "Oak text",
        RouteAnchor.PressBall => "ball",
        _ => "lab text"
    };

    private const int MaxSalvageShift = 12;

    private readonly record struct SalvageStage(int[] Undeclared, string How);

    private IReadOnlyList<SalvageStage> SalvageStages
    {
        get
        {
            int pc = RouteTimeline.PcVisitAdvances(_adapter);
            var stages = new List<SalvageStage>();

            for (int shift = 1; shift <= MaxSalvageShift; shift++)
            {
                stages.Add(new SalvageStage(new[] { shift, -shift },
                    $"the stream {shift} advance{(shift == 1 ? "" : "s")} off"));
            }

            stages.Add(new SalvageStage(new[] { pc }, "an undeclared PC Potion"));

            for (int shift = 1; shift <= MaxSalvageShift; shift++)
            {
                stages.Add(new SalvageStage(new[] { pc + shift, pc - shift },
                    $"an undeclared PC Potion and the stream {shift} advance{(shift == 1 ? "" : "s")} off"));
            }

            return stages;
        }
    }

    private string? SalvageFence(Direction direction, double elapsedMs)
    {
        if (Tracker is not { } original || _exitMs is not { } exit || _oakMs is not { } oak) return null;

        double configured = StarterTool.Settings?.NpcContextWindowMs ?? 0.0;
        var inputs = original.Inputs.Append(new FenceInput(direction, elapsedMs)).ToList();
        IReadOnlyList<SalvageStage> stages = _salvageStages ?? SalvageStages;

        for (int stage = _salvageStage + 1; stage < stages.Count; stage++)
        {
            var clock = System.Diagnostics.Stopwatch.StartNew();
            FenceTracker shifted = SalvageBuild(stage)?.GetAwaiter().GetResult()
                ?? BuildFence(exit, oak, configured, stages[stage].Undeclared);
            if (clock.ElapsedMilliseconds >= 50)
            {
                Log(string.Format(CultureInfo.InvariantCulture,
                    "salvage: waited {1} ms for the field with {0}", stages[stage].How, clock.ElapsedMilliseconds));
            }

            shifted.Clear();
            if (shifted.Observe(inputs, original.Complete) == 0) continue;

            Tracker = shifted;
            _salvageStage = stage;
            string how = stages[stage].How;
            _fenceSalvage = _fenceSalvage == null ? how : _fenceSalvage + ", then " + how;
            return how;
        }

        return null;
    }

    private string SurvivingShifts =>
        Tracker == null
            ? ""
            : string.Join(", ", Tracker.Alive.Select(c => c.UndeclaredAdvances).Distinct().Order()
                .Select(n => n.ToString("+#;-#;0", CultureInfo.InvariantCulture)));

    private void BuildFence()
    {
        if (_exitMs is not { } exit || _oakMs is not { } oak) return;

        double configured = StarterTool.Settings?.NpcContextWindowMs ?? 0.0;
        Tracker = BuildFence(exit, oak, configured, null);
        if (_salvageBuilds == null) PrebuildSalvage(exit, oak);
    }

    private void PrebuildSalvage(double exit, double oak)
    {
        int seed = _seed;
        double fps = StarterTool.VariableOffset?.SelectedFps ?? 60.0;
        double configured = StarterTool.Settings?.NpcContextWindowMs ?? 0.0;
        int manual = _houseAdvances + RouteAdvancesOnly;
        bool adapter = _adapter;
        TitleButtonMode buttons = SaveButtons;

        _salvageStages = SalvageStages;
        _salvageBuilder = undeclared => FenceTracker.Build(seed, exit, oak, fps, configured,
            manual, FenceGuyParity.Both, adapter, buttons, undeclared);
        _salvageBuilds = new List<Task<FenceTracker>>();

        for (int i = 0; i < SalvageLookahead; i++) EnqueueSalvageBuild();
    }

    private const int SalvageLookahead = 3;

    private Task<FenceTracker>? SalvageBuild(int stage)
    {
        if (_salvageBuilds == null) return null;

        while (_salvageBuilds.Count <= stage + SalvageLookahead && EnqueueSalvageBuild()) { }

        return stage < _salvageBuilds.Count ? _salvageBuilds[stage] : null;
    }

    private bool EnqueueSalvageBuild()
    {
        if (_salvageBuilds == null || _salvageStages is not { } stages || _salvageBuilder is not { } build)
            return false;
        if (_salvageBuilds.Count >= stages.Count) return false;

        int[] undeclared = stages[_salvageBuilds.Count].Undeclared;
        _salvageBuilds.Add(_salvageBuilds.Count == 0
            ? Task.Run(() => build(undeclared))
            : _salvageBuilds[^1].ContinueWith(_ => build(undeclared), TaskScheduler.Default));
        return true;
    }

    private FenceTracker BuildFence(double exit, double oak, double contextMs, int[]? undeclared)
    {
        return FenceTracker.Build(
            _seed,
            exit,
            oak,
            StarterTool.VariableOffset?.SelectedFps ?? 60.0,
            contextMs,
            _houseAdvances + RouteAdvancesOnly,
            FenceGuyParity.Both,
            _adapter,
            SaveButtons,
            undeclared);
    }

    private const double LabParentFloor = 0.01;

    private void BuildLab(LabLateness lateness = LabLateness.Fast)
    {
        _ballTarget = null;

        if (Tracker == null || _oakMs is not { } oak || _labMs is not { } lab) return;

        IReadOnlyList<FenceCandidate> carried = Tracker.Alive.Count > 0 ? Tracker.Alive : Tracker.All;
        IReadOnlyList<double>? belief = Tracker.Alive.Count > 0 ? Tracker.Likelihoods : null;

        if (belief != null)
        {
            int top = Tracker.MostLikelyIndex;
            var kept = Enumerable.Range(0, carried.Count)
                .Where(i => i == top || belief[i] >= LabParentFloor)
                .ToList();
            carried = kept.Select(i => carried[i]).ToList();
            belief = kept.Select(i => belief[i]).ToList();
        }

        double fps = StarterTool.VariableOffset?.SelectedFps ?? 60.0;

        if (_adapter)
        {
            Lab = LabTracker.BuildAdapter(_seed, carried.Select(ForLab).ToList(), oak, lab, _ballMs, fps,
                LabContextWindowMs, belief, (int)(StarterTool.VariableOffset?.Info.Frame ?? 0u),
                _postLabAdvances, _ballLateSteps);
            return;
        }

        Lab = LabTracker.Build(
            _seed,
            carried,
            oak,
            lab,
            fps,
            LabContextWindowMs,
            belief,
            lateness);
    }

    public ContextAdvice? Advice()
    {
        if (!_adapter || _seed == 0) return null;

        int target = (int)(StarterTool.VariableOffset?.Info.Frame ?? 0u);
        if (target <= 0) return null;

        if (Lab is { Focused: { } box } lab && box.Representative.Live is { } live)
        {
            (bool needWalking, double labShare, bool unanimous) = LabVerdict(lab, box, target, lab.BallMeasured);

            if (lab.BallMeasured)
            {
                int window = box.Representative.ObservableFrames;
                return Logged(ContextAdvice.AfterBall(!needWalking, live.LadyWalking(window), window, live,
                    unanimous, labShare));
            }

            bool agreed = unanimous;

            if (AgainstBall(target))
            {
                return Logged(ContextAdvice.Assumed(_ballNeedWalking, live,
                    box.Representative.ObservableFrames));
            }

            return Logged(ContextAdvice.InLab(needWalking, live,
                box.Representative.ObservableFrames, agreed, labShare));
        }

        if (Tracker is not { } tracker || tracker.Alive.Count == 0) return null;

        var verdicts = tracker.Alive.Select(c => LabParity.Of(_seed, ForLab(c), Reach(target))).Distinct().ToList();
        bool agree = verdicts.Count == 1;

        if (!agree)
        {
            double fps = StarterTool.VariableOffset?.SelectedFps ?? 60.0;
            bool windowOver = _oakMs is { } oak
                && (Win32.GetTime() - StarterTool.TimerStart - oak) * fps / 1000.0
                    >= RouteTimeline.LeadWalkFatManFreezeFrames;
            if (!tracker.Complete && !windowOver && !RankingEarned(tracker)) return null;
        }

        if (agree) return Logged(ContextAdvice.BeforeLab(verdicts[0], true));

        var weighed = tracker.Alive
            .Select((c, i) => (Parity: LabParity.Of(_seed, ForLab(c), Reach(target)), Weight: tracker.Likelihoods[i]))
            .ToList();
        double wrong = weighed.Where(x => x.Parity.NeedWalking).Sum(x => x.Weight);
        double total = weighed.Sum(x => x.Weight);
        bool side = wrong > total - wrong;
        LabParity said = weighed.Where(x => x.Parity.NeedWalking == side).MaxBy(x => x.Weight).Parity;
        double share = total > 0.0 ? (side ? wrong : total - wrong) / total : 0.5;
        return Logged(ContextAdvice.BeforeLab(said, false, share));
    }

    private static (bool NeedWalking, double Share, bool Unanimous) LabVerdict(LabTracker lab, LabOption focused,
        int target, bool measured = false)
    {
        bool Wrong(LabCandidate m) => measured
            ? !m.ParityOk(lab.ReachTarget(target))
            : !m.ParityOk(lab.ReachTarget(target), 0);

        double wrong = 0.0, total = 0.0;
        for (int i = 0; i < lab.All.Count; i++)
        {
            LabOption option = lab.All[i];
            if (lab.FocusPinned && !ReferenceEquals(option, focused)) continue;

            double weight = lab.FocusPinned ? 1.0 : lab.Likelihoods[i];
            if (weight <= 0.0 || option.Members.Count == 0) continue;

            int wrongMembers = option.Members.Count(Wrong);
            wrong += weight * wrongMembers / option.Members.Count;
            total += weight;
        }

        if (total <= 0.0) return (Wrong(focused.Representative), 1.0, true);

        bool side = wrong > total - wrong;
        double share = (side ? wrong : total - wrong) / total;
        return (side, share, share >= 1.0 - 1e-9);
    }

    private string? _adviceLogged;

    private ContextAdvice Logged(ContextAdvice advice)
    {
        if (_adviceLogged == advice.Text) return advice;

        _adviceLogged = advice.Text;
        Log("advice: " + advice.Text);
        return advice;
    }

    private const int LikelyParityTaps = 2;
    private const int LikelyParityTapsWideContext = 3;

    private const double WideContextMs = 100.0;

    private static bool RankingEarned(FenceTracker tracker) =>
        tracker.Inputs.Count >= (tracker.ContextMs > WideContextMs
            ? LikelyParityTapsWideContext
            : LikelyParityTaps);

    private double LabContextWindowMs => _labCued
        ? StarterTool.Settings?.NpcCuedPressWindowMs ?? AppSettings.DefaultCuedPressWindowMs
        : StarterTool.Settings?.NpcContextWindowMs ?? 0.0;
}

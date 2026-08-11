using System.Globalization;
using System.Text;
using FRLG.StarterTool.Core.Npc;
using FRLG.StarterTool.Core.Settings;

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

    private int _seed;

    private int _houseAdvances;

    private bool _armed;

    private bool _tracking;

    private bool _hit;

    private bool _missed;

    private int? _missCorrection;

    private FenceCandidate? _missFence;

    private bool _missAutomatic;

    private HiddenMoves? _missEastward;

    private int? _hitCountdownFrame;

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

    public int AnchorCount => (_exitMs == null ? 0 : 1) + (_oakMs == null ? 0 : 1) + (_labMs == null ? 0 : 1);

    public RouteAnchor? NextAnchor =>
        !_tracking || !_armed || _missed || !StarterTool.IsTimerRunning ? null
        : _exitMs == null ? RouteAnchor.ExitHouse
        : _oakMs == null ? RouteAnchor.CloseOakText
        : _labMs == null ? RouteAnchor.CloseLabText
        : null;

    public int? OakAnchorFrame => _oakMs is { } ms
        ? FrameWindow.LikelyFrame(ms, StarterTool.VariableOffset?.SelectedFps ?? 60.0)
        : null;

    public ContextStage Stage => AnchorCount switch
    {
        >= 3 => ContextStage.Lab,
        2 => ContextStage.Fence,
        _ => ContextStage.Idle,
    };

    public void Reset()
    {
        _exitMs = _oakMs = _labMs = null;
        LastAnchor = null;
        Tracker = null;
        Lab = null;
        _seed = 0;
        _houseAdvances = 0;
        _armed = false;
        _hit = false;
        _missed = false;
        _missAutomatic = false;
        _missCorrection = null;
        _missFence = null;
        _missEastward = null;
        _hitCountdownFrame = null;
        Changed?.Invoke(this, EventArgs.Empty);
    }

    public void Start()
    {
        Reset();
        _armed = _tracking;
        if (_armed) Log("--- run started ---");
        Changed?.Invoke(this, EventArgs.Empty);
    }

    public bool MarkNextAnchor(double pressTimeMs)
    {
        if (!_tracking || !_armed || _missed || _labMs != null
            || !StarterTool.IsTimerRunning) return false;

        double elapsedMs = pressTimeMs - StarterTool.TimerStart;

        if (_exitMs == null)
        {
            _exitMs = elapsedMs;
            LastAnchor = RouteAnchor.ExitHouse;

            _houseAdvances = StarterTool.VariableOffset?.TakeFrameAdjustment() ?? 0;
        }
        else if (_oakMs == null)
        {
            _oakMs = elapsedMs;
            LastAnchor = RouteAnchor.CloseOakText;

            StarterTool.MainForm.LockTrainerId();

            _seed = StarterTool.MainForm.TrackerSeed;
            BuildFence();
        }
        else
        {
            _labMs = elapsedMs;
            LastAnchor = RouteAnchor.CloseLabText;
            BuildLab();
        }

        double fps = StarterTool.VariableOffset?.SelectedFps ?? 60.0;
        Log(string.Format(CultureInfo.InvariantCulture, "anchor {0} at {1:F1} ms, frame {2}{3}",
            AnchorName(LastAnchor.Value), elapsedMs, FrameWindow.LikelyFrame(elapsedMs, fps),
            LastAnchor == RouteAnchor.ExitHouse && _houseAdvances != 0
                ? string.Format(CultureInfo.InvariantCulture, ", {0:+#;-#;0} manual", _houseAdvances)
                : ""));
        if (LastAnchor == RouteAnchor.CloseOakText) LogFenceField();

        if (LastAnchor == RouteAnchor.CloseLabText)
        {
            LogLabField();
            Retarget();
        }

        Changed?.Invoke(this, EventArgs.Empty);
        return true;
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
        int alive = Tracker.Tap(direction, elapsedMs);

        double fps = StarterTool.VariableOffset?.SelectedFps ?? 60.0;
        Log(string.Format(CultureInfo.InvariantCulture, "tap {0} at {1:F1} ms, frame {2} - {3}",
            Directions.Letter(direction), elapsedMs, FrameWindow.LikelyFrame(elapsedMs, fps),
            Tracker.LastTapRefused
                ? $"REFUSED, fits nothing - {alive} left"
                : $"{alive} left"));

        Changed?.Invoke(this, EventArgs.Empty);
        return true;
    }

    public bool Undo()
    {
        if (Stage == ContextStage.Lab) return SetLate(false);

        if (Tracker == null || _missed || Tracker.Inputs.Count == 0) return false;

        Tracker.Undo();
        Changed?.Invoke(this, EventArgs.Empty);
        return true;
    }

    public bool Clear()
    {
        if (Tracker == null || _missed || (Tracker.Inputs.Count == 0 && !Tracker.Complete)) return false;

        Tracker.Clear();
        Changed?.Invoke(this, EventArgs.Empty);
        return true;
    }

    public bool SetComplete(bool complete)
    {
        if (Tracker == null || _missed) return false;

        Tracker.SetComplete(complete);
        Changed?.Invoke(this, EventArgs.Empty);
        return true;
    }

    public bool Next() => SetNext(!NextReported);

    public bool NextReported => Stage switch
    {
        ContextStage.Fence => Tracker is { Complete: true },
        ContextStage.Lab => Lab is { Late: true },
        _ => false,
    };

    public bool SetNext(bool reported) => Stage switch
    {
        ContextStage.Fence => Tracker != null && SetComplete(reported),
        ContextStage.Lab => SetLate(reported),
        _ => false,
    };

    public bool SetLate(bool late)
    {
        if (Stage != ContextStage.Lab || Lab is not { } lab || lab.Late == late) return false;

        bool pinned = lab.FocusPinned;
        int focus = lab.FocusedIndex;
        LabCandidate? held = pinned ? lab.Focused?.Representative : null;

        BuildLab(late);

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

        Log(string.Format(CultureInfo.InvariantCulture,
            "late to the ball: {0} - window {1} frames, {2} boxes",
            late ? "yes" : "no", LabTracker.Window(late), Lab?.All.Count ?? 0));

        Retarget();
        Changed?.Invoke(this, EventArgs.Empty);
        return true;
    }

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
            "box {0} [{1}]: {2} candidates, advances {3} at the close",
            Lab.FocusedIndex + 1, box, box.Members.Count,
            box.Representative.AdvancesAtTextClose));
    }

    public int? Correction(int targetFrame) => Lab?.Correction(targetFrame) ?? _missCorrection;

    public int? LandedFrame(int countdownFrame) =>
        Lab?.AdvancesAtCountdownFrame(countdownFrame)
        ?? (_missCorrection is { } correction ? countdownFrame - correction : null);

    public bool CanMiss => _tracking && _armed && !_hit && !_missed && Stage != ContextStage.Lab;

    public bool Miss(bool automatic = false)
    {
        if (!CanMiss) return false;

        _missed = true;
        _missAutomatic = automatic;

        if (Tracker?.Focused is { } candidate && OakAnchorFrame is { } oakFrame)
        {
            _missFence = candidate;
            _missCorrection = candidate.MissedCorrection(oakFrame);
        }
        else
        {
            _missEastward = Eastward();
        }

        string how = automatic ? "countdown started with anchor" : "missed anchor";

        Log(_missCorrection is { } correction
            ? string.Format(CultureInfo.InvariantCulture,
                "{0} at {1}/3 - {2} advances at the lab load, correction {3:+#;-#;0} frames",
                how, AnchorCount, _missFence?.TotalAdvances ?? 0, correction)
            : string.Format(CultureInfo.InvariantCulture,
                "{0} at {1}/3 - no field, countdown left uncorrected; eastward {2}",
                how, AnchorCount,
                _missEastward?.ToString() ?? "not simulated (no exit anchor)"));

        Retarget();
        Changed?.Invoke(this, EventArgs.Empty);
        return true;
    }

    private HiddenMoves? Eastward()
    {
        if (_exitMs is not { } exitMs) return null;

        int seed = _seed != 0 ? _seed : StarterTool.MainForm.TrackerSeed;
        if (seed == 0) return null;

        double fps = StarterTool.VariableOffset?.SelectedFps ?? 60.0;

        double shiftMs = RouteTimeline.AnchorCorrectionFrames * 1000.0 / fps;
        double window = (StarterTool.Settings?.NpcContextWindowMs ?? 0.0) + FenceRun.StartUncertaintyMs;

        int exitFrame = FrameWindow.Candidates(exitMs - shiftMs, fps, window)[0];
        return FenceRun.SimulateEastward(seed, exitFrame, _houseAdvances);
    }

    public bool RecordHit(int countdownFrame)
    {
        if (Stage != ContextStage.Lab || _hit) return false;

        _hit = true;
        _hitCountdownFrame = countdownFrame;
        if (Lab?.Focused is { } box) LogHidden(box.Representative);

        Changed?.Invoke(this, EventArgs.Empty);
        return true;
    }

    public bool HitConfirmed => (_hit && Stage == ContextStage.Lab) || _missed;

    public IReadOnlyList<HiddenMoves> Hidden =>
        _missed ? MissedAccount
        : _hit && Lab?.Focused is { } box ? box.Representative.Hidden
        : Array.Empty<HiddenMoves>();

    private IReadOnlyList<HiddenMoves> MissedAccount => new[]
    {
        _missFence?.Hidden ?? _missEastward ?? HiddenMoves.Unknown(NpcId.FatMan),
        HiddenMoves.Unknown(NpcId.Aide),
        HiddenMoves.Unknown(NpcId.ScientistLeft),
        HiddenMoves.Unknown(NpcId.ScientistRight),
    };

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
                        "{0} {1}/3 · guessed {2:+#;-#;0} frames from the fence field",
                        what, AnchorCount + 1, correction)
                    : string.Format(CultureInfo.InvariantCulture,
                        _missEastward is null
                            ? "{0} {1}/3 · nothing to guess from - countdown uncorrected"
                            : "{0} {1}/3 · countdown uncorrected - only his walk to Oak survives",
                        what, AnchorCount + 1);
            }

            if (_hit && Stage == ContextStage.Lab)
            {
                string box = Lab is { All.Count: > 0 } lab
                    ? string.Format(CultureInfo.InvariantCulture, " · box {0}/{1}",
                        lab.FocusedIndex + 1, lab.All.Count)
                    : "";

                return (_hitCountdownFrame is { } pressed && LandedFrame(pressed) is { } frame
                    ? string.Format(CultureInfo.InvariantCulture, "Hit · frame {0}", frame)
                    : "Hit · frame not anchored") + box;
            }

            if (LastAnchor == null) return "Waiting for the house exit - press Start as you leave.";

            double fps = StarterTool.VariableOffset?.SelectedFps ?? 60.0;
            double elapsed = LastAnchor switch
            {
                RouteAnchor.ExitHouse => _exitMs ?? 0.0,
                RouteAnchor.CloseOakText => _oakMs ?? 0.0,
                _ => _labMs ?? 0.0
            };

            string line = string.Format(CultureInfo.InvariantCulture,
                "Anchor {0}/3 · {1}, frame {2}",
                AnchorCount, AnchorName(LastAnchor.Value), FrameWindow.LikelyFrame(elapsed, fps));

            if (_houseAdvances != 0)
            {
                line += string.Format(CultureInfo.InvariantCulture, " · {0:+#;-#;0} manual",
                    _houseAdvances);
            }

            if (Stage != ContextStage.Lab) return line;
            if (Lab == null || Lab.All.Count == 0) return line + " · no boxes";

            return string.Format(CultureInfo.InvariantCulture, "{0} · box {1}/{2}{3}",
                line, Lab.FocusedIndex + 1, Lab.All.Count, Lab.Late ? " · late to the ball" : "");
        }
    }

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

            return Tracker.LastTapRefused ? line + "\r\nLast tap fits nothing - ignored." : line;
        }
    }

    public static string LogPath => Path.Combine(SettingsStore.DefaultDirectory, "fence-log.txt");

    internal static void Log(string line)
    {
        try
        {
            Directory.CreateDirectory(SettingsStore.DefaultDirectory);
            File.AppendAllText(LogPath,
                DateTime.Now.ToString("HH:mm:ss", CultureInfo.InvariantCulture) + "  " + line
                    + Environment.NewLine,
                Encoding.UTF8);
        }
        catch (Exception)
        {
        }
    }

    private void LogFenceField()
    {
        if (Tracker == null) return;

        Log($"  seed {_seed}, +{_houseAdvances} manual, {Tracker.All.Count} candidates:");
        foreach (FenceCandidate candidate in Tracker.All)
        {
            string events = string.Join(" ", candidate.LeadWalk
                .Select(e => $"{Directions.Letter(e.Direction)}@{e.Frame}"));

            Log(string.Format(CultureInfo.InvariantCulture,
                "    exit {0} oak {1}  respawn {2}  visible {3}  advances {4}  [{5}]",
                candidate.ExitFrame, candidate.OakFrame, candidate.LeadWalkStartFrame,
                candidate.LeadWalkVisibleFrame, candidate.TotalAdvances, events));
        }
    }

    private void LogLabField()
    {
        if (Lab == null) return;

        Log(string.Format(CultureInfo.InvariantCulture,
            "  {0} lab boxes, window {1} frames, press frame {2}:",
            Lab.All.Count, LabTracker.Window(Lab.Late),
            Lab.All.Count == 0 ? 0 : Lab.All[0].Representative.LabPressFrame));

        for (int i = 0; i < Lab.All.Count; i++)
        {
            LabOption option = Lab.All[i];
            LabCandidate shown = option.Representative;

            string events = string.Join(" ", shown.Observable
                .Select(e => $"{e.Npc.ShortName()}{Directions.Letter(e.Direction)}@{e.Frame}"
                    + (shown.Completes(e) ? "" : "~")));

            Log(string.Format(CultureInfo.InvariantCulture,
                "    {0}{1}  x{2}  lab {3} frozen {4}  advances {5}  [{6}]",
                i == Lab.FocusedIndex ? "* " : "  ", option, option.Members.Count, shown.LabFrame,
                shown.FrozenFrames, shown.AdvancesAtTextClose, events));
        }
    }

    private static string AnchorName(RouteAnchor anchor) => anchor switch
    {
        RouteAnchor.ExitHouse => "house exit",
        RouteAnchor.CloseOakText => "Oak text",
        _ => "lab text"
    };

    private void BuildFence()
    {
        if (_exitMs is not { } exit || _oakMs is not { } oak) return;

        Tracker = FenceTracker.Build(
            _seed,
            exit,
            oak,
            StarterTool.VariableOffset?.SelectedFps ?? 60.0,
            StarterTool.Settings?.NpcContextWindowMs ?? 0.0,
            _houseAdvances);
    }

    private void BuildLab(bool late = false)
    {
        if (Tracker == null || _oakMs is not { } oak || _labMs is not { } lab) return;

        IReadOnlyList<FenceCandidate> carried = Tracker.Alive.Count > 0 ? Tracker.Alive : Tracker.All;
        IReadOnlyList<double>? belief = Tracker.Alive.Count > 0 ? Tracker.Likelihoods : null;

        Lab = LabTracker.Build(
            _seed,
            carried,
            oak,
            lab,
            StarterTool.VariableOffset?.SelectedFps ?? 60.0,
            StarterTool.Settings?.NpcContextWindowMs ?? 0.0,
            belief,
            late);
    }
}

using System.Globalization;
using FRLG.StarterTool.Core.Timing;
using FRLG.StarterTool.Core.Training;

namespace FRLG.StarterTool.App;

public sealed class TrainingPanel : Panel
{
    private static readonly int[] ManipWindows = { 1, 2, 3, 4 };

    private readonly Random _random = new();

    private readonly TextBox _roundsBox;
    private const int RowHeight = 23;

    private const int PanelWidth = 390;

    private readonly Button _buttonRun;

    private readonly Button _buttonClose;
    private readonly ThemedListView _list;
    private readonly Label _status;
    private readonly Label[] _rates = new Label[ManipWindows.Length];
    private readonly Label _recommendation;
    private readonly Button _buttonApply;
    private readonly Label _hint;

    private TrainingSession? _session;

    private bool _running;

    private bool _roundArmed;
    private int _armedTargetFrame;
    private int _armedOffsetMs;
    private int _armedWindowMs;

    private System.Windows.Forms.Timer? _missDelay;

    public TrainingPanel()
    {
        _roundsBox = new ThemedTextBox
        {
            AutoSize = false,
            Location = new Point(50, 0),
            Size = new Size(40, RowHeight),
            TextAlign = HorizontalAlignment.Center
        };

        var roundsLabel = new Label
        {
            Text = "Rounds",
            Location = new Point(0, (RowHeight - 18) / 2),
            Size = new Size(48, 18)
        };

        _buttonRun = new ThemedButton
        {
            Text = "Start Training",
            Location = new Point(98, 0),
            Size = new Size(110, RowHeight)
        };
        _buttonRun.Click += (_, _) => StartOrResetSession();

        _buttonClose = new ThemedButton
        {
            Text = "Stop",
            Location = new Point(PanelWidth - 80, 0),
            Size = new Size(80, RowHeight)
        };
        _buttonClose.Click += (_, _) => CloseRequested?.Invoke(this, EventArgs.Empty);

        _list = new ThemedListView
        {
            Location = new Point(0, 28),
            Size = new Size(PanelWidth, 152),
            View = View.Details,
            FullRowSelect = true,
            MultiSelect = false,
            HeaderStyle = ColumnHeaderStyle.Nonclickable,
            Font = new Font("Segoe UI", 8F),
            OwnerDraw = true
        };
        _list.DrawColumnHeader += DrawColumnHeader;
        _list.DrawItem += (_, _) => { };
        _list.DrawSubItem += DrawSubItem;
        _list.Columns.Add("#", 34, HorizontalAlignment.Center);
        _list.Columns.Add("Target", 70, HorizontalAlignment.Center);
        _list.Columns.Add("Landed", 70, HorizontalAlignment.Center);
        _list.Columns.Add("Frames", 70, HorizontalAlignment.Center);
        _list.Columns.Add("ms", 62, HorizontalAlignment.Center);
        _list.Columns.Add("Hit", 60, HorizontalAlignment.Center);
        _list.HandleCreated += (_, _) => FitLastColumn();

        _status = new Label
        {
            Location = new Point(0, 186),
            Size = new Size(PanelWidth, 32),
            Font = new Font(Font.FontFamily, 9F, FontStyle.Bold),
            TextAlign = ContentAlignment.MiddleCenter,
            Tag = Theme.KeepForeColor
        };

        for (int i = 0; i < ManipWindows.Length; i++)
        {
            _rates[i] = new Label
            {
                Location = new Point(i % 2 == 0 ? 8 : 200, 222 + i / 2 * 17),
                Size = new Size(184, 16)
            };
        }

        _recommendation = new Label
        {
            Location = new Point(0, 262),
            Size = new Size(250, 20),
            TextAlign = ContentAlignment.MiddleLeft,
            Font = new Font(Font.FontFamily, 9F, FontStyle.Bold),
            Tag = Theme.KeepForeColor
        };

        _buttonApply = new ThemedButton
        {
            Text = "Apply Offset",
            Location = new Point(256, 260),
            Size = new Size(134, 23),
            Enabled = false
        };
        _buttonApply.Click += (_, _) => ApplyRecommendation();

        _hint = new Label
        {
            Location = new Point(0, 288),
            Size = new Size(PanelWidth, 18),
            TextAlign = ContentAlignment.MiddleCenter,
            Tag = Theme.KeepForeColor
        };

        _roundsBox.Leave += (_, _) => CommitRoundCount();
        _roundsBox.KeyDown += (_, e) =>
        {
            if (e.KeyCode != Keys.Enter) return;

            CommitRoundCount();
            if (_list.CanFocus) _list.Focus();
            e.SuppressKeyPress = true;
        };

        Controls.Add(roundsLabel);
        Controls.Add(_roundsBox);
        Controls.Add(_buttonRun);
        Controls.Add(_buttonClose);
        Controls.Add(_list);
        Controls.Add(_status);
        foreach (Label rate in _rates) Controls.Add(rate);
        Controls.Add(_recommendation);
        Controls.Add(_buttonApply);
        Controls.Add(_hint);

        BackColorChanged += (_, _) => RefreshRowColours();

        UpdateReadouts(complete: false);
    }

    public bool IsRunning => _running;

    public event EventHandler? StateChanged;

    public event EventHandler? CloseRequested;

    public void StartSession()
    {
        if (!_running) ToggleSession();
    }

    private int RoundCount =>
        int.TryParse(_roundsBox.Text, NumberStyles.Integer, CultureInfo.InvariantCulture, out int rounds)
            ? Math.Clamp(rounds, 1, 999)
            : DefaultRounds;

    private const int DefaultRounds = 10;

    public void LoadRounds(int rounds) => _roundsBox.Text = rounds.ToString(CultureInfo.InvariantCulture);

    private void CommitRoundCount(bool stopCountdown = true)
    {
        if (!_running || _session == null) return;

        _session.SetRoundCount(RoundCount);
        if (!_session.IsComplete)
        {
            UpdateReadouts(complete: false);
            return;
        }

        _running = false;
        _roundArmed = false;
        _missDelay?.Stop();
        if (stopCountdown && StarterTool.IsTimerRunning) StarterTool.StopTimer(false);

        UpdateReadouts(complete: true);
    }

    public int SaveRounds() => RoundCount;

    public void Cancel()
    {
        if (!_running) return;

        _running = false;
        _roundArmed = false;
        _missDelay?.Stop();

        if (StarterTool.IsTimerRunning) StarterTool.StopTimer(false);

        UpdateReadouts(complete: false);
    }

    private void StartOrResetSession()
    {
        if (_running) Cancel();

        ToggleSession();
    }

    private void ToggleSession()
    {
        if (_running)
        {
            Cancel();
            return;
        }

        VariableOffsetTimer? timer = StarterTool.VariableOffset;
        if (timer == null) return;

        bool visual = timer.TrainingUsesVisualOffset;
        _session = new TrainingSession(
            RoundCount, visual ? timer.VisualOffsetMs : timer.OffsetMs, timer.SelectedFps, visual);
        _running = true;
        _roundArmed = false;
        _list.Items.Clear();
        UpdateReadouts(complete: false);
    }

    public uint? NextTargetFrame(in VariableInfo info)
    {
        CommitRoundCount(stopCountdown: false);

        if (!_running || _session == null || _session.IsComplete) return null;

        MissDelayElapsed(this, EventArgs.Empty);
        _roundArmed = false;
        return _session.NextTargetFrame(info, _random);
    }

    public void RoundArmed(int targetFrame, int offsetMs, int visualOffsetMs, double landingWindowMs)
    {
        if (!_running || _session == null) return;

        _roundArmed = true;
        _armedTargetFrame = targetFrame;
        _armedOffsetMs = _session.Visual ? visualOffsetMs : offsetMs;
        _armedWindowMs = (int)Math.Ceiling(landingWindowMs);
    }

    public bool RecordLanding(int landedFrame, int targetFrame, double deltaMs, double hitChance)
    {
        if (!_running || _session == null || !_roundArmed) return false;

        _missDelay?.Stop();
        _roundArmed = false;
        TrainingRound round = _session.Record(deltaMs, _armedOffsetMs, landedFrame, targetFrame, hitChance);
        AddRow(round);

        bool complete = _session.IsComplete;
        if (complete) _running = false;
        UpdateReadouts(complete);

        if (IsHandleCreated)
        {
            BeginInvoke(() => StarterTool.StopTimer(false));
        }
        else
        {
            StarterTool.StopTimer(false);
        }

        return true;
    }

    public void RoundEnded()
    {
        if (!_running || _session == null || !_roundArmed) return;

        _missDelay ??= new System.Windows.Forms.Timer();
        _missDelay.Stop();
        _missDelay.Interval = Math.Max(1, _armedWindowMs);
        _missDelay.Tick -= MissDelayElapsed;
        _missDelay.Tick += MissDelayElapsed;
        _missDelay.Start();
    }

    private void MissDelayElapsed(object? sender, EventArgs e)
    {
        _missDelay?.Stop();

        if (!_running || _session == null || !_roundArmed) return;

        _roundArmed = false;
        AddRow(_session.MarkMissed(_armedTargetFrame));
        UpdateReadouts(complete: false);
    }

    private void ApplyRecommendation()
    {
        if (_session == null) return;

        VariableOffsetTimer? timer = StarterTool.VariableOffset;
        if (_session.Visual) timer?.ApplyVisualOffset(_session.RecommendedOffsetMs);
        else timer?.ApplyOffset(_session.RecommendedOffsetMs);

        UpdateReadouts(complete: _session.IsComplete);
    }

    private void AddRow(TrainingRound round)
    {
        var item = new ListViewItem(round.Number.ToString(CultureInfo.InvariantCulture));
        item.SubItems.Add(round.TargetFrame.ToString(CultureInfo.InvariantCulture));

        if (round.Missed)
        {
            item.SubItems.Add("-");
            item.SubItems.Add("missed");
            item.SubItems.Add("-");
            item.SubItems.Add("-");
        }
        else
        {
            item.SubItems.Add(round.LandedFrame.ToString(CultureInfo.InvariantCulture));
            item.SubItems.Add(round.ErrorFrames.ToString("+0.00;-0.00;0.00", CultureInfo.InvariantCulture));
            item.SubItems.Add(round.DeltaMs.ToString("+0;-0;0", CultureInfo.InvariantCulture));
            item.SubItems.Add(MainForm.FormatChance(round.HitChance));
        }

        item.Tag = round;
        Colour(item, round);
        _list.Items.Add(item);
        item.EnsureVisible();
        FitLastColumn();
    }

    private static void Colour(ListViewItem item, TrainingRound round)
    {
        if (round.Missed)
        {
            item.BackColor = Theme.ListBack;
            item.ForeColor = Theme.DimText;
            return;
        }

        item.BackColor = round.HitChance > 0.5 ? Theme.LandingHitBack
            : round.HitChance > 0.0 ? Theme.LandingMaybeBack
            : Theme.LandingMissBack;
        item.ForeColor = Theme.LandingRowText;
    }

    private void DrawColumnHeader(object? sender, DrawListViewColumnHeaderEventArgs e)
    {
        using (var background = new SolidBrush(Theme.HeaderBack))
        {
            e.Graphics.FillRectangle(background, e.Bounds);
        }
        using (var pen = new Pen(Theme.Border))
        {
            if (e.ColumnIndex < _list.Columns.Count - 1)
            {
                e.Graphics.DrawLine(pen, e.Bounds.Right - 1, e.Bounds.Top + 2, e.Bounds.Right - 1, e.Bounds.Bottom - 3);
            }
            e.Graphics.DrawLine(pen, e.Bounds.Left, e.Bounds.Bottom - 1, e.Bounds.Right, e.Bounds.Bottom - 1);
        }

        Rectangle bounds = e.Bounds;
        bounds.Inflate(-2, 0);
        TextRenderer.DrawText(e.Graphics, e.Header?.Text ?? "", _list.Font, bounds, Theme.Text, CellFlags);
    }

    private void DrawSubItem(object? sender, DrawListViewSubItemEventArgs e)
    {
        Color back = e.Item?.BackColor ?? _list.BackColor;
        Color fore = e.Item?.ForeColor ?? _list.ForeColor;

        using (var brush = new SolidBrush(back))
        {
            e.Graphics.FillRectangle(brush, e.Bounds);
        }
        using (var pen = new Pen(Theme.GridLine))
        {
            e.Graphics.DrawLine(pen, e.Bounds.Left, e.Bounds.Bottom - 1, e.Bounds.Right, e.Bounds.Bottom - 1);
            if (e.ColumnIndex < _list.Columns.Count - 1)
            {
                e.Graphics.DrawLine(pen, e.Bounds.Right - 1, e.Bounds.Top, e.Bounds.Right - 1, e.Bounds.Bottom - 1);
            }
        }

        Rectangle bounds = e.Bounds;
        bounds.Inflate(-1, 0);
        bounds.Height -= ThemedListView.RuleClearance;
        TextRenderer.DrawText(e.Graphics, e.SubItem?.Text ?? "", _list.Font, bounds, fore, CellFlags);
    }

    private const TextFormatFlags CellFlags = TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis
                                              | TextFormatFlags.NoPrefix | TextFormatFlags.HorizontalCenter;

    private void FitLastColumn()
    {
        int used = 0;
        for (int i = 0; i < _list.Columns.Count - 1; i++) used += _list.Columns[i].Width;

        ColumnHeader last = _list.Columns[_list.Columns.Count - 1];
        int fill = _list.ClientSize.Width - used;
        if (fill >= 60 && fill != last.Width) last.Width = fill;
    }

    private void RefreshRowColours()
    {
        foreach (ListViewItem item in _list.Items)
        {
            if (item.Tag is TrainingRound round) Colour(item, round);
        }
    }

    private void UpdateReadouts(bool complete)
    {
        _buttonRun.Text = _running ? "Reset Rounds" : "Start Training";

        StateChanged?.Invoke(this, EventArgs.Empty);

        if (_session == null)
        {
            _status.ForeColor = Theme.DimText;
            _status.Text = "";
            _hint.ForeColor = Theme.DimText;
            _hint.Text = "Start a session, then press the Start hotkey to run each round.";
            foreach (Label rate in _rates) rate.Text = "";
            _recommendation.Text = "";
            _buttonApply.Enabled = false;
            return;
        }

        OffsetTuner tuner = _session.Tuner;
        VariableOffsetTimer? timer = StarterTool.VariableOffset;
        string offsetName = _session.Visual ? "Visual offset" : "Offset";
        int current = (_session.Visual ? timer?.VisualOffsetMs : timer?.OffsetMs) ?? _session.InitialOffsetMs;
        int recommended = _session.RecommendedOffsetMs;

        _buttonApply.Text = _session.Visual ? "Apply Visual Offset" : "Apply Offset";

        _status.ForeColor = complete ? Theme.LandingHitText : Theme.Text;
        _status.Text = complete
            ? $"Session complete - {_session.CompletedRounds} presses"
            : _running
                ? $"Round {_session.CurrentRound} of {_session.RoundCount}"
                : $"Stopped after {_session.CompletedRounds} of {_session.RoundCount}";

        if (tuner.Observations == 0)
        {
            foreach (Label rate in _rates) rate.Text = "";
            _status.Text += "  -  press Start when ready";
        }
        else
        {
            _status.Text += $"\r\nSpread {tuner.MeanSigma:0.00} frames (+/- {tuner.SdSigma:0.00})";

            for (int i = 0; i < ManipWindows.Length; i++)
            {
                double rate = OffsetTuner.HitRate(tuner.MeanSigma, ManipWindows[i]);
                _rates[i].Text = $"{ManipWindows[i]}-frame manip:  {rate * 100.0:0.0}%";
            }
        }

        _recommendation.ForeColor = recommended == current ? Theme.DimText : Theme.LandingMaybeText;
        _recommendation.Text = tuner.Observations == 0
            ? $"{offsetName} {current} ms"
            : $"Recommended {(_session.Visual ? "visual" : "offset")}: {recommended} ms  (now {current})";
        _buttonApply.Enabled = tuner.Observations > 0 && recommended != current;

        if (_running)
        {
            _hint.ForeColor = Theme.DimText;
            _hint.Text = _session.Visual
                ? "Press Start to begin the round, then again on the last flash."
                : "Press Start to begin the round, then again on the last beep.";
        }
        else if (tuner.Observations > 0)
        {
            _hint.ForeColor = Theme.Text;
            _hint.Text =
                $"Expected hits {_session.ExpectedHits:0.00} of {_session.CompletedRounds}"
                + $"  -  average hit chance {MainForm.FormatChance(_session.AverageHitChance)}";
        }
        else
        {
            _hint.ForeColor = Theme.DimText;
            _hint.Text = "";
        }
    }
}

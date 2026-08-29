using System.Globalization;
using FRLG.StarterTool.Core.Search;
using FRLG.StarterTool.Core.Settings;

namespace FRLG.StarterTool.App;

public partial class MainForm
{
    private readonly List<ConstraintRangePanel> _rangeCards = new();

    private ConstraintRangePanel? _dragCard;

    private int _dragGrabY;

    private bool _fillingFilters;

    private void InitializeRanges()
    {
        ButtonAddRange.Click += (_, _) => AddRange();

        ButtonFilterLoad.Click += (_, _) => LoadSelectedFilter();
        ButtonFilterSaveAs.Click += (_, _) => { SaveFilterAs(); FillFilterList(); };
        ButtonFilterUpdate.Click += (_, _) => { UpdateActiveFilter(); FillFilterList(); };
        ButtonFilterRename.Click += (_, _) => { RenameActiveFilter(); FillFilterList(); };
        ButtonFilterDelete.Click += (_, _) => { DeleteActiveFilter(); FillFilterList(); };

        ListBoxFilters.DoubleClick += (_, _) => LoadSelectedFilter();
        ListBoxFilters.SelectedIndexChanged += (_, _) =>
        {
            if (_fillingFilters) return;
            if (StarterTool.Settings is not { } settings) return;

            settings.ActivePreset = ListBoxFilters.SelectedItem as string ?? "";
        };
    }

    private void ShowRanges(IReadOnlyList<ConstraintRange> ranges)
    {
        PanelRanges.SuspendLayout();

        foreach (ConstraintRangePanel card in _rangeCards)
        {
            PanelRanges.Controls.Remove(card);
            card.Dispose();
        }
        _rangeCards.Clear();

        foreach (ConstraintRange range in ranges)
        {
            AddCard(range.Clone());
        }

        if (_rangeCards.Count == 0) AddCard(NewRange());

        PanelRanges.ResumeLayout(false);
        RelayoutRangeCards();
        FillFilterList();
    }

    private List<ConstraintRange> CaptureRanges()
    {
        var ranges = new List<ConstraintRange>(_rangeCards.Count);
        foreach (ConstraintRangePanel card in _rangeCards) ranges.Add(card.Read().Clone());
        return ranges;
    }

    private void AddRange()
    {
        AddCard(NewRange());
        RelayoutRangeCards();

        PanelRanges.ScrollControlIntoView(_rangeCards[^1]);
    }

    private ConstraintRange NewRange() => new ConstraintRange
    {
        Name = "Range " + (_rangeCards.Count + 1).ToString(CultureInfo.InvariantCulture),
        Color = _rangeCards.Count == 0 ? ConstraintRange.Screen : FreePaletteColor()
    }.Normalize();

    private int FreePaletteColor()
    {
        var used = new HashSet<int>();
        foreach (ConstraintRangePanel card in _rangeCards)
        {
            if (card.RowColor is { } colour) used.Add(colour.ToArgb());
        }

        for (int i = 0; i < PaletteSize; i++)
        {
            if (!used.Contains(Theme.RangeColor(i).ToArgb())) return Theme.RangeColor(i).ToArgb() & 0xFFFFFF;
        }

        return Theme.RangeColor(0).ToArgb() & 0xFFFFFF;
    }

    private const int PaletteSize = 8;

    private static Color? PaletteColorAt(int index) => index <= 0 ? null : Theme.RangeColor(index - 1);

    private void AddCard(ConstraintRange range)
    {
        var card = new ConstraintRangePanel(range);
        card.RemoveRequested += (sender, _) => RemoveCard((ConstraintRangePanel)sender!);
        card.SearchRequested += (_, _) => RunSearch();
        card.CalculateRequested += (sender, _) => CalculateRangeOdds((ConstraintRangePanel)sender!);
        card.ColorChanged += (_, _) => RefreshRangeColors();
        card.Grip.MouseDown += (_, e) => BeginDrag(card, e);
        card.Grip.MouseMove += (_, e) => ContinueDrag(card, e);
        card.Grip.MouseUp += (_, _) => EndDrag();

        _rangeCards.Add(card);
        PanelRanges.Controls.Add(card);

        Theme.ApplyTo(card);
        card.RefreshSwatch();
    }

    private void RemoveCard(ConstraintRangePanel card)
    {
        if (_rangeCards.Count <= 1) return;

        _rangeCards.Remove(card);
        PanelRanges.Controls.Remove(card);
        card.Dispose();
        RelayoutRangeCards();
        RefreshRangeColors();
    }

    private void RelayoutRangeCards()
    {
        PanelRanges.SuspendLayout();

        int gap = Scaled(RowGap);
        int width = PanelRanges.Width - SystemInformation.VerticalScrollBarWidth - gap;
        int top = 0;

        for (int i = 0; i < _rangeCards.Count; i++)
        {
            ConstraintRangePanel card = _rangeCards[i];
            card.PaletteColor = PaletteColorAt(i);
            card.Width = width;
            card.Relayout();
            card.Location = new Point((PanelRanges.ClientSize.Width - width) / 2, top);
            card.SetRemovable(_rangeCards.Count > 1);
            top = card.Bottom + gap;
        }

        PanelRanges.ResumeLayout(true);
    }

    private void RefreshRangeColors()
    {
        for (int i = 0; i < _rangeCards.Count; i++)
        {
            _rangeCards[i].PaletteColor = PaletteColorAt(i);
            _rangeCards[i].RefreshSwatch();
            _rangeCards[i].Invalidate();
        }

        ListViewResults.Invalidate();
    }

    private Color? RangeRowColor(int index) =>
        index >= 0 && index < _rangeCards.Count ? _rangeCards[index].RowColor : null;

    private void BeginDrag(ConstraintRangePanel card, MouseEventArgs e)
    {
        if (e.Button != MouseButtons.Left) return;

        _dragCard = card;
        _dragGrabY = card.Grip.Top + e.Y;
        card.BringToFront();
    }

    private void ContinueDrag(ConstraintRangePanel card, MouseEventArgs e)
    {
        if (_dragCard != card) return;

        int index = _rangeCards.IndexOf(card);
        if (index < 0) return;

        int pointer = card.Top + card.Grip.Top + e.Y;
        int pitch = card.Height + Scaled(RowGap);
        if (pitch <= 0) return;

        int target = Math.Clamp((pointer - _dragGrabY + pitch / 2) / pitch, 0, _rangeCards.Count - 1);
        if (target == index) return;

        _rangeCards.RemoveAt(index);
        _rangeCards.Insert(target, card);
        RelayoutRangeCards();
        card.BringToFront();

        ListViewResults.Invalidate();
    }

    private void EndDrag() => _dragCard = null;

    private List<RangeSearchCriteria> ReadRangeCriteria(bool seedless = false)
    {
        int seed = seedless ? 0 : ReadTrainerId();
        ReadFrameRange(out int windowMin, out int windowMax);

        var criteria = new List<RangeSearchCriteria>(_rangeCards.Count);
        foreach (ConstraintRangePanel card in _rangeCards)
        {
            ConstraintRange range = card.Read();

            int min = Bound(range.MinFrame, windowMin);
            int max = Bound(range.MaxFrame, windowMax);

            criteria.Add(new RangeSearchCriteria(
                new PredictorSearchCriteria
                {
                    Seed = seed,
                    MinFrame = Math.Max(windowMin, min),
                    MaxFrame = Math.Min(windowMax, max),
                    Natures = range.Natures,
                    Minus = ToPack(range.IvMinus),
                    Neutral = ToPack(range.IvNeutral),
                    Plus = ToPack(range.IvPlus)
                },
                range.Backup,
                range.BackupWithin));
        }

        return criteria;

        static int Bound(string text, int fallback) =>
            int.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out int value)
                ? value
                : fallback;
    }

    private static StatPack ToPack(int[] values) =>
        new(values[0], values[1], values[2], values[3], values[4], values[5]);

    private async void CalculateRangeOdds(ConstraintRangePanel card)
    {
        int index = _rangeCards.IndexOf(card);
        if (index < 0) return;

        List<RangeSearchCriteria> criteria = ReadRangeCriteria(seedless: true);
        if (index >= criteria.Count) return;

        PredictorSearchCriteria filter = criteria[index].Filter;

        if (StarterTool.Settings != null) StarterTool.Settings.TipOddsCalculated = true;

        card.ShowOddsBusy();
        try
        {
            double odds = await Task.Run(() => SeedOdds.Calculate(filter));
            card.ShowOdds((odds * 100.0).ToString("0.00", CultureInfo.InvariantCulture) + "%");
        }
        catch (Exception)
        {
            card.ShowOdds(null);
        }
    }

    private void FillFilterList()
    {
        if (StarterTool.Settings is not { } settings) return;

        _fillingFilters = true;
        try
        {
            ListBoxFilters.BeginUpdate();
            ListBoxFilters.Items.Clear();
            foreach (FilterPreset preset in settings.Presets) ListBoxFilters.Items.Add(preset.Name);
            ListBoxFilters.EndUpdate();

            int index = -1;
            for (int i = 0; i < settings.Presets.Count; i++)
            {
                if (!FilterPreset.NameEquals(settings.Presets[i].Name, settings.ActivePreset)) continue;

                index = i;
                break;
            }
            ListBoxFilters.SelectedIndex = index;
        }
        finally
        {
            _fillingFilters = false;
        }

        bool any = ListBoxFilters.SelectedIndex >= 0;
        ButtonFilterLoad.Enabled = any;
        ButtonFilterUpdate.Enabled = any;
        ButtonFilterRename.Enabled = any;
        ButtonFilterDelete.Enabled = any;
    }

    private void LoadSelectedFilter()
    {
        if (StarterTool.Settings is not { } settings) return;
        if (ListBoxFilters.SelectedItem is not string name) return;

        FilterPreset? preset = settings.FindPreset(name);
        if (preset == null) return;

        LoadFilter(preset);
    }
}

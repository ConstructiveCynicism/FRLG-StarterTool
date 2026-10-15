using System.Globalization;
using FRLG.StarterTool.Core.Pokemon;
using FRLG.StarterTool.Core.Settings;

namespace FRLG.StarterTool.App;

public sealed class TorrentPanel : Panel
{
    public const int PanelWidth = 188;

    private const int RowHeight = 22;

    private const int FieldX = 70;

    public const int TableRows = 16;

    private const int TableTop = 126;

    private const int TableRowHeight = 20;

    public const int PanelHeight = TableTop + (TableRows + 1) * TableRowHeight;

    private readonly ThemedComboBox _routeBox;
    private readonly ThemedComboBox _moonBox;
    private readonly ThemedTextBox _levelBox;
    private readonly ThemedTextBox _ivBox;
    private readonly Label _evs;
    private readonly Label _source;
    private readonly TorrentGrid _grid;

    private PokemonSpecies _species = PokemonSpecies.Get(7);

    private int? _frame;

    private long _pid = -1;

    public const int StartLevel = 7;

    private bool _filling;

    public TorrentPanel()
    {
        Size = new Size(PanelWidth, PanelHeight);

        Controls.Add(MakeLabel("Route", 0));
        _routeBox = MakeCombo(0);
        _routeBox.Items.AddRange(new object[] { "Glitchless", "Round 2" });
        _routeBox.SelectedIndex = 0;
        Controls.Add(_routeBox);

        Controls.Add(MakeLabel("Mt. Moon", 26));
        _moonBox = MakeCombo(26);
        Controls.Add(_moonBox);
        FillMoonOptionals();

        _routeBox.SelectedIndexChanged += (_, _) =>
        {
            FillMoonOptionals();
            Rebuild();
        };
        _moonBox.SelectedIndexChanged += (_, _) =>
        {
            if (!_filling) Rebuild();
        };

        Controls.Add(MakeLabel("Level", 52));
        _levelBox = MakeBox(52, "7");
        Controls.Add(_levelBox);
        Button minus = MakeButton("−", FieldX + 52, 52);
        minus.Click += (_, _) => StepLevel(-1);
        Controls.Add(minus);
        Button plus = MakeButton("+", FieldX + 76, 52);
        plus.Click += (_, _) => StepLevel(1);
        Controls.Add(plus);

        Controls.Add(MakeLabel("HP IV", 78));
        _ivBox = MakeBox(78, "31");
        Controls.Add(_ivBox);
        _evs = new Label
        {
            Location = new Point(FieldX + 52, 81),
            Size = new Size(PanelWidth - FieldX - 52, 16),
            TextAlign = ContentAlignment.MiddleLeft,
            Tag = Theme.KeepForeColor
        };
        Controls.Add(_evs);

        _source = new Label
        {
            Location = new Point(0, 104),
            Size = new Size(PanelWidth, 18),
            TextAlign = ContentAlignment.MiddleLeft,
            AutoEllipsis = true,
            Tag = Theme.KeepForeColor
        };
        Controls.Add(_source);

        _grid = new TorrentGrid
        {
            Location = new Point(0, TableTop),
            Size = new Size(PanelWidth, PanelHeight - TableTop)
        };
        _grid.MouseWheel += (_, e) => StepLevel(e.Delta > 0 ? -1 : 1);
        _grid.RowClicked += (_, level) => SetLevel(level);
        Controls.Add(_grid);

        foreach (TextBox box in new[] { _levelBox, _ivBox }) box.TextChanged += (_, _) => Rebuild();

        Rebuild();
    }

    public void SetSelection(PokemonSpecies species, int hpIv, int frame, long pid)
    {
        bool fresh = _frame != frame || _pid != pid;
        _species = species;
        _frame = frame;
        _pid = pid;
        if (fresh) SetLevel(StartLevel);
        string iv = hpIv.ToString(CultureInfo.InvariantCulture);
        if (_ivBox.Text != iv) _ivBox.Text = iv;
        else Rebuild();
    }

    public void SetSpecies(PokemonSpecies species)
    {
        if (_frame != null) return;
        _species = species;
        Rebuild();
    }

    public void LoadSettings(AppSettings settings)
    {
        _levelBox.Text = Math.Clamp(settings.TorrentLevel, 1, 100).ToString(CultureInfo.InvariantCulture);
        _routeBox.SelectedIndex = settings.TorrentRoute == "round2" ? 1 : 0;
        MoonOptional optional = Enum.TryParse(settings.TorrentMoonOptional, true, out MoonOptional parsed)
            ? parsed
            : MoonOptional.Josh;
        _moonBox.SelectedIndex = (int)optional < _moonBox.Items.Count ? (int)optional : 0;
    }

    public void CaptureSettings(AppSettings settings)
    {
        settings.TorrentLevel = Level;
        settings.TorrentRoute = Route == TorrentRoute.Round2 ? "round2" : "glitchless";
        settings.TorrentMoonOptional = Optional.ToString().ToLowerInvariant();
    }

    public void StepLevel(int by) => SetLevel(Level + by);

    private void SetLevel(int level) =>
        _levelBox.Text = Math.Clamp(level, 1, 100).ToString(CultureInfo.InvariantCulture);

    private int Level => Read(_levelBox, StartLevel, 1, 100);

    private TorrentRoute Route => _routeBox.SelectedIndex == 1 ? TorrentRoute.Round2 : TorrentRoute.Glitchless;

    private MoonOptional Optional => (MoonOptional)Math.Max(0, _moonBox.SelectedIndex);

    private void FillMoonOptionals()
    {
        int keep = Math.Max(0, _moonBox.SelectedIndex);
        string[] names = Route == TorrentRoute.Round2
            ? new[] { "Josh", "Robby", "Rocket", "Hiker" }
            : new[] { "Josh", "Robby", "Rocket" };

        _filling = true;
        _moonBox.Items.Clear();
        _moonBox.Items.AddRange(names);
        _moonBox.SelectedIndex = keep < names.Length ? keep : 0;
        _filling = false;
    }

    private static int Read(TextBox box, int fallback, int min, int max) =>
        int.TryParse(box.Text, NumberStyles.Integer, CultureInfo.InvariantCulture, out int value)
            ? Math.Clamp(value, min, max)
            : fallback;

    private void Rebuild()
    {
        TorrentRoute route = Route;
        MoonOptional optional = Optional;
        int level = Level;

        _grid.Rows = TorrentTable.Build(
            _species, Read(_ivBox, 31, 0, 31), at => TorrentTable.HpEvsAt(route, optional, at), level, TableRows);
        _grid.Invalidate();

        _evs.Text = $"{TorrentTable.HpEvsAt(route, optional, level)} HP EVs";
        _evs.ForeColor = Theme.DimText;
        _source.Text = _frame is int frame ? $"Frame {frame} · {_species.Name}" : $"{_species.Name} · no row selected";
        _source.ForeColor = Theme.DimText;
    }

    private static Label MakeLabel(string text, int y) =>
        new()
        {
            Text = text,
            Location = new Point(0, y + 3),
            Size = new Size(FieldX - 4, 16),
            TextAlign = ContentAlignment.MiddleRight
        };

    private static ThemedComboBox MakeCombo(int y) =>
        new()
        {
            Location = new Point(FieldX, y),
            Size = new Size(PanelWidth - FieldX, RowHeight),
            DropDownStyle = ComboBoxStyle.DropDownList
        };

    private static ThemedTextBox MakeBox(int y, string text) =>
        new()
        {
            Numeric = true,
            AutoSize = false,
            Text = text,
            Location = new Point(FieldX, y),
            Size = new Size(48, RowHeight),
            TextAlign = HorizontalAlignment.Center
        };

    private static Button MakeButton(string text, int x, int y) =>
        new ThemedButton
        {
            Text = text,
            Location = new Point(x, y),
            Size = new Size(22, RowHeight),
            TakesCaret = false
        };

    private sealed class TorrentGrid : Control
    {
        private static readonly string[] Headers = { "Level", "Torrent", "Gain", "Max" };

        public IReadOnlyList<TorrentRow> Rows { get; set; } = Array.Empty<TorrentRow>();

        public event EventHandler<int>? RowClicked;

        public TorrentGrid()
        {
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer
                     | ControlStyles.UserPaint | ControlStyles.ResizeRedraw, true);
            SetStyle(ControlStyles.Selectable, false);
            Cursor = Cursors.Hand;
        }

        private float RowPitch => Height / (float)(TableRows + 1);

        protected override void OnMouseClick(MouseEventArgs e)
        {
            base.OnMouseClick(e);
            int index = (int)(e.Y / RowPitch) - 1;
            if (index > 0 && index < Rows.Count) RowClicked?.Invoke(this, Rows[index].Level);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            Graphics g = e.Graphics;
            g.Clear(Theme.ListBack);

            float rowHeight = RowPitch;
            using var bold = new Font(Font, FontStyle.Bold);

            using (var header = new SolidBrush(Theme.HeaderBack)) g.FillRectangle(header, 0, 0, Width, rowHeight);
            for (int column = 0; column < Headers.Length; column++)
            {
                DrawCell(g, Headers[column], Font, Theme.DimText, column, 0, rowHeight);
            }

            for (int i = 0; i < Rows.Count; i++)
            {
                TorrentRow row = Rows[i];
                float top = (i + 1) * rowHeight;
                Color back = i == 0 ? Theme.Accent : i % 2 == 0 ? Theme.RowPrimary : Theme.RowAlternate;
                using (var brush = new SolidBrush(back)) g.FillRectangle(brush, 0, top, Width, rowHeight);

                Color text = i == 0 ? Theme.AccentText : Theme.Text;
                DrawCell(g, row.Level.ToString(CultureInfo.InvariantCulture), Font, text, 0, top, rowHeight);
                DrawCell(g, row.TorrentHp > 0 ? row.TorrentHp.ToString(CultureInfo.InvariantCulture) : "—",
                    bold, text, 1, top, rowHeight);
                DrawCell(g, i == 0 ? "" : "+" + row.Gain.ToString(CultureInfo.InvariantCulture), Font,
                    i == 0 ? text : Theme.DimText, 2, top, rowHeight);
                DrawCell(g, row.MaxHp.ToString(CultureInfo.InvariantCulture), Font, text, 3, top, rowHeight);
            }

            using var rule = new Pen(Theme.GridLine);
            g.DrawRectangle(rule, 0, 0, Width - 1, Height - 1);
        }

        private void DrawCell(Graphics g, string text, Font font, Color color, int column, float top, float height)
        {
            int left = column * Width / Headers.Length;
            var cell = new Rectangle(left, (int)top, (column + 1) * Width / Headers.Length - left, (int)height);
            TextRenderer.DrawText(g, text, font, cell, color,
                TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPadding);
        }
    }
}

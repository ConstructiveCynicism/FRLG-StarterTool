namespace FRLG.StarterTool.App;

public static class Theme
{
    public static bool Dark { get; set; } = true;

    public const string KeepForeColor = "keep-fore";

    public const string KeepBackColor = "keep-back";

    public const string SectionHeader = "section-header";

    public static Color Window => Dark ? Color.FromArgb(0x24, 0x26, 0x2A) : SystemColors.Control;

    public static Color Surface => Dark ? Color.FromArgb(0x3A, 0x3E, 0x45) : Color.FromArgb(0xFA, 0xFA, 0xFA);

    public static Color Input => Dark ? Color.FromArgb(0x36, 0x3A, 0x41) : Color.White;

    public static Color ListBack => Dark ? Color.FromArgb(0x0F, 0x11, 0x14) : Color.FromArgb(0xD6, 0xD9, 0xDE);

    public static Color RowPrimary => Dark ? Color.Black : Color.FromArgb(0xF4, 0xF6, 0xF8);

    public static Color RowAlternate => Dark ? Color.FromArgb(0x1B, 0x1E, 0x23) : Color.FromArgb(0xE6, 0xE9, 0xEE);

    public static Color HeaderBack => Dark ? Color.FromArgb(0x1A, 0x1C, 0x20) : Surface;

    public static Color GridLine => Dark ? Color.FromArgb(0x33, 0x37, 0x3D) : Color.FromArgb(0xA8, 0xAD, 0xB5);

    public static Color Text => Dark ? Color.FromArgb(0xE6, 0xE8, 0xEA) : Color.FromArgb(0x14, 0x14, 0x14);

    public static Color DimText => Dark ? Color.FromArgb(0x9A, 0xA0, 0xA6) : Color.FromArgb(0x5A, 0x5A, 0x5A);

    public static Color Border => Dark ? Color.FromArgb(0x6C, 0x73, 0x7B) : Color.FromArgb(0x46, 0x46, 0x46);

    public static Color SectionBorder => Dark ? Color.FromArgb(0x7C, 0x5C, 0xAD) : Border;

    public static Color SectionCaption => Dark ? Color.FromArgb(0x86, 0xC5, 0xD1) : Color.Black;

    public static Color CheckMark => Dark ? Color.FromArgb(0x5A, 0xA9, 0xE6) : Color.FromArgb(0x15, 0x5E, 0xB0);

    public static Color Accent => Dark ? Color.FromArgb(0x3A, 0x6E, 0xA5) : SystemColors.Highlight;

    public static Color AccentText => Dark ? Color.White : SystemColors.HighlightText;

    public static Color Hover => Dark ? Color.FromArgb(0x4C, 0x52, 0x5C) : Color.FromArgb(0xE4, 0xE4, 0xE4);

    public static Color TimerFlash => Color.FromArgb(0x1B, 0x35, 0x8C);

    public static Color TimerFlashFinal => Color.FromArgb(0x12, 0x5E, 0x2B);

    public static Color LandingHitBack => Dark ? Color.FromArgb(0x2E, 0x7D, 0x32) : Color.FromArgb(0x76, 0xD1, 0x76);

    public static Color LandingMaybeBack => Dark ? Color.FromArgb(0x8A, 0x6D, 0x1F) : Color.FromArgb(0xE8, 0xD0, 0x7A);

    public static Color LandingMissBack => Dark ? Color.FromArgb(0xB0, 0x3A, 0x3A) : Color.FromArgb(0xE8, 0x84, 0x84);

    public static Color LandingAlternateBack => Dark ? Color.FromArgb(0x55, 0x48, 0x22) : Color.FromArgb(0xF6, 0xEC, 0xC2);

    public static Color LandingRowText => Dark ? Color.White : Color.Black;

    public static Color LandingHitText => Dark ? Color.FromArgb(0x6E, 0xD4, 0x77) : Color.FromArgb(0x10, 0x55, 0x1B);

    public static Color LandingMaybeText => Dark ? Color.FromArgb(0xE6, 0xC0, 0x50) : Color.FromArgb(0x8A, 0x63, 0x00);

    public static Color LandingMissText => Dark ? Color.FromArgb(0xF0, 0x8A, 0x8A) : Color.FromArgb(0xA8, 0x1C, 0x1C);

    public const string StartButtonTag = "action-start";

    public const string StopButtonTag = "action-stop";

    public static Color StartBack => Dark ? Color.FromArgb(0x2E, 0x7D, 0x32) : Color.FromArgb(0x43, 0xA0, 0x47);

    public static Color StopBack => Dark ? Color.FromArgb(0xB0, 0x3A, 0x3A) : Color.FromArgb(0xD3, 0x2F, 0x2F);

    public const string NudgeButtonTag = "action-nudge";

    public static Color NudgeBack => Dark ? Color.FromArgb(0x1E, 0x3C, 0x66) : Color.FromArgb(0xC6, 0xDC, 0xF2);

    public static void Apply(Form form)
    {
        form.SuspendLayout();
        try
        {
            form.BackColor = Window;
            form.ForeColor = Text;
            ApplyToChildren(form);
        }
        finally
        {
            form.ResumeLayout();
        }

        ApplyTitleBar(form);
        form.Invalidate(true);
    }

    public static void ApplyMenu(ToolStripItemCollection items) => ApplyMenu(items, Surface);

    private static void ApplyMenu(ToolStripItemCollection items, Color background)
    {
        foreach (ToolStripItem item in items)
        {
            StyleMenuItem(item, background);
        }
    }

    private static void ApplyToChildren(Control parent)
    {
        foreach (Control control in parent.Controls)
        {
            if (Style(control)) ApplyToChildren(control);
        }
    }

    private static bool Style(Control control)
    {
        switch (control)
        {
            case StatBoxPanel:
                return false;

            case MenuStrip menu:
                menu.BackColor = Window;
                menu.ForeColor = Text;
                menu.Renderer = new ThemeMenuRenderer();
                ApplyMenu(menu.Items, Window);
                return false;

            case GroupBox group:
                group.BackColor = Window;
                group.ForeColor = SectionCaption;
                return true;

            case CheckBox { Appearance: Appearance.Button } toggle:
                StyleAsButton(toggle, toggle.FlatAppearance);
                toggle.FlatAppearance.CheckedBackColor = Accent;
                toggle.ForeColor = Text;
                return false;

            case Button button when button.Tag as string == StartButtonTag:
                StyleAsActionButton(button, button.FlatAppearance, StartBack);
                return false;

            case Button button when button.Tag as string == StopButtonTag:
                StyleAsActionButton(button, button.FlatAppearance, StopBack);
                return false;

            case ThemedButton button when button.Tag as string == NudgeButtonTag:
                StyleAsButton(button, button.FlatAppearance);
                button.GlyphColor = Dark ? Color.White : Color.Black;
                button.BackColor = NudgeBack;
                button.FlatAppearance.MouseOverBackColor = Dark
                    ? ControlPaint.Light(NudgeBack, 0.4f)
                    : ControlPaint.Dark(NudgeBack, 0.05f);
                button.FlatAppearance.MouseDownBackColor = button.FlatAppearance.MouseOverBackColor;
                return false;

            case Button button:
                StyleAsButton(button, button.FlatAppearance);
                return false;

            case CheckBox check:
                check.BackColor = Window;
                check.ForeColor = Text;
                check.FlatStyle = Dark ? FlatStyle.Flat : FlatStyle.Standard;
                check.FlatAppearance.BorderColor = Border;
                check.FlatAppearance.CheckedBackColor = Input;
                return false;

            case TextBox textBox:
                textBox.BackColor = Input;
                textBox.ForeColor = Text;
                textBox.BorderStyle = BorderStyle.FixedSingle;
                (textBox as ThemedTextBox)?.RefreshBorder();
                return false;

            case ComboBox combo:
                combo.BackColor = Input;
                combo.ForeColor = Text;
                combo.FlatStyle = FlatStyle.Flat;
                (combo as ThemedComboBox)?.RefreshDrawMode();
                return false;

            case ListView list:
                list.BackColor = ListBack;
                list.ForeColor = Text;
                list.BorderStyle = BorderStyle.FixedSingle;
                StyleScrollBars(list);
                (list as ThemedListView)?.RefreshHeader();
                return false;

            case PictureBox picture:
                picture.BackColor = Window;
                return false;

            case TrackBar track:
                track.BackColor = Window;
                return false;

            case Label label:
                label.BackColor = Color.Transparent;
                if (label.Tag as string == SectionHeader) label.ForeColor = SectionCaption;
                else if (label.Tag as string != KeepForeColor) label.ForeColor = Text;
                return false;

            case TableLayoutPanel or Panel:
                if (control.Tag as string != KeepBackColor) control.BackColor = Window;
                control.ForeColor = Text;
                return true;

            default:
                control.BackColor = Window;
                control.ForeColor = Text;
                return true;
        }
    }

    private static void StyleScrollBars(Control control)
    {
        if (control.IsHandleCreated)
        {
            Win32.SetDarkScrollBars(control.Handle, Dark);
            return;
        }

        control.HandleCreated += OnHandleCreated;

        void OnHandleCreated(object? sender, EventArgs e)
        {
            control.HandleCreated -= OnHandleCreated;
            Win32.SetDarkScrollBars(control.Handle, Dark);
        }
    }

    private static void StyleAsButton(ButtonBase button, FlatButtonAppearance flat)
    {
        button.FlatStyle = FlatStyle.Flat;
        button.BackColor = Surface;
        if (button.Tag as string != KeepForeColor) button.ForeColor = Text;
        button.UseVisualStyleBackColor = false;
        flat.BorderColor = Border;
        flat.BorderSize = 1;
        flat.MouseOverBackColor = Hover;
        flat.MouseDownBackColor = Hover;
    }

    private static void StyleAsActionButton(ButtonBase button, FlatButtonAppearance flat, Color back)
    {
        button.FlatStyle = FlatStyle.Flat;
        button.BackColor = back;
        button.ForeColor = Color.White;
        button.UseVisualStyleBackColor = false;
        flat.BorderColor = Border;
        flat.BorderSize = 1;
        flat.MouseOverBackColor = ControlPaint.Light(back, 0.25f);
        flat.MouseDownBackColor = ControlPaint.Dark(back, 0.1f);
    }

    private static void StyleMenuItem(ToolStripItem item, Color background)
    {
        item.BackColor = background;
        item.ForeColor = item.Enabled ? Text : DimText;

        if (item is ToolStripMenuItem menuItem)
        {
            menuItem.DropDown.BackColor = Surface;
            menuItem.DropDown.ForeColor = Text;
            ApplyMenu(menuItem.DropDownItems, Surface);
        }
    }

    private static void ApplyTitleBar(Form form)
    {
        if (form.IsHandleCreated)
        {
            Win32.SetDarkTitleBar(form.Handle, Dark);
            return;
        }

        form.HandleCreated += OnHandleCreated;

        void OnHandleCreated(object? sender, EventArgs e)
        {
            form.HandleCreated -= OnHandleCreated;
            Win32.SetDarkTitleBar(form.Handle, Dark);
        }
    }

    private sealed class ThemeMenuRenderer : ToolStripProfessionalRenderer
    {
        public ThemeMenuRenderer() : base(new ThemeColorTable())
        {
            RoundedEdges = false;
        }

        protected override void OnRenderArrow(ToolStripArrowRenderEventArgs e)
        {
            e.ArrowColor = e.Item is { Enabled: false } ? DimText : Text;
            base.OnRenderArrow(e);
        }

        private sealed class ThemeColorTable : ProfessionalColorTable
        {
            public override Color MenuStripGradientBegin => Window;
            public override Color MenuStripGradientEnd => Window;
            public override Color ToolStripDropDownBackground => Surface;
            public override Color ToolStripBorder => Window;
            public override Color MenuBorder => Border;
            public override Color MenuItemBorder => Border;
            public override Color MenuItemSelected => Hover;
            public override Color MenuItemSelectedGradientBegin => Hover;
            public override Color MenuItemSelectedGradientEnd => Hover;
            public override Color MenuItemPressedGradientBegin => Surface;
            public override Color MenuItemPressedGradientMiddle => Surface;
            public override Color MenuItemPressedGradientEnd => Surface;
            public override Color ImageMarginGradientBegin => Surface;
            public override Color ImageMarginGradientMiddle => Surface;
            public override Color ImageMarginGradientEnd => Surface;
            public override Color CheckBackground => Accent;
            public override Color CheckSelectedBackground => Accent;
            public override Color CheckPressedBackground => Accent;
            public override Color ButtonSelectedBorder => Border;
            public override Color SeparatorDark => Border;
            public override Color SeparatorLight => Border;
        }
    }
}

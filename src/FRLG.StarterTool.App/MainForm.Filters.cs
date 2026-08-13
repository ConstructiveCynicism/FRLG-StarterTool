using FRLG.StarterTool.Core.Pokemon;
using FRLG.StarterTool.Core.Settings;

namespace FRLG.StarterTool.App;

public partial class MainForm
{
    private static AppSettings Settings => StarterTool.Settings;

    private void InitializeFilters()
    {
        MenuFilters.DropDownOpening += (_, _) => BuildFiltersMenu();

        MenuFilters.DropDownItems.Add(new ToolStripMenuItem("(no saved filters)") { Enabled = false });
    }

    private void ApplyFilter(FilterPreset filter)
    {
        SelectSpecies(filter.SpeciesId);
        TextBoxMinFrame.Text = filter.MinFrame;
        TextBoxMaxFrame.Text = filter.MaxFrame;

        for (int i = 0; i < CheckBoxNatures.Length && i < filter.Natures.Length; i++)
        {
            CheckBoxNatures[i].Checked = filter.Natures[i];
        }

        ApplyStatPack(0, filter.IvMinus);
        ApplyStatPack(1, filter.IvNeutral);
        ApplyStatPack(2, filter.IvPlus);
    }

    private FilterPreset CaptureFilter(string name = "")
    {
        var natures = new bool[Nature.NatureCount];
        for (int i = 0; i < CheckBoxNatures.Length && i < natures.Length; i++)
        {
            natures[i] = CheckBoxNatures[i].Checked;
        }

        return new FilterPreset
        {
            Name = name,
            SpeciesId = SelectedSpecies.Id,
            MinFrame = TextBoxMinFrame.Text,
            MaxFrame = TextBoxMaxFrame.Text,
            Natures = natures,
            IvMinus = ReadStatPack(0).ToArray(),
            IvNeutral = ReadStatPack(1).ToArray(),
            IvPlus = ReadStatPack(2).ToArray()
        };
    }

    private void BuildFiltersMenu()
    {
        MenuFilters.DropDownItems.Clear();

        AppSettings settings = Settings;
        FilterPreset current = CaptureFilter();
        FilterPreset? active = settings.FindPreset(settings.ActivePreset);

        if (settings.Presets.Count == 0)
        {
            MenuFilters.DropDownItems.Add(new ToolStripMenuItem("(no saved filters)") { Enabled = false });
        }
        else
        {
            foreach (FilterPreset preset in settings.Presets)
            {
                FilterPreset target = preset;
                var item = new ToolStripMenuItem(Escape(preset.Name))
                {
                    Checked = preset.SameFilterAs(current)
                };
                item.Click += (_, _) => LoadFilter(target);
                MenuFilters.DropDownItems.Add(item);
            }
        }

        MenuFilters.DropDownItems.Add(new ToolStripSeparator());

        var saveAs = new ToolStripMenuItem("Save Current As…");
        saveAs.Click += (_, _) => SaveFilterAs();
        MenuFilters.DropDownItems.Add(saveAs);

        var update = new ToolStripMenuItem(active == null ? "Update Filter" : $"Update \"{Escape(active.Name)}\"")
        {
            Enabled = active != null
        };
        update.Click += (_, _) => UpdateActiveFilter();
        MenuFilters.DropDownItems.Add(update);

        var rename = new ToolStripMenuItem(active == null ? "Rename Filter…" : $"Rename \"{Escape(active.Name)}\"…")
        {
            Enabled = active != null
        };
        rename.Click += (_, _) => RenameActiveFilter();
        MenuFilters.DropDownItems.Add(rename);

        var delete = new ToolStripMenuItem(active == null ? "Delete Filter…" : $"Delete \"{Escape(active.Name)}\"…")
        {
            Enabled = active != null
        };
        delete.Click += (_, _) => DeleteActiveFilter();
        MenuFilters.DropDownItems.Add(delete);

        Theme.ApplyMenu(MenuFilters.DropDownItems);
    }

    private static string Escape(string name) => name.Replace("&", "&&");

    private void LoadFilter(FilterPreset preset)
    {
        ApplyFilter(preset);
        Settings.ActivePreset = preset.Name;

        if (StarterTool.IsTimerRunning) return;

        ReopenTrainerIdCaret();
        FocusTrainerId();
    }

    private void SaveFilterAs()
    {
        AppSettings settings = Settings;
        string? name = PromptForName("Save Filter", "Name for this filter:", settings.ActivePreset);
        if (name == null) return;

        FilterPreset? existing = settings.FindPreset(name);
        if (existing != null && !Confirm($"A filter named \"{existing.Name}\" already exists. Overwrite it?", "Save Filter"))
        {
            return;
        }

        FilterPreset saved = CaptureFilter(name);
        if (existing == null)
        {
            settings.Presets.Add(saved);
        }
        else
        {
            settings.Presets[settings.Presets.IndexOf(existing)] = saved;
        }

        settings.ActivePreset = saved.Name;
        StarterTool.SaveSettings();
    }

    private void UpdateActiveFilter()
    {
        AppSettings settings = Settings;
        FilterPreset? active = settings.FindPreset(settings.ActivePreset);
        if (active == null) return;

        settings.Presets[settings.Presets.IndexOf(active)] = CaptureFilter(active.Name);
        StarterTool.SaveSettings();
    }

    private void RenameActiveFilter()
    {
        AppSettings settings = Settings;
        FilterPreset? active = settings.FindPreset(settings.ActivePreset);
        if (active == null) return;

        string? name = PromptForName("Rename Filter", "New name for this filter:", active.Name);
        if (name == null || FilterPreset.NameEquals(name, active.Name)) return;

        FilterPreset? clash = settings.FindPreset(name);
        if (clash != null && !Confirm($"A filter named \"{clash.Name}\" already exists. Overwrite it?", "Rename Filter"))
        {
            return;
        }
        if (clash != null)
        {
            settings.Presets.Remove(clash);
        }

        active.Name = name;
        settings.ActivePreset = name;
        StarterTool.SaveSettings();
    }

    private void DeleteActiveFilter()
    {
        AppSettings settings = Settings;
        FilterPreset? active = settings.FindPreset(settings.ActivePreset);
        if (active == null) return;

        if (!Confirm($"Delete the filter \"{active.Name}\"?", "Delete Filter")) return;

        settings.Presets.Remove(active);
        settings.ActivePreset = "";
        StarterTool.SaveSettings();
    }

    private string? PromptForName(string title, string prompt, string initialValue)
        => StarterTool.Modal(() =>
        {
            using var dialog = new TextPromptDialog(title, prompt, initialValue);
            return dialog.ShowDialog(this) == DialogResult.OK ? dialog.Value : null;
        });

    private bool Confirm(string message, string title)
        => StarterTool.Modal(() => MessageBox.Show(
            this, message, title, MessageBoxButtons.YesNo, MessageBoxIcon.Question)) == DialogResult.Yes;
}

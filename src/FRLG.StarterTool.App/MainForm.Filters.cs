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

        ShowRanges(filter.Clone().Normalize().Ranges);
    }

    private FilterPreset CaptureFilter(string name = "") =>
        new FilterPreset
        {
            Name = name,
            SpeciesId = SelectedSpecies.Id,
            MinFrame = TextBoxMinFrame.Text,
            MaxFrame = TextBoxMaxFrame.Text,
            Ranges = CaptureRanges()
        }.Normalize();

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

        MenuFilters.DropDownItems.Add(new ToolStripSeparator());

        var import = new ToolStripMenuItem("Import Filter…");
        import.Click += (_, _) => ImportFilter();
        MenuFilters.DropDownItems.Add(import);

        var export = new ToolStripMenuItem(active == null ? "Export Filter…" : $"Export \"{Escape(active.Name)}\"…")
        {
            Enabled = active != null
        };
        export.Click += (_, _) => ExportActiveFilter();
        MenuFilters.DropDownItems.Add(export);

        Theme.ApplyMenu(MenuFilters.DropDownItems);
    }

    private static string Escape(string name) => name.Replace("&", "&&");

    private void LoadFilter(FilterPreset preset, bool focusTrainerId = true)
    {
        ApplyFilter(preset);
        Settings.ActivePreset = preset.Name;
        FillFilterList();

        if (!focusTrainerId || StarterTool.IsTimerRunning) return;

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

    private void ImportFilter()
    {
        AppSettings settings = Settings;
        string? path = BrowseOpen("Import filter", "Filter (*.json)|*.json|All files (*.*)|*.*");
        if (path == null) return;

        FilterPreset? preset;
        try
        {
            preset = PresetFile.Read<FilterPreset>(path);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or System.Text.Json.JsonException)
        {
            Fail($"Could not read \"{Path.GetFileName(path)}\": {ex.Message}", "Import filter");
            return;
        }
        if (preset == null)
        {
            Fail($"\"{Path.GetFileName(path)}\" holds no filter.", "Import filter");
            return;
        }

        preset.Normalize();
        if (preset.Name.Length == 0) preset.Name = Path.GetFileNameWithoutExtension(path);

        FilterPreset? existing = settings.FindPreset(preset.Name);
        if (existing != null && !Confirm($"A filter named \"{existing.Name}\" already exists. Overwrite it?", "Import filter"))
        {
            return;
        }

        if (existing == null)
        {
            settings.Presets.Add(preset);
        }
        else
        {
            settings.Presets[settings.Presets.IndexOf(existing)] = preset;
        }

        StarterTool.SaveSettings();
        LoadFilter(preset, focusTrainerId: false);
    }

    private void ExportActiveFilter()
    {
        AppSettings settings = Settings;
        FilterPreset? active = settings.FindPreset(settings.ActivePreset);
        if (active == null) return;

        string? path = BrowseSave("Export filter", "Filter (*.json)|*.json|All files (*.*)|*.*", active.Name);
        if (path == null) return;

        try
        {
            PresetFile.Write(path, active);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            Fail($"Could not write \"{Path.GetFileName(path)}\": {ex.Message}", "Export filter");
        }
    }

    private string? BrowseOpen(string title, string filter)
        => StarterTool.Modal(() =>
        {
            using var dialog = new OpenFileDialog { Title = title, Filter = filter, CheckFileExists = true };
            return dialog.ShowDialog(this) == DialogResult.OK ? dialog.FileName : null;
        });

    private string? BrowseSave(string title, string filter, string name)
        => StarterTool.Modal(() =>
        {
            using var dialog = new SaveFileDialog
            {
                Title = title,
                Filter = filter,
                FileName = PresetFileName(name),
                OverwritePrompt = true
            };
            return dialog.ShowDialog(this) == DialogResult.OK ? dialog.FileName : null;
        });

    public static string PresetFileName(string name)
    {
        char[] illegal = Path.GetInvalidFileNameChars();
        string stem = new string(name.Trim().Select(c => illegal.Contains(c) ? '_' : c).ToArray()).Trim();
        return (stem.Length == 0 ? "preset" : stem) + ".json";
    }

    private void Fail(string message, string title)
        => StarterTool.Modal(() => MessageBox.Show(
            this, message, title, MessageBoxButtons.OK, MessageBoxIcon.Warning));

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

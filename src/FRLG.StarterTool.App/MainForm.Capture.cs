using System.Globalization;
using FRLG.StarterTool.App.Capture;
using FRLG.StarterTool.Core.Settings;
using FRLG.StarterTool.Core.Video;

namespace FRLG.StarterTool.App;

public partial class MainForm
{
    private void InitializeCapture()
    {
        StarterTool.Capture.Ready += ShowCapture;

        ComboBoxEncounterRoute.SelectedIndexChanged += (_, _) => RefreshCapture();

        CaptureView.CloseRequested += (_, _) => CloseCapture();
        CaptureView.EditDelayRequested += EditCaptureDelay;
        CaptureView.SaveViewRequested += view => SaveCaptureReference(null, view, 0);
        CaptureView.SaveReferenceRequested += SaveCaptureReference;
        CaptureView.ClearRequested += (_, _) => ClearCaptureReferences();
    }

    public void RefreshCapture()
    {
        if (StarterTool.Settings == null) return;
        StarterTool.Capture.Refresh(StarterTool.Settings, SelectedEncounterRoute.Length > 0);
    }

    private void ShowCapture(CaptureResult result)
    {
        EncounterRoutePreset? route = EncounterPanel.FindRoute(result.RouteName);
        ViewState view = route is { ReferenceZoom: > 0 }
            ? new ViewState(route.ReferenceZoom, route.ReferenceCenterX, route.ReferenceCenterY)
            : ViewState.Fit;

        CaptureView.Show(result, LoadReferences(route), view);
        CaptureView.Visible = true;
        CaptureView.BringToFront();
    }

    public void CloseCapture()
    {
        if (!CaptureView.Visible && !CaptureView.HasResult) return;
        CaptureView.Visible = false;
        CaptureView.Clear();
    }

    private string CaptureRouteName => CaptureView.RouteName ?? SelectedEncounterRoute;

    private static List<(Bitmap Image, int Frames)> LoadReferences(EncounterRoutePreset? route)
    {
        var loaded = new List<(Bitmap, int)>();
        if (route == null) return loaded;

        foreach (CaptureReference reference in route.References)
        {
            string? path = PresetLibrary.Default.ReferencePath(route.Name, reference);
            if (path == null) continue;
            try
            {
                using var stream = new MemoryStream(File.ReadAllBytes(path));
                using var image = Image.FromStream(stream);
                loaded.Add((new Bitmap(image), reference.Frames));
            }
            catch (Exception e) when (e is IOException or UnauthorizedAccessException or ArgumentException)
            {
                ContextSession.Log("capture: reference \"" + path + "\" could not be read - " + e.Message);
            }
        }
        return loaded;
    }

    private void SaveCaptureReference(CapturedFrame? frame, ViewState view, int delayMs)
    {
        string routeName = CaptureRouteName;
        if (routeName.Length == 0)
        {
            CaptureView.Notice("No route on the Route row to save the reference to.");
            return;
        }

        string? file = null;
        int frames = 0;
        if (frame != null)
        {
            if (AskReferenceFrames(CaptureView.PressFramesOff()) is not int answer) return;
            frames = answer;

            EncounterRoutePreset? owner = EncounterPanel.FindRoute(routeName);
            if (owner == null)
            {
                CaptureView.Notice($"No saved route \"{routeName}\" to save the reference to.");
                return;
            }
            file = CaptureReference.FileName(frames);
            try
            {
                string directory = PresetLibrary.Default.EnsureRouteDirectory(owner);
                using Bitmap bitmap = CaptureSession.ToBitmap(frame);
                bitmap.Save(Path.Combine(directory, file), System.Drawing.Imaging.ImageFormat.Png);
            }
            catch (Exception e) when (e is IOException or UnauthorizedAccessException or System.Runtime.InteropServices.ExternalException)
            {
                CaptureView.Notice("The reference could not be saved: " + e.Message);
                return;
            }
        }

        bool replaced = false;
        int delaySet = 0;
        bool saved = EncounterPanel.EditCapture(routeName, route =>
        {
            if (file != null)
            {
                if (route.VideoDelayMs == 0 && delayMs != 0)
                {
                    delaySet = Math.Clamp(delayMs, EncounterRoutePreset.MinVideoDelayMs, EncounterRoutePreset.MaxVideoDelayMs);
                    route.VideoDelayMs = delaySet;
                }
                replaced = route.References.Any(reference => reference.Frames == frames);
                route.References.RemoveAll(reference => reference.Frames == frames);
                route.References.Add(new CaptureReference { Image = file, Frames = frames });
            }
            view = view.Normalize();
            route.ReferenceZoom = view.Zoom;
            route.ReferenceCenterX = view.CenterX;
            route.ReferenceCenterY = view.CenterY;
        });
        if (!saved) return;

        if (file != null)
        {
            CaptureView.SetReferences(LoadReferences(EncounterPanel.FindRoute(routeName)), frames);
        }

        CaptureView.Notice(file != null
            ? $"Reference ({CaptureReference.Describe(frames)}) {(replaced ? "replaced" : "saved")} and view saved to \"{routeName}\""
                + (delaySet != 0 ? $", video delay set to {delaySet} ms." : ".")
            : $"View saved to \"{routeName}\".");
        if (delaySet != 0) ContextSession.Log($"capture: video delay for route \"{routeName}\" set to {delaySet} ms off its first reference");
        ContextSession.Log(file != null
            ? $"capture: reference ({frames:+0;-0;0} frames) {(replaced ? "replaced" : "saved")} in route \"{routeName}\" as {file}"
            : $"capture: view saved to route \"{routeName}\"");
    }

    private int? AskReferenceFrames(int suggested) =>
        AskNumber("Save reference", "Frames off target (0 on target, -1 early, +1 late):", "Save",
            suggested.ToString("+0;-0;0", CultureInfo.InvariantCulture), -CaptureReference.MaxFrames, CaptureReference.MaxFrames);

    private int? AskNumber(string title, string caption, string accept, string initial, int min, int max)
    {
        using var dialog = new Form
        {
            Text = title,
            FormBorderStyle = FormBorderStyle.FixedDialog,
            StartPosition = FormStartPosition.CenterParent,
            MinimizeBox = false,
            MaximizeBox = false,
            ShowInTaskbar = false,
            ClientSize = new Size(340, 118),
        };
        dialog.Controls.Add(new Label
        {
            Text = caption,
            Location = new Point(12, 14),
            AutoSize = true,
        });
        var box = new ThemedTextBox
        {
            Numeric = true,
            Location = new Point(12, 40),
            Width = 80,
            Text = initial,
        };
        dialog.Controls.Add(box);
        var ok = new ThemedButton
        {
            Text = accept, Size = new Size(76, 28), Location = new Point(340 - 12 - 76 - 8 - 76, 78),
            DialogResult = DialogResult.OK,
        };
        var cancel = new ThemedButton
        {
            Text = "Cancel", Size = new Size(76, 28), Location = new Point(340 - 12 - 76, 78),
            DialogResult = DialogResult.Cancel,
        };
        dialog.Controls.Add(ok);
        dialog.Controls.Add(cancel);
        dialog.AcceptButton = ok;
        dialog.CancelButton = cancel;
        Theme.Apply(dialog);

        while (true)
        {
            box.SelectAll();
            if (StarterTool.Modal(() => dialog.ShowDialog(this)) != DialogResult.OK) return null;
            if (int.TryParse(box.Text, NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out int value)
                && value >= min && value <= max)
            {
                return value;
            }
        }
    }

    private void ClearCaptureReferences()
    {
        string routeName = CaptureRouteName;
        EncounterRoutePreset? route = EncounterPanel.FindRoute(routeName);
        if (route == null) return;

        int count = route.References.Count;
        string message =
            $"This deletes the {count} reference picture{(count == 1 ? "" : "s")} saved for \"{routeName}\" "
            + "and its calibrated video delay.\n\nThe route's next manip run calibrates again. Continue?";
        if (StarterTool.Modal(() => MessageBox.Show(this, message, "Clear references and calibration",
                MessageBoxButtons.OKCancel, MessageBoxIcon.Warning, MessageBoxDefaultButton.Button2)) != DialogResult.OK)
        {
            return;
        }

        List<string> files = route.References
            .Select(reference => PresetLibrary.Default.ReferencePath(routeName, reference))
            .OfType<string>()
            .ToList();
        if (!EncounterPanel.EditCapture(routeName, edited =>
            {
                edited.References.Clear();
                edited.VideoDelayMs = 0;
            }))
        {
            return;
        }
        DeleteReferenceFiles(files);

        CaptureView.SetReferences(Array.Empty<(Bitmap, int)>(), null);
        CaptureView.Notice($"References and calibration cleared for \"{routeName}\": its next run calibrates.");
        ContextSession.Log($"capture: references ({count}) and video delay cleared for route \"{routeName}\"");
    }

    private static void DeleteReferenceFiles(IEnumerable<string> paths)
    {
        foreach (string path in paths)
        {
            try
            {
                File.Delete(path);
            }
            catch (Exception e) when (e is IOException or UnauthorizedAccessException)
            {
                ContextSession.Log("capture: reference \"" + path + "\" could not be deleted - " + e.Message);
            }
        }
    }

    private void EditCaptureDelay(int suggestedMs)
    {
        EncounterRoutePreset? route = EncounterPanel.FindRoute(CaptureRouteName);
        if (route == null)
        {
            CaptureView.Notice("No saved route to keep the delay on.");
            return;
        }
        if (AskNumber("Video delay", $"Video delay for \"{route.Name}\" in ms (0 calibrates the next run):", "Set",
                suggestedMs.ToString(CultureInfo.InvariantCulture),
                EncounterRoutePreset.MinVideoDelayMs, EncounterRoutePreset.MaxVideoDelayMs) is int delayMs)
        {
            SetCaptureDelay(delayMs);
        }
    }

    private void SetCaptureDelay(int delayMs)
    {
        string routeName = CaptureRouteName;
        delayMs = Math.Clamp(delayMs, EncounterRoutePreset.MinVideoDelayMs, EncounterRoutePreset.MaxVideoDelayMs);
        if (!EncounterPanel.EditCapture(routeName, route => route.VideoDelayMs = delayMs))
        {
            CaptureView.Notice("No saved route to keep the delay on.");
            return;
        }
        StarterTool.SaveSettings();
        CaptureView.Notice(delayMs == 0
            ? $"Video delay for \"{routeName}\" cleared: its next run calibrates."
            : $"Video delay for \"{routeName}\" set to {delayMs} ms.");
        ContextSession.Log($"capture: video delay for route \"{routeName}\" set to {delayMs} ms");
    }
}

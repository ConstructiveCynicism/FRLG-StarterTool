using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using FRLG.StarterTool.Core.Video;

namespace FRLG.StarterTool.App;

public sealed class CropDialog : Form
{
    private readonly PicturePanel _picture;
    private readonly Label _readout;

    public CropDialog(Rectangle crop, bool gamePixels = false)
    {
        Text = "Capture crop";
        FormBorderStyle = FormBorderStyle.Sizable;
        StartPosition = FormStartPosition.CenterParent;
        MinimizeBox = false;
        ShowInTaskbar = false;
        ClientSize = new Size(720, 540);
        MinimumSize = new Size(400, 320);

        Controls.Add(new Label
        {
            Text = "Drag a rectangle over the game area.",
            Location = new Point(12, 10),
            AutoSize = true
        });

        _picture = new PicturePanel
        {
            Location = new Point(12, 32),
            Size = new Size(ClientSize.Width - 24, ClientSize.Height - 32 - 50),
            Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right | AnchorStyles.Bottom,
            Crop = crop
        };
        _picture.CropChanged += (_, _) => ShowReadout();
        Controls.Add(_picture);

        var preview = new ThemedCheckBox
        {
            Text = "Preview 240x160",
            AutoSize = true,
            Checked = gamePixels,
            Anchor = AnchorStyles.Top | AnchorStyles.Right
        };
        Controls.Add(preview);
        preview.Location = new Point(ClientSize.Width - 12 - preview.PreferredSize.Width, 8);
        _picture.ShowGamePixels = gamePixels;
        preview.CheckedChanged += (_, _) =>
        {
            _picture.ShowGamePixels = preview.Checked;
            ShowReadout();
        };

        _readout = new Label
        {
            Location = new Point(12, ClientSize.Height - 38),
            AutoSize = true,
            MaximumSize = new Size(ClientSize.Width - 24 - 76 - 8 - 76 - 8 - 100 - 8, 0),
            Anchor = AnchorStyles.Left | AnchorStyles.Bottom
        };
        Controls.Add(_readout);

        var whole = new ThemedButton
        {
            Text = "Whole frame",
            Size = new Size(100, 28),
            Location = new Point(ClientSize.Width - 12 - 76 - 8 - 76 - 8 - 100, ClientSize.Height - 40),
            Anchor = AnchorStyles.Right | AnchorStyles.Bottom
        };
        whole.Click += (_, _) =>
        {
            _picture.Crop = Rectangle.Empty;
            ShowReadout();
        };
        var ok = new ThemedButton
        {
            Text = "OK",
            Size = new Size(76, 28),
            Location = new Point(ClientSize.Width - 12 - 76 - 8 - 76, ClientSize.Height - 40),
            Anchor = AnchorStyles.Right | AnchorStyles.Bottom,
            DialogResult = DialogResult.OK
        };
        var cancel = new ThemedButton
        {
            Text = "Cancel",
            Size = new Size(76, 28),
            Location = new Point(ClientSize.Width - 12 - 76, ClientSize.Height - 40),
            Anchor = AnchorStyles.Right | AnchorStyles.Bottom,
            DialogResult = DialogResult.Cancel
        };
        Controls.Add(whole);
        Controls.Add(ok);
        Controls.Add(cancel);
        AcceptButton = ok;
        CancelButton = cancel;

        ShowReadout();
        Theme.Apply(this);
    }

    public Rectangle Crop => _picture.Crop;

    public void ShowFrame(Bitmap frame)
    {
        if (IsDisposed)
        {
            frame.Dispose();
            return;
        }
        _picture.SetFrame(frame);
        ShowReadout();
    }

    private void ShowReadout()
    {
        Size source = _picture.FrameSize;
        string sourceText = source.IsEmpty ? "waiting for the source..." : $"source {source.Width}x{source.Height}";
        Rectangle crop = _picture.Crop;
        _readout.Text = crop.Width <= 0 || crop.Height <= 0
            ? $"Whole frame ({sourceText})"
            : $"{crop.Width}x{crop.Height} at {crop.X},{crop.Y} ({sourceText}) - "
                + (crop.Width / (double)GamePixels.Width).ToString("0.##", System.Globalization.CultureInfo.InvariantCulture) + " x "
                + (crop.Height / (double)GamePixels.Height).ToString("0.##", System.Globalization.CultureInfo.InvariantCulture)
                + " source pixels a game pixel"
                + (GamePixels.IsGameShaped(crop.Width, crop.Height)
                    ? ""
                    : " - not 3:2: fine if the game is stretched to fill it, otherwise crop to the game inside any border (a DS has one)");
    }

    private sealed class PicturePanel : Control
    {
        private Bitmap? _frame;
        private Bitmap? _sampled;
        private Point? _dragStart;
        private bool _showGamePixels;

        public PicturePanel()
        {
            SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint
                | ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
        }

        public event EventHandler? CropChanged;

        public Rectangle Crop
        {
            get => _crop;
            set
            {
                _crop = value;
                if (_showGamePixels) Resample();
            }
        }

        private Rectangle _crop;

        public Size FrameSize => _frame?.Size ?? Size.Empty;

        public bool ShowGamePixels
        {
            get => _showGamePixels;
            set
            {
                _showGamePixels = value;
                Resample();
                Invalidate();
            }
        }

        public void SetFrame(Bitmap frame)
        {
            _frame?.Dispose();
            _frame = frame;
            Resample();
            Invalidate();
        }

        private unsafe void Resample()
        {
            _sampled?.Dispose();
            _sampled = null;
            if (!_showGamePixels || _frame == null) return;

            Rectangle clip = Crop.Width > 0 && Crop.Height > 0
                ? Rectangle.Intersect(Crop, new Rectangle(Point.Empty, _frame.Size))
                : new Rectangle(Point.Empty, _frame.Size);
            if (clip.IsEmpty) return;

            BitmapData data = _frame.LockBits(new Rectangle(Point.Empty, _frame.Size), ImageLockMode.ReadOnly, PixelFormat.Format32bppRgb);
            var sampled = new byte[GamePixels.Bytes];
            try
            {
                var source = new ReadOnlySpan<byte>((void*)data.Scan0, data.Stride * _frame.Height);
                GamePixels.Sample(source, data.Stride, clip.X, clip.Y, clip.Width, clip.Height, sampled);
            }
            finally
            {
                _frame.UnlockBits(data);
            }

            var bitmap = new Bitmap(GamePixels.Width, GamePixels.Height, PixelFormat.Format32bppRgb);
            BitmapData target = bitmap.LockBits(new Rectangle(0, 0, GamePixels.Width, GamePixels.Height), ImageLockMode.WriteOnly, PixelFormat.Format32bppRgb);
            try
            {
                for (int y = 0; y < GamePixels.Height; y++)
                {
                    System.Runtime.InteropServices.Marshal.Copy(sampled, y * GamePixels.Width * 4, target.Scan0 + y * target.Stride, GamePixels.Width * 4);
                }
            }
            finally
            {
                bitmap.UnlockBits(target);
            }
            _sampled = bitmap;
        }

        private RectangleF ImageBounds()
        {
            if (_frame == null) return RectangleF.Empty;
            float scale = Math.Min(Width / (float)_frame.Width, Height / (float)_frame.Height);
            float width = _frame.Width * scale, height = _frame.Height * scale;
            return new RectangleF((Width - width) / 2f, (Height - height) / 2f, width, height);
        }

        private Point ToSource(Point at)
        {
            RectangleF bounds = ImageBounds();
            if (_frame == null || bounds.Width <= 0) return Point.Empty;
            float scale = bounds.Width / _frame.Width;
            int x = (int)Math.Round((at.X - bounds.X) / scale);
            int y = (int)Math.Round((at.Y - bounds.Y) / scale);
            return new Point(Math.Clamp(x, 0, _frame.Width), Math.Clamp(y, 0, _frame.Height));
        }

        protected override void OnMouseDown(MouseEventArgs e)
        {
            base.OnMouseDown(e);
            if (e.Button != MouseButtons.Left || _frame == null || _showGamePixels) return;
            _dragStart = ToSource(e.Location);
            Capture = true;
        }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            base.OnMouseMove(e);
            if (_dragStart is not { } start) return;
            Point end = ToSource(e.Location);
            Crop = Rectangle.FromLTRB(
                Math.Min(start.X, end.X), Math.Min(start.Y, end.Y),
                Math.Max(start.X, end.X), Math.Max(start.Y, end.Y));
            CropChanged?.Invoke(this, EventArgs.Empty);
            Invalidate();
        }

        protected override void OnMouseUp(MouseEventArgs e)
        {
            base.OnMouseUp(e);
            _dragStart = null;
            Capture = false;
            if (Crop.Width < 4 || Crop.Height < 4)
            {
                Crop = Rectangle.Empty;
                CropChanged?.Invoke(this, EventArgs.Empty);
                Invalidate();
            }
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            Graphics g = e.Graphics;
            g.Clear(Theme.ListBack);
            if (_frame == null)
            {
                TextRenderer.DrawText(g, "No picture from the source yet.", Font, ClientRectangle, Theme.DimText,
                    TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);
                return;
            }

            if (_sampled != null)
            {
                int scale = Math.Max(1, Math.Min(Width / GamePixels.Width, Height / GamePixels.Height));
                var dest = new Rectangle((Width - GamePixels.Width * scale) / 2, (Height - GamePixels.Height * scale) / 2,
                    GamePixels.Width * scale, GamePixels.Height * scale);
                g.InterpolationMode = InterpolationMode.NearestNeighbor;
                g.PixelOffsetMode = PixelOffsetMode.Half;
                g.DrawImage(_sampled, dest);
                return;
            }

            RectangleF bounds = ImageBounds();
            g.InterpolationMode = InterpolationMode.NearestNeighbor;
            g.PixelOffsetMode = PixelOffsetMode.Half;
            g.DrawImage(_frame, bounds);

            if (Crop.Width > 0 && Crop.Height > 0)
            {
                float scale = bounds.Width / _frame.Width;
                var rect = new RectangleF(bounds.X + Crop.X * scale, bounds.Y + Crop.Y * scale,
                    Crop.Width * scale, Crop.Height * scale);

                using (var dim = new SolidBrush(Color.FromArgb(150, 0, 0, 0)))
                using (var outside = new Region(bounds))
                {
                    outside.Exclude(rect);
                    g.FillRegion(dim, outside);
                }
                using var pen = new Pen(Theme.NpcStepArrow, 2f);
                g.DrawRectangle(pen, rect.X, rect.Y, rect.Width, rect.Height);
            }
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                _frame?.Dispose();
                _sampled?.Dispose();
            }
            base.Dispose(disposing);
        }
    }
}

using System.Runtime.InteropServices;

namespace FRLG.StarterTool.App;

public sealed class ThemedTextBox : TextBox
{
    private const int TopInset = 2;

    private bool Adjusted => BorderStyle == BorderStyle.FixedSingle && !Multiline;

    public bool Numeric { get; set; }

    protected override void OnTextChanged(EventArgs e)
    {
        if (Numeric && Text.Any(char.IsWhiteSpace))
        {
            string text = Text;
            int caret = SelectionStart;
            int before = 0;
            for (int i = 0; i < caret && i < text.Length; i++)
            {
                if (char.IsWhiteSpace(text[i])) before++;
            }

            Text = string.Concat(text.Where(c => !char.IsWhiteSpace(c)));
            SelectionStart = Math.Max(0, caret - before);
            return;
        }

        base.OnTextChanged(e);
    }

    protected override void OnHandleCreated(EventArgs e)
    {
        base.OnHandleCreated(e);

        Win32.DisableVisualStyles(Handle);
    }

    protected override void WndProc(ref Message m)
    {
        if (m.Msg == Win32.WM_NCCALCSIZE && Adjusted)
        {
            base.WndProc(ref m);
            CentreClientRect(m.LParam);
            return;
        }

        base.WndProc(ref m);

        if ((m.Msg == Win32.WM_NCPAINT || m.Msg == Win32.WM_PAINT) && Adjusted)
        {
            PaintFrame();
        }
    }

    private static void CentreClientRect(IntPtr lParam)
    {
        if (lParam == IntPtr.Zero) return;

        Win32.RECT client = Marshal.PtrToStructure<Win32.RECT>(lParam);
        if (client.Bottom - client.Top < TopInset + 1) return;

        client.Top += TopInset;
        Marshal.StructureToPtr(client, lParam, false);
    }

    public void RefreshBorder()
    {
        if (IsHandleCreated) Win32.RedrawFrame(Handle);
    }

    private void PaintFrame()
    {
        IntPtr hdc = Win32.GetWindowDC(Handle);
        if (hdc == IntPtr.Zero) return;

        try
        {
            using Graphics g = Graphics.FromHdc(hdc);

            using (var background = new SolidBrush(BackColor))
            {
                g.FillRectangle(background, 1, 1, Math.Max(0, Width - 2), TopInset);
            }

            using var pen = new Pen(Theme.Border);
            g.DrawRectangle(pen, 0, 0, Width - 1, Height - 1);
        }
        finally
        {
            Win32.ReleaseDC(Handle, hdc);
        }
    }
}

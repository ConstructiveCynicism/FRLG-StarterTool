using System.Runtime.InteropServices;

namespace FRLG.StarterTool.App;

public sealed class ThemedListView : ListView
{
    public const int RuleClearance = 2;

    private readonly HeaderWindow _header;

    public ThemedListView()
    {
        _header = new HeaderWindow(this);
    }

    protected override void WndProc(ref Message m)
    {
        if (m.Msg == Win32.WM_NOTIFY && IsColumnResize(m.LParam))
        {
            m.Result = (IntPtr)1;
            return;
        }

        base.WndProc(ref m);

        if (m.Msg == Win32.WM_PAINT) PaintEmptyArea();
    }

    private static bool IsColumnResize(IntPtr lParam)
    {
        if (lParam == IntPtr.Zero) return false;

        var header = Marshal.PtrToStructure<Win32.NMHDR>(lParam);
        return header.code is Win32.HDN_BEGINTRACKA or Win32.HDN_BEGINTRACKW
            or Win32.HDN_DIVIDERDBLCLICKA or Win32.HDN_DIVIDERDBLCLICKW;
    }

    private void PaintEmptyArea()
    {
        if (!Win32.GetClientRect(Handle, out Win32.RECT client)) return;

        int top = client.Top;
        if (_header.Handle != IntPtr.Zero && Win32.GetClientRect(_header.Handle, out Win32.RECT header))
        {
            top = header.Bottom;
        }

        if (Items.Count > 0)
        {
            top = Math.Max(top, Items[Items.Count - 1].Bounds.Bottom);
        }

        if (top >= client.Bottom) return;

        using Graphics g = Graphics.FromHwnd(Handle);
        using var background = new SolidBrush(BackColor);
        g.FillRectangle(background, client.Left, top, client.Right - client.Left, client.Bottom - top);
    }

    protected override void OnHandleCreated(EventArgs e)
    {
        base.OnHandleCreated(e);

        Win32.SetDarkScrollBars(Handle, Theme.Dark);

        IntPtr header = Win32.SendMessage(Handle, Win32.LVM_GETHEADER, IntPtr.Zero, IntPtr.Zero);
        if (header != IntPtr.Zero)
        {
            _header.AssignHandle(header);
        }
    }

    public void RefreshHeader()
    {
        if (_header.Handle != IntPtr.Zero)
        {
            Win32.InvalidateRect(_header.Handle, IntPtr.Zero, erase: true);
        }
    }

    protected override void OnHandleDestroyed(EventArgs e)
    {
        _header.ReleaseHandle();
        base.OnHandleDestroyed(e);
    }

    private sealed class HeaderWindow : NativeWindow
    {
        private readonly ListView _owner;

        public HeaderWindow(ListView owner) => _owner = owner;

        protected override void WndProc(ref Message m)
        {
            base.WndProc(ref m);

            switch (m.Msg)
            {
                case Win32.WM_PAINT:
                    PaintTail(IntPtr.Zero);
                    break;

                case Win32.WM_PRINTCLIENT:
                    PaintTail(m.WParam);
                    break;
            }
        }

        private void PaintTail(IntPtr hdc)
        {
            int used = 0;
            foreach (ColumnHeader column in _owner.Columns)
            {
                used += column.Width;
            }

            if (!Win32.GetClientRect(Handle, out Win32.RECT bounds) || used >= bounds.Right) return;

            using Graphics g = hdc == IntPtr.Zero ? Graphics.FromHwnd(Handle) : Graphics.FromHdc(hdc);
            using var background = new SolidBrush(Theme.HeaderBack);
            g.FillRectangle(background, used, bounds.Top, bounds.Right - used, bounds.Bottom - bounds.Top);

            using var pen = new Pen(Theme.Border);
            g.DrawLine(pen, used, bounds.Bottom - 1, bounds.Right, bounds.Bottom - 1);
        }
    }
}

using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace FRLG.StarterTool.App;

public sealed class ScrollBarOutline : NativeWindow
{
    private static readonly ConditionalWeakTable<Control, ScrollBarOutline> Attached = new();

    private readonly Control _owner;

    private ScrollBarOutline(Control owner) => _owner = owner;

    public static void Attach(Control control)
    {
        if (Attached.TryGetValue(control, out _)) return;

        var outline = new ScrollBarOutline(control);
        Attached.Add(control, outline);

        control.HandleCreated += (_, _) => outline.Hook();
        if (control.IsHandleCreated) outline.Hook();
    }

    private void Hook()
    {
        if (Handle == _owner.Handle) return;
        if (Handle != IntPtr.Zero) ReleaseHandle();
        AssignHandle(_owner.Handle);
        Win32.RedrawFrame(Handle);
        _owner.Invalidate();
    }

    protected override void WndProc(ref Message m)
    {
        base.WndProc(ref m);

        switch (m.Msg)
        {
            case Win32.WM_NCCALCSIZE:
                TakeGap(m.LParam);
                break;

            case Win32.WM_NCPAINT:
                PaintGap();
                break;

            case Win32.WM_PAINT:
                PaintLine();
                break;
        }
    }

    private (bool Vertical, bool Horizontal) Bars()
    {
        int style = Win32.GetWindowLong(Handle, Win32.GWL_STYLE);
        return ((style & Win32.WS_VSCROLL) != 0, (style & Win32.WS_HSCROLL) != 0);
    }

    private void TakeGap(IntPtr lParam)
    {
        if (lParam == IntPtr.Zero) return;

        (bool vertical, bool horizontal) = Bars();

        Win32.RECT client = Marshal.PtrToStructure<Win32.RECT>(lParam);
        if (vertical && client.Right - client.Left > 1) client.Right--;
        if (horizontal && client.Bottom - client.Top > 1) client.Bottom--;
        Marshal.StructureToPtr(client, lParam, false);
    }

    private void PaintGap()
    {
        (bool vertical, bool horizontal) = Bars();
        if (!vertical && !horizontal) return;
        if (!Win32.GetWindowRect(Handle, out Win32.RECT window)) return;

        Rectangle? v = vertical ? BarRect(Win32.OBJID_VSCROLL, window) : null;
        Rectangle? h = horizontal ? BarRect(Win32.OBJID_HSCROLL, window) : null;

        IntPtr hdc = Win32.GetWindowDC(Handle);
        if (hdc == IntPtr.Zero) return;

        try
        {
            using Graphics g = Graphics.FromHdc(hdc);
            using var ground = new SolidBrush(_owner.BackColor);

            if (v is { } bar)
            {
                int bottom = h is { } under ? under.Bottom + 1 : bar.Bottom;
                g.FillRectangle(ground, bar.Right, bar.Top, 1, bottom - bar.Top);
            }

            if (h is { } row)
            {
                int right = v is { } beside ? beside.Right + 1 : row.Right;
                g.FillRectangle(ground, row.Left, row.Bottom, right - row.Left, 1);
            }
        }
        finally
        {
            Win32.ReleaseDC(Handle, hdc);
        }
    }

    private Rectangle? BarRect(int id, Win32.RECT window)
    {
        var info = new Win32.SCROLLBARINFO { cbSize = Marshal.SizeOf<Win32.SCROLLBARINFO>() };
        if (!Win32.GetScrollBarInfo(Handle, id, ref info)) return null;

        Win32.RECT r = info.rcScrollBar;
        if (r.Right <= r.Left || r.Bottom <= r.Top) return null;
        return Rectangle.FromLTRB(r.Left - window.Left, r.Top - window.Top, r.Right - window.Left, r.Bottom - window.Top);
    }

    public static void PaintHeader(Graphics g, ListView owner, IntPtr headerHandle, Win32.RECT header)
    {
        int style = Win32.GetWindowLong(owner.Handle, Win32.GWL_STYLE);
        if ((style & Win32.WS_VSCROLL) == 0) return;
        if (!Win32.GetClientRect(owner.Handle, out Win32.RECT view)) return;
        Win32.MapWindowPoints(owner.Handle, headerHandle, ref view, 2);

        int right = view.Right - 1;
        if (right <= header.Left) return;

        using var pen = new Pen(Theme.Border);
        g.DrawLine(pen, right, header.Top, right, header.Bottom - 1);
    }

    private void PaintLine()
    {
        (bool vertical, bool horizontal) = Bars();
        if (!vertical && !horizontal) return;

        if (!Win32.GetClientRect(Handle, out Win32.RECT client)) return;

        int top = client.Top;
        if (_owner is ListView)
        {
            IntPtr header = Win32.SendMessage(Handle, Win32.LVM_GETHEADER, IntPtr.Zero, IntPtr.Zero);
            if (header != IntPtr.Zero && Win32.GetClientRect(header, out Win32.RECT bounds)) top = bounds.Bottom;
        }

        int right = client.Right - 1;
        int bottom = client.Bottom - 1;
        if (right <= client.Left || bottom <= top) return;

        using Graphics g = Graphics.FromHwnd(Handle);
        using var pen = new Pen(Theme.Border);
        if (vertical) g.DrawLine(pen, right, top, right, bottom);
        if (horizontal) g.DrawLine(pen, client.Left, bottom, right, bottom);
    }
}

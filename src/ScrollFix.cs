using System;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace RobloxMultiHub
{
    // Makes the mouse wheel always scroll the page, even when the cursor is over a text box.
    // Windows normally sends the wheel to whatever control is under the pointer, so hovering a
    // text field "traps" the scroll. This redirects the wheel to the nearest scrollable panel,
    // UNLESS the control under the cursor is a text box you have actually clicked into (focused) -
    // then it scrolls that box, as you'd expect while editing.
    public class WheelRedirect : IMessageFilter
    {
        const int WM_MOUSEWHEEL = 0x020A;

        public bool PreFilterMessage(ref Message m)
        {
            if (m.Msg != WM_MOUSEWHEEL) return false;
            try
            {
                POINT pt; pt.X = (short)((long)m.LParam & 0xFFFF); pt.Y = (short)(((long)m.LParam >> 16) & 0xFFFF);
                Control under = Control.FromHandle(WindowFromPoint(pt));
                if (under == null) return false;

                // the control under the cursor is a text box the user is editing: let it scroll itself
                if (under is TextBoxBase && under.Focused) return false;

                // find the nearest scrollable container above the cursor
                Control scroll = under;
                while (scroll != null)
                {
                    Panel panel = scroll as Panel;           // Panel and FlowLayoutPanel both qualify
                    if (panel != null && panel.AutoScroll) break;
                    scroll = scroll.Parent;
                }
                if (scroll == null || scroll == under) return false;   // nothing to redirect to

                // hand the wheel to the scrollable panel instead of the control under the cursor
                SendMessage(scroll.Handle, WM_MOUSEWHEEL, m.WParam, m.LParam);
                return true;
            }
            catch (Exception) { return false; }
        }

        [StructLayout(LayoutKind.Sequential)]
        struct POINT { public int X; public int Y; }

        [DllImport("user32.dll")]
        static extern IntPtr WindowFromPoint(POINT p);

        [DllImport("user32.dll")]
        static extern IntPtr SendMessage(IntPtr hWnd, int msg, IntPtr wParam, IntPtr lParam);
    }
}

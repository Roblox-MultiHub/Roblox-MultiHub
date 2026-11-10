using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace RobloxMultiHub
{
    // The app's own vertical scrollbar for scrollable pages: a slim rounded overlay that replaces
    // the native bar. The native bar is hidden (ShowScrollBar), the overlay lives on the panel's
    // PARENT so it stays put while content scrolls, and dragging the thumb maps back through
    // AutoScrollPosition. Rounded, theme-aware, hover states - matches the rest of the UI.
    public class DarkVBar : Control
    {
        readonly ScrollableControl target;
        bool drag;
        int dragY0;
        int dragV0;
        bool hover;

        public DarkVBar(ScrollableControl target)
        {
            this.target = target;
            SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer, true);
            Width = 10;
            Visible = false;
            target.Parent.Controls.Add(this);
            BringToFront();
            target.HandleCreated += delegate { HideNative(); };
            target.HandleDestroyed += delegate { try { Dispose(); } catch (Exception) { } };
            target.Scroll += delegate { Sync(); };
            target.Resize += delegate { Sync(); };
            target.Layout += delegate { Sync(); };
            target.MouseWheel += delegate { Sync(); };
            target.VisibleChanged += delegate { Sync(); };      // bars of hidden pages must hide too
            Sync();
        }

        [DllImport("user32.dll")]
        static extern bool ShowScrollBar(IntPtr hWnd, int bar, bool show);

        void HideNative()
        {
            try { ShowScrollBar(target.Handle, 1 /*SB_VERT*/, false); } catch (Exception) { }
        }

        void Sync()
        {
            if (target.AutoScroll) HideNative();      // it re-shows itself whenever the range changes
            Panel p = target as Panel;
            if (p != null && !p.IsHandleCreated) return;
            if (!target.Visible || target.Parent == null) { Visible = false; return; }
            int max = target.VerticalScroll.Maximum - target.VerticalScroll.LargeChange + 1;
            if (max <= 0) { Visible = false; return; }
            Bounds = new Rectangle(
                target.Right - Width - 2,
                target.Top + 1,
                Width,
                Math.Max(20, target.Height - 4));
            Visible = target.Height < target.VerticalScroll.Maximum;
            Invalidate();
        }

        int TrackH { get { return Height - 4; } }
        int MaxRange { get { return Math.Max(1, target.VerticalScroll.Maximum - target.VerticalScroll.LargeChange + 1); } }

        int ThumbY
        {
            get
            {
                double f = (double)target.VerticalScroll.Value / MaxRange;
                return 2 + (int)((TrackH - ThumbH) * f);
            }
        }

        int ThumbH
        {
            get
            {
                double f = (double)target.Height / Math.Max(1, target.VerticalScroll.Maximum);
                return Math.Max(18, (int)(TrackH * Math.Min(1.0, f)));
            }
        }

        protected override void OnMouseDown(MouseEventArgs e)
        {
            base.OnMouseDown(e);
            Focus();
            if (e.Y < ThumbY || e.Y > ThumbY + ThumbH)
            {
                // click on the track: page toward the click
                int page = (int)((double)ThumbH / Math.Max(1, TrackH - ThumbH) * MaxRange);
                int v = target.VerticalScroll.Value + (e.Y < ThumbY ? -page : page);
                SetScroll(v);
            }
            drag = true;
            dragY0 = e.Y;
            dragV0 = target.VerticalScroll.Value;
        }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            base.OnMouseMove(e);
            if (!drag) return;
            double f = (double)(e.Y - dragY0) / Math.Max(1, TrackH - ThumbH);
            SetScroll(dragV0 + (int)(f * MaxRange));
        }

        protected override void OnMouseUp(MouseEventArgs e) { drag = false; base.OnMouseUp(e); }
        protected override void OnMouseEnter(EventArgs e) { hover = true; Invalidate(); base.OnMouseEnter(e); }
        protected override void OnMouseLeave(EventArgs e) { hover = false; Invalidate(); base.OnMouseLeave(e); }

        void SetScroll(int v)
        {
            v = Math.Max(0, Math.Min(MaxRange, v));
            // AutoScrollPosition is in client coordinates of the CONTENT, so it is negated
            target.AutoScrollPosition = new Point(0, v);
            Invalidate();
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            Graphics g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.Clear(target.Parent != null ? target.Parent.BackColor : Theme.Panel);
            Rectangle th = new Rectangle(1, ThumbY, Width - 3, ThumbH);
            using (GraphicsPath p = Shapes.Round(th, 4))
            using (SolidBrush b = new SolidBrush(drag ? Theme.Accent : (hover ? Tint.Shift(Theme.Border, 40) : Theme.Border)))
                g.FillPath(b, p);
        }
    }
}

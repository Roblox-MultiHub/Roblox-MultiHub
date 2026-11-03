using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace RobloxMultiHub
{
    // A tiny line graph of the last N values, drawn to fill itself. Used on the Monitor page.
    public class Sparkline : Control
    {
        List<double> values = new List<double>();
        double fixedMax;            // 0 = scale to the data
        public Color LineColor = Theme.Accent;

        public Sparkline()
        {
            SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
        }

        public void SetData(List<double> data, double max)
        {
            values = data ?? new List<double>();
            fixedMax = max;
            Invalidate();
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            Graphics g = e.Graphics;
            g.Clear(BackColor);
            g.SmoothingMode = SmoothingMode.AntiAlias;
            using (GraphicsPath p = Shapes.Round(new Rectangle(0, 0, Width - 1, Height - 1), 6))
            using (SolidBrush b = new SolidBrush(Theme.Dark ? Tint.Shift(BackColor, 6) : Theme.Panel2))
                g.FillPath(b, p);
            if (values.Count < 2) return;

            double max = fixedMax;
            if (max <= 0) { max = 1; foreach (double v in values) if (v > max) max = v; }
            int n = values.Count;
            float stepX = (Width - 6f) / (n - 1);
            PointF[] pts = new PointF[n];
            for (int i = 0; i < n; i++)
            {
                double v = values[i]; if (v < 0) v = 0; if (v > max) v = max;
                float x = 3 + i * stepX;
                float y = Height - 3 - (float)(v / max) * (Height - 6);
                pts[i] = new PointF(x, y);
            }
            // soft fill under the line
            using (GraphicsPath fill = new GraphicsPath())
            {
                fill.AddLines(pts);
                fill.AddLine(pts[n - 1], new PointF(pts[n - 1].X, Height));
                fill.AddLine(new PointF(pts[n - 1].X, Height), new PointF(pts[0].X, Height));
                fill.CloseFigure();
                using (SolidBrush fb = new SolidBrush(Color.FromArgb(40, LineColor))) g.FillPath(fb, fill);
            }
            using (Pen pen = new Pen(LineColor, 1.6f)) g.DrawLines(pen, pts);
        }
    }

    // Best-effort snapshot of a window, even when it is behind others. GPU apps like Roblox sometimes
    // come back black; the Monitor card then just shows the stats and a placeholder.
    public static class Thumb
    {
        const uint PW_RENDERFULLCONTENT = 0x00000002;

        [DllImport("user32.dll")]
        static extern bool PrintWindow(IntPtr hwnd, IntPtr hdc, uint flags);

        public static Bitmap Capture(IntPtr hwnd, int outW, int outH)
        {
            try
            {
                if (hwnd == IntPtr.Zero || !Native.IsWindow(hwnd) || Native.IsIconic(hwnd)) return null;
                Native.RECT r;
                if (!Native.GetWindowRect(hwnd, out r)) return null;
                int w = r.Width, h = r.Height;
                if (w < 40 || h < 40) return null;

                using (Bitmap full = new Bitmap(w, h))
                {
                    using (Graphics g = Graphics.FromImage(full))
                    {
                        IntPtr hdc = g.GetHdc();
                        bool ok;
                        try { ok = PrintWindow(hwnd, hdc, PW_RENDERFULLCONTENT); }
                        finally { g.ReleaseHdc(hdc); }
                        if (!ok) return null;
                    }
                    if (IsNearlyBlank(full)) return null;

                    // scale into the thumbnail keeping the aspect ratio
                    Bitmap thumb = new Bitmap(outW, outH);
                    using (Graphics g = Graphics.FromImage(thumb))
                    {
                        g.Clear(Theme.Bg);
                        g.InterpolationMode = InterpolationMode.HighQualityBilinear;
                        double scale = Math.Min(outW / (double)w, outH / (double)h);
                        int dw = (int)(w * scale), dh = (int)(h * scale);
                        g.DrawImage(full, (outW - dw) / 2, (outH - dh) / 2, dw, dh);
                    }
                    return thumb;
                }
            }
            catch (Exception) { return null; }
        }

        // Samples a few pixels; if they are all (near) the same colour the capture failed (black frame).
        static bool IsNearlyBlank(Bitmap b)
        {
            try
            {
                Color c0 = b.GetPixel(b.Width / 2, b.Height / 2);
                Color[] probe = new Color[]
                {
                    b.GetPixel(4, 4), b.GetPixel(b.Width - 5, 4),
                    b.GetPixel(4, b.Height - 5), b.GetPixel(b.Width - 5, b.Height - 5),
                    b.GetPixel(b.Width / 3, b.Height / 2), b.GetPixel(b.Width * 2 / 3, b.Height / 2)
                };
                foreach (Color c in probe)
                    if (Math.Abs(c.R - c0.R) + Math.Abs(c.G - c0.G) + Math.Abs(c.B - c0.B) > 24) return false;
                return true;
            }
            catch (Exception) { return false; }
        }
    }
}

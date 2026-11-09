using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace RobloxMultiHub
{
    public static class Tint
    {
        public static Color Shift(Color c, int d)
        {
            return Color.FromArgb(Clamp(c.R + d), Clamp(c.G + d), Clamp(c.B + d));
        }

        public static Color Mix(Color a, Color b, float t)
        {
            return Color.FromArgb((int)(a.R + (b.R - a.R) * t), (int)(a.G + (b.G - a.G) * t), (int)(a.B + (b.B - a.B) * t));
        }

        static int Clamp(int v) { return v < 0 ? 0 : (v > 255 ? 255 : v); }
    }

    // Rounded, self-painted button. Sizes itself to its text so nothing is ever cut off.
    public class RoundButton : Button
    {
        public string Kind = "";        // "", "primary", "danger", "nav"
        public bool Active;             // highlighted state of nav / toggle buttons
        public bool LeftAlign;          // left-align the text (sidebar items) instead of centering
        public float Opacity = 1f;      // 0..1, applied while painting (used for show/hide animations)
        public float TextOpacity = 1f;  // text can fade faster than the body while a button shrinks
        public float Wipe;              // nav only: how much of the active highlight is shown (0..1)
        Point? growAt;                  // where the highlight evolves out from (the click point)
        float growFrom, growTo;         // evolve animation, driven by the SAME clock and repaint
        int growT0;                     // timer as the ripple so the two never beat against each other
        bool growing;
        bool hover;
        bool pressed;
        Color curFill, curText, curBorder;   // smoothly animated toward their target each frame
        bool colorsInit;
        // every animating button repaints on the ONE shared 8ms animation clock (Anim.Frame) -
        // per-button WinForms timers delivered uneven ~15.6ms WM_TIMERs, which read as judder
        static readonly System.Collections.Generic.HashSet<RoundButton> animating = new System.Collections.Generic.HashSet<RoundButton>();
        static bool frameHooked;
        int animUntil;                          // safety: no animation legitimately runs this long
        Point rippleAt;                      // click ripple origin (the press point)
        float rippleR;                       // press point -> farthest corner, the radius the circle reaches
        int rippleT0;
        bool rippling;
        const int RippleMs = 400;            // the ripple law: r * sqrt(t) grows, 0.15*(1-t)^2 fades, 0.4s

        public RoundButton()
        {
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
            FlatStyle = FlatStyle.Flat;
            FlatAppearance.BorderSize = 0;
            Cursor = Cursors.Hand;
            Font = Theme.Base;
            Height = 30;
        }

        // Starts (or reverses) the highlight evolve, on the shared animation clock.
        public void Evolve(float to)
        {
            growFrom = CurrentGrow();
            growTo = to;
            growT0 = Environment.TickCount;
            growing = true;
            RunAnim();
        }

        // The CURRENT highlight amount (0..1); advances from its timestamps so the painted frame is
        // always correct no matter when repaints arrive, and settles Wipe when finished.
        float CurrentGrow()
        {
            if (!growing) return Wipe;
            float t = Math.Min(1f, (Environment.TickCount - growT0) / (float)Anim.Ms);
            if (t >= 1f) { growing = false; Wipe = growTo; return Wipe; }
            float e = 1f - (1f - t) * (1f - t) * (1f - t);
            return growFrom + (growTo - growFrom) * e;
        }

        public bool Evolving { get { return growing; } }

        // called by the shared clock every frame: advances (and settles) the highlight value from
        // its timestamps, INDEPENDENT of painting - so the state can never get stuck even if a
        // button is not repainted by the branch that would normally advance it
        internal void Advance() { CurrentGrow(); }

        public void FitText(int minWidth)
        {
            Size ts = TextRenderer.MeasureText(Text, Font);
            Width = Math.Max(minWidth, ts.Width + 28);
        }

        protected override void OnMouseEnter(EventArgs e) { hover = true; Invalidate(); base.OnMouseEnter(e); }
        protected override void OnMouseLeave(EventArgs e) { hover = false; pressed = false; Invalidate(); base.OnMouseLeave(e); }
        protected override void OnMouseDown(MouseEventArgs e)
        {
            pressed = true;
            if (e.Button == MouseButtons.Left && Store.Settings.Animations)
            {
                rippleAt = e.Location;
                growAt = e.Location;
                // farthest corner from the press point - the radius the circle has at t = 1
                float dx = Math.Max(e.X, Width - e.X);
                float dy = Math.Max(e.Y, Height - e.Y);
                rippleR = (float)Math.Sqrt(dx * dx + dy * dy);
                rippleT0 = Environment.TickCount;
                rippling = true;
            }
            Invalidate();
            base.OnMouseDown(e);
        }
        protected override void OnMouseUp(MouseEventArgs e) { pressed = false; Invalidate(); base.OnMouseUp(e); }
        protected override void OnEnabledChanged(EventArgs e) { Invalidate(); base.OnEnabledChanged(e); }
        protected override void OnTextChanged(EventArgs e) { Invalidate(); base.OnTextChanged(e); }
        protected override void Dispose(bool disposing)
        {
            if (disposing) animating.Remove(this);
            base.Dispose(disposing);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            Graphics g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.Clear(Parent != null ? Parent.BackColor : Theme.Panel);
            if (Width < 6 || Height < 6) return;
            Rectangle r = new Rectangle(0, 0, Width - 1, Height - 1);

            // ---- target colours for the current state ----
            Color fill = Theme.Panel2;
            Color border = Theme.Border;
            Color text = Theme.Text;
            if (Kind == "primary")
            {
                fill = Theme.Accent;
                border = Theme.Accent;
                text = Color.White;
                if (hover) fill = Tint.Shift(fill, 18);
            }
            else if (Kind == "danger")
            {
                border = Theme.Bad;
                text = Theme.Bad;
                if (hover) fill = Tint.Mix(Theme.Panel2, Theme.Bad, 0.2f);
            }
            else if (Kind == "nav")
            {
                if (Active) { fill = Theme.Accent; text = Color.White; }
                else { fill = hover ? Theme.Panel2 : Theme.Panel; text = hover ? Theme.Text : Theme.Muted; }
                border = fill;
            }
            else if (Kind == "toggle")
            {
                // outlined like a normal button, filled when it is the active choice (List / Cards)
                if (Active) { fill = Theme.Accent; border = Theme.Accent; text = Color.White; }
                else { fill = Theme.Panel2; border = hover ? Theme.Accent : Theme.Border; text = Theme.Text; }
            }
            else if (hover) fill = Tint.Shift(fill, Theme.Dark ? 14 : -10);
            if (pressed) fill = Tint.Shift(fill, -14);
            if (!Enabled) { text = Theme.Muted; fill = Theme.Panel; border = Theme.Border; }

            // ---- ease the displayed colours toward the target (smooth hover / active) ----
            if (!colorsInit) { curFill = fill; curText = text; curBorder = border; colorsInit = true; }
            else
            {
                curFill = Lerp(curFill, fill, 0.28f);
                curText = Lerp(curText, text, 0.34f);
                curBorder = Lerp(curBorder, border, 0.28f);
            }

            Color drawFill = Fade(curFill, Opacity);
            Color drawBorder = Fade(curBorder, Opacity);
            Color drawText = Fade(curText, Opacity);
            using (GraphicsPath path = Shapes.Round(r, Math.Min(10, Height / 2)))
            {
                if (Kind == "nav")
                {
                    // nav body: the plain panel colour, then the active highlight. When a tab is
                    // clicked the highlight EVOLVES out of the click point (the ripple's origin,
                    // same clock): the accent fills a circle that grows with the ripple until it
                    // covers the whole rounded rectangle. The tab being left just dissolves.
                    using (SolidBrush b = new SolidBrush(hover && !Active ? Theme.Panel2 : Theme.Panel)) g.FillPath(b, path);
                    if (Active || Wipe > 0.01f)
                    {
                        if (Active)
                        {
                            float w = Math.Max(0f, Math.Min(1f, CurrentGrow()));
                            Point o = growAt ?? new Point(Width / 2, Height / 2);
                            int rr = Math.Max(1, (int)(Math.Sqrt(Width * (double)Width + Height * (double)Height) * w));
                            g.SetClip(path);
                            using (System.Drawing.Drawing2D.GraphicsPath c = new System.Drawing.Drawing2D.GraphicsPath())
                            {
                                c.AddEllipse(o.X - rr, o.Y - rr, rr * 2, rr * 2);
                                g.SetClip(c, System.Drawing.Drawing2D.CombineMode.Intersect);
                            }
                            using (SolidBrush ab = new SolidBrush(Theme.Accent)) g.FillPath(ab, path);
                            g.ResetClip();
                        }
                        else
                        {
                            // dissolving: must ALSO advance here (CurrentGrow), or Wipe stays at its
                            // stale value forever and the tab keeps its highlight - that was the bug
                            float w = Math.Max(0f, Math.Min(1f, CurrentGrow()));
                            int a = (int)(255 * w);
                            if (a > 2)
                                using (SolidBrush ab = new SolidBrush(Color.FromArgb(a, Theme.Accent))) g.FillPath(ab, path);
                        }
                    }
                }
                else
                    using (SolidBrush b = new SolidBrush(drawFill)) g.FillPath(b, path);
                if (rippling)
                {
                    float rt = (Environment.TickCount - rippleT0) / (float)RippleMs;
                    if (rt >= 1f) rippling = false;
                    else
                    {
                        // the law: radius grows as r*sqrt(t) (fast burst, gentle landing) and the
                        // alpha fades as 0.15*(1-t)^2 from the press point to the farthest corner
                        float rr = rippleR * (float)Math.Sqrt(rt);
                        int alpha = (int)(255 * 0.15f * (1f - rt) * (1f - rt));
                        if (alpha > 0)
                            using (SolidBrush rb = new SolidBrush(Color.FromArgb(alpha, Color.White)))
                            {
                                g.SetClip(path);
                                g.FillEllipse(rb, rippleAt.X - rr, rippleAt.Y - rr, rr * 2, rr * 2);
                                g.ResetClip();
                            }
                    }
                }
                if (Diff(curBorder, curFill) > 6)
                    using (Pen pen = new Pen(drawBorder)) g.DrawPath(pen, path);
            }
            Rectangle tr = r;
            TextFormatFlags flags = TextFormatFlags.VerticalCenter | TextFormatFlags.SingleLine | TextFormatFlags.NoPrefix | TextFormatFlags.EndEllipsis;
            if (LeftAlign) { tr = new Rectangle(r.Left + 12, r.Top, r.Width - 16, r.Height); flags |= TextFormatFlags.Left; }
            else flags |= TextFormatFlags.HorizontalCenter;
            TextRenderer.DrawText(g, Text, Font, tr, Fade(curText, TextOpacity * Opacity), flags);

            // repaint on the shared clock while anything is in motion; settle = leave the animating set
            if (rippling || growing || Diff(curFill, fill) + Diff(curText, text) + Diff(curBorder, border) > 4) RunAnim();
            else
            {
                animating.Remove(this);
                if (animating.Count == 0) Anim.FrameDemand = false;   // loop winds down when idle
            }
        }

        static Color Lerp(Color a, Color b, float t)
        {
            return Color.FromArgb(
                (int)(a.R + (b.R - a.R) * t),
                (int)(a.G + (b.G - a.G) * t),
                (int)(a.B + (b.B - a.B) * t));
        }

        static Color Fade(Color c, float o)
        {
            if (o >= 0.999f) return c;
            return Color.FromArgb((int)(Math.Max(0f, Math.Min(1f, o)) * 255f), c);
        }

        static int Diff(Color a, Color b)
        {
            return Math.Abs(a.R - b.R) + Math.Abs(a.G - b.G) + Math.Abs(a.B - b.B);
        }

        void RunAnim()
        {
            animating.Add(this);
            animUntil = Environment.TickCount + 1500;   // nothing animates longer than this; if a
            Anim.FrameDemand = true;                    // settle is ever missed the loop still winds down
            Anim.EnsureClock();                 // starts the shared frame loop on first need
            if (frameHooked) return;
            frameHooked = true;
            Anim.Frame += delegate
            {
                foreach (RoundButton b in animating)
                {
                    if (Environment.TickCount > b.animUntil) b.ForceSettle();
                    else { b.Advance(); b.Invalidate(); }
                }
                if (animating.Count == 0) Anim.FrameDemand = false;
            };
        }

        // failsafe settle: leaves the animating set even if the normal settle was never reached
        internal void ForceSettle()
        {
            rippling = false;
            if (growing) { Wipe = growTo; growing = false; }
            animating.Remove(this);
            Invalidate();
        }
    }

    // Combo box that follows the theme (the standard one stays white) with a rounded closed box.
    public class DarkCombo : ComboBox
    {
        bool hover;

        public DarkCombo()
        {
            DrawMode = DrawMode.OwnerDrawFixed;
            DropDownStyle = ComboBoxStyle.DropDownList;
            FlatStyle = FlatStyle.Flat;
            ItemHeight = 22;
            Font = Theme.Base;
            BackColor = Theme.Panel2;             // the typing area is drawn by Windows from these
            ForeColor = Theme.Text;
            // Windows paints the typing area of a DISABLED editable combo with its own white brush
            // and ignores BackColor (measured). While disabled we switch to DropDownList style,
            // which the control's own dark painting fully covers, and restore style + text after.
            EnabledChanged += delegate
            {
                if (!Enabled)
                {
                    wasEditable = DropDownStyle == ComboBoxStyle.DropDown;
                    savedText = Text;
                    if (wasEditable) DropDownStyle = ComboBoxStyle.DropDownList;
                }
                else if (wasEditable)
                {
                    DropDownStyle = ComboBoxStyle.DropDown;
                    Text = savedText;
                }
            };
        }

        bool wasEditable;
        string savedText;

        protected override void OnDrawItem(DrawItemEventArgs e)
        {
            if (e.Index < 0) return;
            bool hot = (e.State & DrawItemState.Selected) != 0;
            using (SolidBrush b = new SolidBrush(hot ? Theme.Accent : Theme.Panel2)) e.Graphics.FillRectangle(b, e.Bounds);
            string text = GetItemText(Items[e.Index]);
            TextRenderer.DrawText(e.Graphics, text, Font, new Rectangle(e.Bounds.Left + 8, e.Bounds.Top, e.Bounds.Width - 12, e.Bounds.Height),
                hot ? Color.White : Theme.Text,
                TextFormatFlags.VerticalCenter | TextFormatFlags.Left | TextFormatFlags.EndEllipsis | TextFormatFlags.NoPrefix);
        }

        protected override void OnMouseEnter(EventArgs e) { hover = true; Invalidate(); base.OnMouseEnter(e); }
        protected override void OnMouseLeave(EventArgs e) { hover = false; Invalidate(); base.OnMouseLeave(e); }

        protected override void WndProc(ref Message m)
        {
            base.WndProc(ref m);
            if (m.Msg == 0x000F) PaintBox();     // WM_PAINT: draw our own look over the system one
        }

        void PaintBox()
        {
            if (!IsHandleCreated || Width < 24 || Height < 12) return;
            try
            {
                using (Graphics g = Graphics.FromHwnd(Handle))
                {
                    g.SmoothingMode = SmoothingMode.AntiAlias;
                    bool list = DropDownStyle == ComboBoxStyle.DropDownList;
                    Color outer = Parent != null ? Parent.BackColor : Theme.Panel;
                    Color edge = (Focused || hover) ? Theme.Accent : Theme.Border;
                    Rectangle r = new Rectangle(0, 0, Width - 1, Height - 1);
                    if (list)
                    {
                        g.Clear(outer);
                        using (GraphicsPath path = Shapes.Round(r, 8))
                        {
                            using (SolidBrush b = new SolidBrush(Theme.Panel2)) g.FillPath(b, path);
                            using (Pen pen = new Pen(edge)) g.DrawPath(pen, path);
                        }
                        TextRenderer.DrawText(g, Text, Font, new Rectangle(10, 0, Width - 34, Height), Enabled ? Theme.Text : Theme.Muted,
                            TextFormatFlags.VerticalCenter | TextFormatFlags.Left | TextFormatFlags.EndEllipsis | TextFormatFlags.NoPrefix);
                    }
                    else
                    {
                        using (SolidBrush b = new SolidBrush(Theme.Panel2)) g.FillRectangle(b, Width - 26, 1, 25, Height - 2);
                        using (Pen pen = new Pen(edge)) g.DrawRectangle(pen, r);
                    }
                    int cx = Width - 16;
                    int cy = Height / 2;
                    using (Pen arrow = new Pen(Theme.Muted, 1.8f))
                        g.DrawLines(arrow, new Point[] { new Point(cx - 4, cy - 2), new Point(cx, cy + 2), new Point(cx + 4, cy - 2) });
                }
            }
            catch (Exception) { }
        }
    }

    // Panel with softly rounded corners (the big cards of the window).
    public class RoundPanel : Panel
    {
        public int Radius = 14;

        public RoundPanel()
        {
            DoubleBuffered = true;
            ResizeRedraw = true;
        }

        protected override void OnPaintBackground(PaintEventArgs e)
        {
            Graphics g = e.Graphics;
            g.Clear(Parent != null ? Parent.BackColor : Theme.Bg);
            if (Width < 4 || Height < 4) return;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            Rectangle r = new Rectangle(0, 0, Width - 1, Height - 1);
            using (GraphicsPath path = Shapes.Round(r, Radius))
            {
                using (SolidBrush b = new SolidBrush(BackColor)) g.FillPath(b, path);
                using (Pen pen = new Pen(Theme.Dark ? Tint.Shift(BackColor, 10) : Theme.Border)) g.DrawPath(pen, path);
            }
        }
    }

    // Text input with rounded border. Wraps a borderless TextBox and forwards the members the app uses.
    public class RoundTextBox : Control
    {
        readonly TextBox tb = new TextBox();
        bool focused;

        public RoundTextBox()
        {
            SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
            tb.BorderStyle = BorderStyle.None;
            tb.Font = Theme.Base;
            Controls.Add(tb);
            Height = 28;
            tb.TextChanged += delegate { OnTextChanged(EventArgs.Empty); };
            tb.KeyDown += delegate(object s, KeyEventArgs e) { OnKeyDown(e); };
            tb.Enter += delegate { focused = true; Invalidate(); OnEnter(EventArgs.Empty); };
            tb.Leave += delegate { focused = false; Invalidate(); OnLeave(EventArgs.Empty); };
            ApplyColors();
            LayoutInner();
        }

        public override string Text
        {
            get { return tb.Text; }
            set { tb.Text = value; }
        }

        public bool UseSystemPasswordChar
        {
            get { return tb.UseSystemPasswordChar; }
            set { tb.UseSystemPasswordChar = value; }
        }

        public bool ReadOnly
        {
            get { return tb.ReadOnly; }
            set { tb.ReadOnly = value; }
        }

        public new bool Focused { get { return tb.Focused; } }

        public void Tip(string text)
        {
            Ui.Tips.SetToolTip(this, text);
            Ui.Tips.SetToolTip(tb, text);
        }

        public void ApplyColors()
        {
            bool inset = (Tag as string) == "inset";
            tb.BackColor = inset ? Theme.Bg : Theme.Panel2;
            tb.ForeColor = Enabled ? Theme.Text : Theme.Muted;
            Invalidate();
        }

        void LayoutInner()
        {
            tb.Left = 10;
            tb.Width = Math.Max(10, Width - 20);
            tb.Top = Math.Max(0, (Height - tb.Height) / 2);
        }

        protected override void OnResize(EventArgs e) { base.OnResize(e); LayoutInner(); }
        protected override void OnFontChanged(EventArgs e) { base.OnFontChanged(e); tb.Font = Font; LayoutInner(); }
        protected override void OnEnabledChanged(EventArgs e) { base.OnEnabledChanged(e); ApplyColors(); }
        protected override void OnGotFocus(EventArgs e) { base.OnGotFocus(e); tb.Focus(); }

        protected override void OnPaint(PaintEventArgs e)
        {
            Graphics g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.Clear(Parent != null ? Parent.BackColor : Theme.Panel);
            if (Width < 6 || Height < 6) return;
            bool inset = (Tag as string) == "inset";
            Rectangle r = new Rectangle(0, 0, Width - 1, Height - 1);
            using (GraphicsPath path = Shapes.Round(r, 8))
            {
                using (SolidBrush b = new SolidBrush(inset ? Theme.Bg : Theme.Panel2)) g.FillPath(b, path);
                using (Pen pen = new Pen(focused ? Theme.Accent : Theme.Border)) g.DrawPath(pen, path);
            }
        }
    }

    // Small round "i" next to an option; hovering it shows the long explanation.
    public class InfoIcon : Control
    {
        bool hover;

        public InfoIcon(string tip)
        {
            SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer, true);
            Size = new Size(20, 20);
            Cursor = Cursors.Help;
            Ui.Tips.SetToolTip(this, tip);
        }

        protected override void OnMouseEnter(EventArgs e) { hover = true; Invalidate(); base.OnMouseEnter(e); }
        protected override void OnMouseLeave(EventArgs e) { hover = false; Invalidate(); base.OnMouseLeave(e); }

        protected override void OnPaint(PaintEventArgs e)
        {
            Graphics g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.Clear(Parent != null ? Parent.BackColor : Theme.Panel);
            Rectangle r = new Rectangle(1, 1, 17, 17);
            using (SolidBrush b = new SolidBrush(hover ? Theme.Accent : Theme.Panel2)) g.FillEllipse(b, r);
            using (Pen pen = new Pen(hover ? Theme.Accent : Theme.Muted)) g.DrawEllipse(pen, r);
            TextRenderer.DrawText(g, "i", Theme.Bold, new Rectangle(1, 0, 18, 19), hover ? Color.White : Theme.Muted,
                TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPadding | TextFormatFlags.NoPrefix);
        }
    }

    // Gives scrollbars (and spin buttons) the dark / light Windows look instead of the plain grey classic one.
    public static class NativeTheme
    {
        [DllImport("uxtheme.dll", CharSet = CharSet.Unicode)]
        static extern int SetWindowTheme(IntPtr hwnd, string appName, string idList);

        static readonly ConditionalWeakTable<Control, object> hooked = new ConditionalWeakTable<Control, object>();

        static bool Wanted(Control c)
        {
            if (c is Form) return false;
            if (c is ListView || c is NumericUpDown) return true;
            TextBox tb = c as TextBox;
            if (tb != null) return tb.Multiline;
            if (c.GetType().Name == "UpDownButtons") return true;
            ScrollableControl sc = c as ScrollableControl;
            return sc != null && sc.AutoScroll;
        }

        public static void Apply(Control c)
        {
            if (!Wanted(c)) return;
            object o;
            if (!hooked.TryGetValue(c, out o))
            {
                hooked.Add(c, new object());
                c.HandleCreated += delegate { Set(c); };
            }
            if (c.IsHandleCreated) Set(c);
        }

        static void Set(Control c)
        {
            try { SetWindowTheme(c.Handle, Theme.Dark ? "DarkMode_Explorer" : "Explorer", null); }
            catch (Exception) { }
        }
    }

    // Small clickable Discord logo (the real Discord mark, embedded as a PNG).
    public class DiscordIcon : System.Windows.Forms.Control
    {
        const string LogoB64 = "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";
        static Image logo;
        bool hover;

        public DiscordIcon()
        {
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw | ControlStyles.SupportsTransparentBackColor, true);
            Size = new Size(30, 24);
            Cursor = System.Windows.Forms.Cursors.Hand;
            BackColor = Color.Transparent;
            if (logo == null)
            {
                try { logo = Image.FromStream(new System.IO.MemoryStream(Convert.FromBase64String(LogoB64))); }
                catch (Exception) { }
            }
        }

        protected override void OnMouseEnter(EventArgs e) { hover = true; Invalidate(); base.OnMouseEnter(e); }
        protected override void OnMouseLeave(EventArgs e) { hover = false; Invalidate(); base.OnMouseLeave(e); }

        protected override void OnPaint(System.Windows.Forms.PaintEventArgs e)
        {
            if (logo == null) return;
            Graphics g = e.Graphics;
            g.InterpolationMode = InterpolationMode.HighQualityBicubic;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            int s = Math.Min(Width, Height) + (hover ? 2 : 0);   // the real Discord mark, square, fit to the control
            int x = (Width - s) / 2;
            int y = (Height - s) / 2;
            g.DrawImage(logo, new Rectangle(x, y, s, s));
        }
    }
}

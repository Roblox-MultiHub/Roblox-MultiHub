using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace RobloxMultiHub
{
    // TinyTask-style macro recorder in its own window (not owned by the hub, so 'Stay on top' only
    // affects this small window). Record mouse + keyboard, save / open recordings, play them back
    // once, N times or until stopped, at any speed. Global hotkeys work while a game has the focus.
    public class MacroForm : Form
    {
        const int HK_REC = 1;
        const int HK_PLAY = 2;
        const int W = 470;

        static readonly string[] HotkeyChoices = new string[]
        {
            "Ctrl+Shift+Alt+R", "Ctrl+Shift+Alt+P", "Ctrl+Shift+Alt+S", "Ctrl+Shift+Alt+E",
            "F5", "F6", "F7", "F8", "F9", "F10", "F11", "F12",
            "Ctrl+F6", "Ctrl+F7", "Ctrl+F8", "Ctrl+F12", "Pause", "Scroll Lock"
        };

        MacroSettings S { get { return Store.Settings.Macro; } }

        // true while recording or playing (anti-AFK then waits, so it never takes the focus mid-macro)
        public bool Busy { get { return MacroRecorder.Recording || player.Playing; } }
        List<MacroEvent> events = new List<MacroEvent>();
        readonly MacroPlayer player = new MacroPlayer();
        string filePath = "";
        bool dirty;
        bool loading;

        RoundButton btnRec, btnPlay, btnOpen, btnSave, btnSize;
        Label lblStatus;
        Panel host;
        FlowLayoutPanel bar;                            // small display: only the buttons
        bool compact;
        Size fullClient;
        readonly Dictionary<Control, Point> fullPos = new Dictionary<Control, Point>();
        NumericUpDown numSpeed, numRepeats, numPause;
        CheckBox chkContinuous, chkMoves, chkHiRes, chkTop, chkHide;
        ComboBox cbRecKey, cbPlayKey;

        public MacroForm()
        {
            loading = true;
            Text = "Macro recorder - Roblox MultiHub";
            Font = Theme.Base;
            AutoScaleDimensions = new SizeF(96F, 96F);
            AutoScaleMode = AutoScaleMode.Dpi;
            FormBorderStyle = FormBorderStyle.FixedSingle;
            MaximizeBox = false;
            ShowInTaskbar = true;
            StartPosition = FormStartPosition.CenterScreen;
            try { Icon = Icon.ExtractAssociatedIcon(Application.ExecutablePath); } catch (Exception) { }

            host = new Panel();
            host.Dock = DockStyle.Fill;
            host.Tag = "card";
            Controls.Add(host);
            bar = new FlowLayoutPanel();
            bar.AutoSize = true;
            bar.WrapContents = false;
            bar.Padding = new Padding(6);
            bar.Tag = "card";
            bar.Visible = false;
            Controls.Add(bar);
            PageBuilder p = new PageBuilder(host, W);
            p.Title("Macro recorder", "Record your mouse and keyboard and play it back, like TinyTask. The hotkeys also work while a game has the focus.");

            btnOpen = Ui.Btn("Open...", 70, delegate { OpenFile(); }, "");
            btnSave = Ui.Btn("Save...", 70, delegate { SaveFile(); }, "");
            btnRec = Ui.Btn("Record", 96, delegate { ToggleRecord(true); }, "danger");
            btnPlay = Ui.Btn("Play", 86, delegate { TogglePlay(false); }, "primary");
            btnSize = Ui.Btn("Small", 60, delegate { SetCompact(!compact); }, "");
            Ui.Tips.SetToolTip(btnSize, "Small: show only the buttons, so the window covers almost nothing. Large: show all options again. Remembered for next time.");
            p.Row(btnOpen, btnSave, btnRec, btnPlay, btnSize);
            lblStatus = Ui.Lbl("", "muted");
            lblStatus.AutoSize = false;
            lblStatus.Size = new Size(W - PageBuilder.PadX * 2, 34);
            p.Row(lblStatus);

            p.Section("Playback");
            numSpeed = new NumericUpDown();
            numSpeed.DecimalPlaces = 2;
            numSpeed.Increment = 0.25M;
            numSpeed.Minimum = 0.1M;
            numSpeed.Maximum = 100M;
            numSpeed.Width = 80;
            numSpeed.Font = Theme.Base;
            numSpeed.Value = (decimal)Math.Max(0.1, Math.Min(100, S.Speed));
            p.Opt("1 = real speed, 2 = twice as fast, 0.5 = half speed.",
                "Multiplies the speed of the whole recording. Very high speeds can be too fast for some games to notice every key press.",
                Ui.Fld("Playback speed (x)", 150), numSpeed);
            numRepeats = Ui.Num(1, 999999, S.Repeats, 90);
            chkContinuous = Ui.Chk("Repeat until stopped", S.Continuous, null);
            p.Opt("How many times the recording is played.",
                "Plays the recording this many times in a row. 'Repeat until stopped' (continuous playback) loops it forever until you press Stop or the play hotkey.",
                Ui.Fld("Play (times)", 150), numRepeats, chkContinuous);
            numPause = Ui.Num(0, 3600000, S.PauseMs, 90);
            p.Opt("Wait between two repeats (milliseconds).",
                "An extra pause after each repeat before the next one starts. 0 = start the next repeat immediately.",
                Ui.Fld("Pause between (ms)", 150), numPause);

            p.Section("Recording");
            chkMoves = Ui.Chk("Record mouse movement", S.RecordMoves, null);
            p.Opt("Off = only clicks, scrolling and keys are recorded.",
                "With this off, the mouse jumps straight to each click position during playback instead of following your movement.",
                chkMoves);
            chkHiRes = Ui.Chk("High-resolution mouse movement", S.HiRes, null);
            p.Opt("Records every single mouse movement (smoother, bigger files).",
                "On: every movement Windows reports is recorded, so playback follows the mouse exactly (needed for camera turns in games). Off: at most about 60 movements per second, which is enough for most clicking macros.",
                chkHiRes);

            p.Section("Hotkeys & window");
            cbRecKey = Ui.Cmb(170, HotkeyChoices);
            cbPlayKey = Ui.Cmb(170, HotkeyChoices);
            cbRecKey.SelectedIndex = Math.Max(0, Array.IndexOf(HotkeyChoices, S.RecHotkey));
            cbPlayKey.SelectedIndex = Math.Max(0, Array.IndexOf(HotkeyChoices, S.PlayHotkey));
            p.Opt("Start / stop recording.", "Works everywhere, also while a game has the focus. The hotkey itself is left out of the recording.",
                Ui.Fld("Record hotkey", 150), cbRecKey);
            p.Opt("Start / stop playback.", "Works everywhere, also while a game has the focus. Playback waits until you let go of Ctrl / Shift / Alt, so they do not mix with the recording.",
                Ui.Fld("Play hotkey", 150), cbPlayKey);
            chkTop = Ui.Chk("Stay on top", S.TopMost, null);
            p.Opt("Keeps only this small window above other windows.", "Only the macro window stays on top - the hub window is not affected.", chkTop);
            chkHide = Ui.Chk("Hide this window while recording or playing", S.HideWhileRunning, null);
            p.Opt("Out of the way while it runs - stop with the hotkey.", "The window comes back when recording or playback stops. Use the hotkeys to stop while it is hidden.", chkHide);

            ClientSize = new Size(W + 18, Math.Min(p.Y + 16, Screen.PrimaryScreen.WorkingArea.Height - 80));
            TopMost = S.TopMost;
            if (S.Compact) Opacity = 0;                     // switched to the small display once shown, without a flash

            EventHandler changed = delegate { SaveOptions(); };
            numSpeed.ValueChanged += changed;
            numRepeats.ValueChanged += changed;
            numPause.ValueChanged += changed;
            chkContinuous.CheckedChanged += changed;
            chkMoves.CheckedChanged += changed;
            chkHiRes.CheckedChanged += changed;
            chkHide.CheckedChanged += changed;
            chkTop.CheckedChanged += delegate { SaveOptions(); TopMost = chkTop.Checked; };
            cbRecKey.SelectedIndexChanged += delegate { SaveOptions(); RegisterKeys(); };
            cbPlayKey.SelectedIndexChanged += delegate { SaveOptions(); RegisterKeys(); };

            player.RepeatStarted += delegate(int rep) { Ui0(delegate { ShowPlaying(rep); }); };
            player.Finished += delegate { Ui0(OnPlayFinished); };

            Theme.Apply(this);
            loading = false;
            UpdateUi();
        }

        protected override void OnShown(EventArgs e)
        {
            base.OnShown(e);
            if (S.Compact) SetCompact(true);
            Opacity = 1;
        }

        // Small display = only Open / Save / Record / Play and this toggle; the state shows in the title bar.
        void SetCompact(bool on)
        {
            Control[] buttons = new Control[] { btnOpen, btnSave, btnRec, btnPlay, btnSize };
            SuspendLayout();
            host.AutoScrollPosition = Point.Empty;
            if (on && !compact)
            {
                fullClient = ClientSize;
                foreach (Control c in buttons)
                {
                    fullPos[c] = c.Location;
                    c.Margin = new Padding(3);
                    bar.Controls.Add(c);
                }
                host.Visible = false;
                bar.Visible = true;
            }
            else if (!on && compact)
            {
                foreach (Control c in buttons)
                {
                    host.Controls.Add(c);
                    if (fullPos.ContainsKey(c)) c.Location = fullPos[c];
                }
                bar.Visible = false;
                host.Visible = true;
            }
            compact = on;
            btnSize.Text = on ? "Large" : "Small";
            ResumeLayout(true);
            ClientSize = on ? bar.PreferredSize : fullClient;
            if (S.Compact != on) { S.Compact = on; Store.SaveSettings(); }
        }

        void Ui0(Action a)
        {
            if (IsDisposed) return;
            try { BeginInvoke(a); } catch (Exception) { }
        }

        void SaveOptions()
        {
            if (loading) return;
            S.Speed = (double)numSpeed.Value;
            S.Repeats = (int)numRepeats.Value;
            S.Continuous = chkContinuous.Checked;
            S.PauseMs = (int)numPause.Value;
            S.RecordMoves = chkMoves.Checked;
            S.HiRes = chkHiRes.Checked;
            S.TopMost = chkTop.Checked;
            S.HideWhileRunning = chkHide.Checked;
            S.RecHotkey = HotkeyChoices[Math.Max(0, cbRecKey.SelectedIndex)];
            S.PlayHotkey = HotkeyChoices[Math.Max(0, cbPlayKey.SelectedIndex)];
            Store.SaveSettings();
            numRepeats.Enabled = !chkContinuous.Checked;
            chkHiRes.Enabled = chkMoves.Checked;
        }

        // ---------------- state / status ----------------
        void UpdateUi()
        {
            bool rec = MacroRecorder.Recording;
            bool play = player.Playing;
            btnRec.Text = rec ? "Stop rec." : "Record";
            btnPlay.Text = play ? Lang.T("Stop") : "Play";
            btnRec.Enabled = !play;
            btnPlay.Enabled = !rec && (play || events.Count > 0);
            btnOpen.Enabled = !rec && !play;
            btnSave.Enabled = !rec && !play && events.Count > 0;
            numRepeats.Enabled = !chkContinuous.Checked;
            chkHiRes.Enabled = chkMoves.Checked;
            foreach (Control c in new Control[] { btnRec, btnPlay, btnOpen, btnSave }) c.Invalidate();
            if (rec)
                SetStatus("Recording...  press " + S.RecHotkey + " (or 'Stop rec.') to stop.", "warn");
            else if (!play)
            {
                string name = filePath.Length > 0 ? Path.GetFileName(filePath) + (dirty ? " (not saved)" : "") : (events.Count > 0 ? "new recording (not saved)" : "nothing recorded yet");
                SetStatus(events.Count == 0
                    ? "Press Record or " + S.RecHotkey + " to start recording, or open a saved macro."
                    : name + "  -  " + events.Count + " events, " + (MacroRecorder.LengthMs(events) / 1000.0).ToString("0.0") + " s.  Play: " + S.PlayHotkey, events.Count == 0 ? "muted" : "good");
            }
        }

        void SetStatus(string text, string tag)
        {
            lblStatus.Text = text;
            lblStatus.ForeColor = tag == "warn" ? Theme.Warn : (tag == "good" ? Theme.Good : Theme.Muted);
            // the title bar also shows the state, so the small display needs no status line
            string state = MacroRecorder.Recording ? "Recording..." : (player.Playing ? "Playing" : (events.Count > 0 ? events.Count + " events" : "Ready"));
            Text = (compact ? "Macro - " : "Macro recorder - ") + state;
        }

        void ShowPlaying(int rep)
        {
            string of = player.Continuous ? " (until stopped)" : " of " + player.Repeats;
            SetStatus("Playing " + rep + of + " at " + player.Speed.ToString("0.##") + "x  -  press " + S.PlayHotkey + " to stop.", "warn");
            Text = (compact ? "Macro - Playing " : "Macro recorder - Playing ") + rep + (player.Continuous ? "" : "/" + player.Repeats);
        }

        // ---------------- recording ----------------
        void ToggleRecord(bool viaButton)
        {
            if (player.Playing) return;
            if (MacroRecorder.Recording)
            {
                MacroRecorder.Stop();
                events = new List<MacroEvent>(MacroRecorder.Events);
                if (viaButton)
                {
                    // the click on 'Stop rec.' itself is not part of the macro
                    int i = events.FindLastIndex(x => x.Kind == MacroEvent.Button && x.A == 1 && x.B == 1);
                    if (i >= 0) events.RemoveRange(i, events.Count - i);
                }
                dirty = events.Count > 0;
                filePath = "";
                if (S.HideWhileRunning) { Show(); Activate(); }
                UpdateUi();
                return;
            }
            if (dirty && !AskSave()) return;
            MacroRecorder.RecordMoves = S.RecordMoves;
            MacroRecorder.HiRes = S.HiRes;
            if (!MacroRecorder.Start())
            {
                SetStatus("Windows did not allow the recorder to watch the mouse and keyboard.", "warn");
                return;
            }
            if (S.HideWhileRunning) Hide();
            UpdateUi();
        }

        // ---------------- playback ----------------
        async void TogglePlay(bool fromHotkey)
        {
            if (MacroRecorder.Recording) return;
            if (player.Playing) { player.Stop(); return; }
            if (events.Count == 0) { SetStatus("Record something or open a macro first.", "warn"); return; }
            if (fromHotkey)
            {
                // let go of the hotkey first, or Ctrl / Shift / Alt would be held during the first events
                for (int i = 0; i < 100 && ModifiersDown(); i++) await Task.Delay(20);
            }
            player.Speed = (double)numSpeed.Value;
            player.Repeats = (int)numRepeats.Value;
            player.Continuous = chkContinuous.Checked;
            player.PauseMs = (int)numPause.Value;
            if (S.HideWhileRunning) Hide();
            player.Start(events);
            UpdateUi();
        }

        void OnPlayFinished()
        {
            if (S.HideWhileRunning && !Visible) Show();
            UpdateUi();
        }

        static bool ModifiersDown()
        {
            foreach (int vk in new int[] { 0x10, 0x11, 0x12, 0x5B, 0x5C })
                if ((GetAsyncKeyState(vk) & 0x8000) != 0) return true;
            return false;
        }

        // ---------------- files ----------------
        const string Filter = "Macro recordings (*.rmacro)|*.rmacro|All files|*.*";

        void OpenFile()
        {
            if (dirty && !AskSave()) return;
            using (OpenFileDialog d = new OpenFileDialog())
            {
                d.Title = "Open a macro";
                d.Filter = Filter;
                if (S.LastFile.Length > 0) try { d.InitialDirectory = Path.GetDirectoryName(S.LastFile); } catch (Exception) { }
                if (d.ShowDialog(this) != DialogResult.OK) return;
                try
                {
                    events = MacroRecorder.Load(d.FileName);
                    filePath = d.FileName;
                    dirty = false;
                    S.LastFile = d.FileName;
                    Store.SaveSettings();
                }
                catch (Exception ex) { MessageBox.Show(this, "Could not open that file: " + ex.Message, "Macro recorder"); }
            }
            UpdateUi();
        }

        bool SaveFile()
        {
            if (events.Count == 0) return true;
            using (SaveFileDialog d = new SaveFileDialog())
            {
                d.Title = "Save the macro";
                d.Filter = Filter;
                d.DefaultExt = "rmacro";
                d.FileName = filePath.Length > 0 ? Path.GetFileName(filePath) : "macro.rmacro";
                if (S.LastFile.Length > 0) try { d.InitialDirectory = Path.GetDirectoryName(S.LastFile); } catch (Exception) { }
                if (d.ShowDialog(this) != DialogResult.OK) return false;
                try
                {
                    MacroRecorder.Save(d.FileName, events);
                    filePath = d.FileName;
                    dirty = false;
                    S.LastFile = d.FileName;
                    Store.SaveSettings();
                }
                catch (Exception ex) { MessageBox.Show(this, "Could not save: " + ex.Message, "Macro recorder"); return false; }
            }
            UpdateUi();
            return true;
        }

        // false = the user cancelled
        bool AskSave()
        {
            DialogResult r = MessageBox.Show(this, "Save the current recording first?", "Macro recorder", MessageBoxButtons.YesNoCancel, MessageBoxIcon.Question);
            if (r == DialogResult.Cancel) return false;
            if (r == DialogResult.Yes) return SaveFile();
            dirty = false;
            return true;
        }

        // ---------------- hotkeys ----------------
        void RegisterKeys()
        {
            if (!IsHandleCreated) return;
            UnregisterHotKey(Handle, HK_REC);
            UnregisterHotKey(Handle, HK_PLAY);
            if (S.RecHotkey == S.PlayHotkey)
            {
                SetStatus("Record and play need two different hotkeys.", "warn");
                return;
            }
            bool a = Register(HK_REC, S.RecHotkey);
            bool b = Register(HK_PLAY, S.PlayHotkey);
            if (!a || !b) SetStatus("A hotkey is already used by another program - pick another one below.", "warn");
            else if (!loading) UpdateUi();
        }

        bool Register(int id, string text)
        {
            uint mod = 0x4000;                              // MOD_NOREPEAT
            uint vk = 0;
            foreach (string part in text.Split('+'))
            {
                if (part == "Ctrl") mod |= 0x0002;
                else if (part == "Shift") mod |= 0x0004;
                else if (part == "Alt") mod |= 0x0001;
                else if (part == "Pause") vk = 0x13;
                else if (part == "Scroll Lock") vk = 0x91;
                else if (part.Length > 1 && part[0] == 'F') vk = (uint)(0x6F + int.Parse(part.Substring(1)));
                else if (part.Length == 1) vk = part[0];
            }
            return RegisterHotKey(Handle, id, mod, vk);
        }

        protected override void OnHandleCreated(EventArgs e)
        {
            base.OnHandleCreated(e);
            RegisterKeys();
        }

        protected override void WndProc(ref Message m)
        {
            if (m.Msg == 0x0312)                            // WM_HOTKEY
            {
                int id = m.WParam.ToInt32();
                if (id == HK_REC) ToggleRecord(false);
                else if (id == HK_PLAY) TogglePlay(true);
            }
            base.WndProc(ref m);
        }

        protected override void OnFormClosing(FormClosingEventArgs e)
        {
            if (e.CloseReason == CloseReason.UserClosing && dirty && !MacroRecorder.Recording && !AskSave())
            {
                e.Cancel = true;
                return;
            }
            if (MacroRecorder.Recording) MacroRecorder.Stop();
            player.Stop();
            if (IsHandleCreated)
            {
                UnregisterHotKey(Handle, HK_REC);
                UnregisterHotKey(Handle, HK_PLAY);
            }
            base.OnFormClosing(e);
        }

        [DllImport("user32.dll")]
        static extern bool RegisterHotKey(IntPtr hWnd, int id, uint modifiers, uint vk);

        [DllImport("user32.dll")]
        static extern bool UnregisterHotKey(IntPtr hWnd, int id);

        [DllImport("user32.dll")]
        static extern short GetAsyncKeyState(int vk);
    }
}

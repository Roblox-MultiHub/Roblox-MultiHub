using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;

namespace RobloxMultiHub
{
    public static class Dialogs
    {
        public static Form Make(string title, int w, int h)
        {
            Form f = new Form();
            f.Text = title;
            f.AutoScaleDimensions = new SizeF(96F, 96F);
            f.AutoScaleMode = AutoScaleMode.Dpi;
            f.StartPosition = FormStartPosition.CenterParent;
            f.FormBorderStyle = FormBorderStyle.FixedDialog;
            f.MaximizeBox = false;
            f.MinimizeBox = false;
            f.ShowInTaskbar = false;
            f.Font = Theme.Base;
            f.ClientSize = new Size(w, h);
            try { f.Icon = Icon.ExtractAssociatedIcon(Application.ExecutablePath); } catch (Exception) { }
            return f;
        }

        public static T Put<T>(Form f, T c, int x, int y) where T : Control
        {
            c.Location = new Point(x, y);
            f.Controls.Add(c);
            return c;
        }

        // Adds OK / Cancel buttons at the bottom right.
        static void OkCancel(Form f, string okText)
        {
            Button ok = Ui.Btn(okText, 110, null, "primary");
            ok.DialogResult = DialogResult.OK;
            Button cancel = Ui.Btn(Lang.T("Cancel"), 90, null, "");
            cancel.DialogResult = DialogResult.Cancel;
            Put(f, ok, f.ClientSize.Width - 110 - 90 - 30, f.ClientSize.Height - 46);
            Put(f, cancel, f.ClientSize.Width - 90 - 16, f.ClientSize.Height - 46);
            f.AcceptButton = ok;
            f.CancelButton = cancel;
        }

        public static string AskPassword(IWin32Window owner, string title, string prompt, bool confirm)
        {
            Form f = Make(title, 420, confirm ? 220 : 170);
            if (owner == null) { f.ShowInTaskbar = true; f.StartPosition = FormStartPosition.CenterScreen; }
            Put(f, Ui.Lbl(prompt, "muted"), 16, 14);
            RoundTextBox t1 = Put(f, Ui.Txt(386, ""), 16, 44);
            t1.UseSystemPasswordChar = true;
            RoundTextBox t2 = null;
            if (confirm)
            {
                Put(f, Ui.Lbl("Type it again:", "muted"), 16, 80);
                t2 = Put(f, Ui.Txt(386, ""), 16, 102);
                t2.UseSystemPasswordChar = true;
            }
            OkCancel(f, "OK");
            Theme.Apply(f);
            while (true)
            {
                if (f.ShowDialog(owner) != DialogResult.OK) { f.Dispose(); return null; }
                if (t1.Text.Length == 0) { MessageBox.Show(f, "The password cannot be empty."); continue; }
                if (confirm && t1.Text != t2.Text) { MessageBox.Show(f, "The two passwords do not match."); continue; }
                string pw = t1.Text;
                f.Dispose();
                return pw;
            }
        }

        public static string Prompt(IWin32Window owner, string title, string label, string initial)
        {
            Form f = Make(title, 420, 150);
            Put(f, Ui.Lbl(label, "muted"), 16, 14);
            RoundTextBox t = Put(f, Ui.Txt(386, initial), 16, 44);
            OkCancel(f, "OK");
            Theme.Apply(f);
            f.ActiveControl = t;
            string result = null;
            if (f.ShowDialog(owner) == DialogResult.OK) result = t.Text.Trim();
            f.Dispose();
            return result;
        }

        public static void ShowText(IWin32Window owner, string title, string text)
        {
            Form f = Make(title, 560, 330);
            TextBox t = new TextBox();
            t.Multiline = true;
            t.ReadOnly = true;
            t.ScrollBars = ScrollBars.Vertical;
            t.Font = Theme.Mono;
            t.Text = text.Replace("\r\n", "\n").Replace("\n", "\r\n");
            t.Size = new Size(528, 250);
            Put(f, t, 16, 14);
            Button close = Ui.Btn("Close", 90, null, "primary");
            close.DialogResult = DialogResult.OK;
            Put(f, close, f.ClientSize.Width - 106, f.ClientSize.Height - 46);
            Button copy = Ui.Btn("Copy", 90, delegate { Util.Copy(text); }, "");
            Put(f, copy, f.ClientSize.Width - 106 - 100, f.ClientSize.Height - 46);
            f.AcceptButton = close;
            Theme.Apply(f);
            t.SelectionStart = 0;
            t.SelectionLength = 0;
            f.ShowDialog(owner);
            f.Dispose();
        }

        // Paste any text that contains cookies. Returns the raw text or null.
        public static string PasteCookies(IWin32Window owner)
        {
            Form f = Make("Add accounts from cookies", 620, 380);
            Put(f, Ui.Lbl("Paste one or more .ROBLOSECURITY cookies below (any text is fine - the app finds the cookies itself).\r\nEach cookie starts with  _|WARNING:-DO-NOT-SHARE-THIS...", "muted"), 16, 12);
            TextBox t = new TextBox();
            t.Multiline = true;
            t.ScrollBars = ScrollBars.Vertical;
            t.Font = Theme.Mono;
            t.AcceptsReturn = true;
            t.Size = new Size(588, 240);
            Put(f, t, 16, 56);
            Put(f, Ui.Lbl("Anyone with a cookie can use that account. Never share these, and never paste them into websites.", "warn"), 16, 304);
            OkCancel(f, Lang.T("Add accounts"));
            Theme.Apply(f);
            f.ActiveControl = t;
            string result = null;
            if (f.ShowDialog(owner) == DialogResult.OK) result = t.Text;
            f.Dispose();
            return result;
        }

        // Edits one or several accounts at once. With several accounts a field shows the value they share, or stays
        // empty when they differ; only the fields you change are written to every selected account.
        // Paste a list of user:password lines. Returns the raw text, or null if cancelled.
        public static string PasteUserPass(IWin32Window owner)
        {
            Form f = Make("Add several accounts (username:password)", 620, 400);
            Put(f, Ui.Lbl("Paste one account per line, as  username:password  - exactly like Roblox Account Manager:\r\n\r\n    myaccount1:mypassword1\r\n    myaccount2:mypassword2\r\n\r\nA browser window opens for each account with the fields already filled in. You still press 'Log In'\r\nyourself (and solve any captcha / 2-step); the account is then added automatically.", "muted"), 16, 12);
            TextBox t = new TextBox();
            t.Multiline = true;
            t.ScrollBars = ScrollBars.Vertical;
            t.Font = Theme.Mono;
            t.AcceptsReturn = true;
            t.Size = new Size(588, 180);
            Put(f, t, 16, 130);
            Put(f, Ui.Lbl("Passwords are only typed into Roblox's own login page. They are not saved unless you fill them in under Edit.", "warn"), 16, 320);
            OkCancel(f, "Add accounts");
            Theme.Apply(f);
            f.ActiveControl = t;
            string result = null;
            if (f.ShowDialog(owner) == DialogResult.OK) result = t.Text;
            f.Dispose();
            return result;
        }

        public static bool EditAccounts(IWin32Window owner, List<Account> list)
        {
            if (list == null || list.Count == 0) return false;
            bool multi = list.Count > 1;
            Account a = list[0];
            string names = string.Join(" - ", list.Select(x => x.Username).ToArray());
            Form f = Make((multi ? "Edit accounts - " : "Edit account - ") + names, 520, 504);
            int y = 14;
            if (multi)
            {
                Put(f, Ui.Lbl("Editing " + list.Count + " accounts. Only the fields you change are saved to all of them.\r\nA field marked 'different per account' keeps each account's own value while it stays empty.", "muted"), 16, y);
                y += 40;
            }
            else
            {
                Put(f, Ui.Lbl("Roblox user: " + a.Username + "   (ID " + a.UserId + ")", "muted"), 16, y);
                y += 30;
            }

            string cAlias = Common(list, x => x.Alias);
            string cDesc = Common(list, x => x.Description);
            string cGroup = Common(list, x => x.Group);
            string cPass = Common(list, x => x.Password);
            string cGame = Common(list, x => string.IsNullOrEmpty(x.GameLink) ? x.SavedPlaceId : x.GameLink);
            string cJob = Common(list, x => x.SavedJobId);
            int cFps = list.All(x => x.FpsCap == a.FpsCap) ? a.FpsCap : -1;

            RoundTextBox alias = AddField(f, Mixed("Alias (nickname shown in the list)", cAlias), cAlias, ref y);
            RoundTextBox desc = AddField(f, Mixed("Description / notes", cDesc), cDesc, ref y);

            Put(f, Ui.Lbl(Mixed("Group (type a new name to create a group)", cGroup), "muted"), 16, y);
            DarkCombo grp = new DarkCombo();
            grp.DropDownStyle = ComboBoxStyle.DropDown;
            grp.Width = 488;
            grp.Font = Theme.Base;
            foreach (string g in Store.AllGroups()) grp.Items.Add(g);
            grp.Text = cGroup ?? "";
            Put(f, grp, 16, y + 18);
            y += 50;

            RoundTextBox pass = AddField(f, Mixed("Password (optional - only so you can copy it later)", cPass), cPass, ref y);
            pass.UseSystemPasswordChar = true;
            RoundTextBox place = AddField(f, Mixed("Game for this account - game link, private server link or Place ID", cGame), cGame, ref y);
            RoundTextBox job = AddField(f, Mixed("Saved Job ID / server (optional)", cJob), cJob, ref y);

            Put(f, Ui.Lbl(cFps < 0 ? "FPS cap  (0 = use the global cap)  - different per account" : "FPS cap for this account  (0 = use the global cap)", "muted"), 16, y + 4);
            NumericUpDown fps = Put(f, Ui.Num(0, 1000, Math.Max(0, cFps), 80), 424, y);
            bool fpsTouched = false;
            fps.ValueChanged += delegate { fpsTouched = true; };
            y += 40;

            RoundTextBox cookie = null;
            if (!multi)                                     // one cookie can only belong to one account
            {
                cookie = AddField(f, "Replace cookie (leave empty to keep the current one)", "", ref y);
                cookie.UseSystemPasswordChar = true;
            }

            f.ClientSize = new Size(520, y + 60);
            OkCancel(f, "Save");
            Theme.Apply(f);
            bool ok = f.ShowDialog(owner) == DialogResult.OK;
            if (ok)
            {
                string v;
                if (Changed(cAlias, alias.Text.Trim(), out v)) foreach (Account x in list) x.Alias = v;
                if (Changed(cDesc, desc.Text.Trim(), out v)) foreach (Account x in list) x.Description = v;
                if (Changed(cGroup, grp.Text.Trim(), out v)) foreach (Account x in list) x.Group = v;
                if (Changed(cPass, pass.Text, out v)) foreach (Account x in list) x.Password = v;
                if (Changed(cGame, place.Text.Trim(), out v))
                {
                    long pid = Util.ParsePlaceId(v);
                    foreach (Account x in list)
                    {
                        x.GameLink = v;
                        x.SavedPlaceId = pid > 0 ? pid.ToString() : "";
                    }
                }
                if (Changed(cJob, job.Text.Trim(), out v)) foreach (Account x in list) x.SavedJobId = v;
                if (cFps < 0 ? fpsTouched : (int)fps.Value != cFps) foreach (Account x in list) x.FpsCap = (int)fps.Value;
                if (cookie != null)
                {
                    List<string> cs = Util.ExtractCookies(cookie.Text);
                    if (cs.Count > 0)
                    {
                        a.Cookie = cs[0];
                        a.LoginLost = false;
                        AppState.Live(a).CookieBad = false;
                    }
                }
            }
            f.Dispose();
            return ok;
        }

        // The value all accounts share, or null when they differ.
        static string Common(List<Account> list, Func<Account, string> get)
        {
            string first = get(list[0]) ?? "";
            foreach (Account x in list)
                if ((get(x) ?? "") != first) return null;
            return first;
        }

        static string Mixed(string label, string common)
        {
            return common == null ? label + "  - different per account" : label;
        }

        // A shared field is written when it changed; a 'different' field only when something was typed into it.
        static bool Changed(string common, string now, out string value)
        {
            value = now;
            return common == null ? now.Length > 0 : now != common;
        }

        static RoundTextBox AddField(Form f, string label, string value, ref int y)
        {
            Put(f, Ui.Lbl(label, "muted"), 16, y);
            RoundTextBox t = Put(f, Ui.Txt(488, value ?? ""), 16, y + 18);
            y += 50;
            return t;
        }

        // Returns the new rectangle, or null if cancelled.
        public static Rectangle? PinRect(IWin32Window owner, Rectangle current)
        {
            Form f = Make("Pin custom position / size", 380, 250);
            Put(f, Ui.Lbl("This window is placed exactly here (in pixels) and is left out of the grid.", "muted"), 16, 14);
            int y = 50;
            NumericUpDown nx = Put(f, Ui.Num(-20000, 20000, current.X, 90), 90, y);
            Put(f, Ui.Lbl("X", "muted"), 16, y + 3);
            NumericUpDown ny = Put(f, Ui.Num(-20000, 20000, current.Y, 90), 280, y);
            Put(f, Ui.Lbl("Y", "muted"), 220, y + 3);
            y += 40;
            NumericUpDown nw = Put(f, Ui.Num(200, 20000, Math.Max(200, current.Width), 90), 90, y);
            Put(f, Ui.Lbl("Width", "muted"), 16, y + 3);
            NumericUpDown nh = Put(f, Ui.Num(150, 20000, Math.Max(150, current.Height), 90), 280, y);
            Put(f, Ui.Lbl("Height", "muted"), 220, y + 3);
            OkCancel(f, "Pin");
            Theme.Apply(f);
            Rectangle? result = null;
            if (f.ShowDialog(owner) == DialogResult.OK)
                result = new Rectangle((int)nx.Value, (int)ny.Value, (int)nw.Value, (int)nh.Value);
            f.Dispose();
            return result;
        }
    }
}

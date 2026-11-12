using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace RobloxMultiHub
{
    public static class ServerSort
    {
        public static readonly string[] Names = new string[]
        {
            "Fewest players first", "Most players first", "Lowest ping first", "Highest FPS first"
        };

        static int PingKey(ServerInfo s) { return s.Ping > 0 ? s.Ping : int.MaxValue; }   // unknown ping goes last

        public static List<ServerInfo> Apply(List<ServerInfo> list, int mode)
        {
            if (mode == 1) return list.OrderByDescending(s => s.Playing).ThenBy(s => PingKey(s)).ToList();
            if (mode == 2) return list.OrderBy(s => PingKey(s)).ThenBy(s => s.Playing).ToList();
            if (mode == 3) return list.OrderByDescending(s => s.Fps).ThenBy(s => PingKey(s)).ToList();
            return list.OrderBy(s => s.Playing).ThenBy(s => PingKey(s)).ToList();
        }

        public static string[] Row(ServerInfo si)
        {
            return new string[]
            {
                si.Id,
                si.Playing + " / " + si.Max,
                si.Ping > 0 ? si.Ping + " ms" : "-",
                si.Fps > 0 ? si.Fps.ToString("0") : "-"
            };
        }
    }

    public static class Pickers
    {
        // Popup: type a game name, pick a result. Returns null when cancelled.
        public static GameInfo PickGame(IWin32Window owner, string cookie)
        {
            Form f = Dialogs.Make("Search a game", 640, 468);
            RoundTextBox q = Dialogs.Put(f, Ui.Txt(400, ""), 16, 16);
            RoundButton go = Dialogs.Put(f, Ui.Btn("Search", 90, null, "primary"), 424, 14);
            BufferedListView list = new BufferedListView();
            list.Size = new Size(608, 310);
            list.Columns.Add("Game", 330);
            list.Columns.Add("Players", 100);
            list.Columns.Add(Lang.T("Place ID"), 150);
            list.MultiSelect = false;
            ListStyle.Attach(list);
            Dialogs.Put(f, list, 16, 58);
            Label status = Dialogs.Put(f, Ui.Lbl("Type part of a game's name and press Enter.", "muted"), 16, 376);
            GameInfo chosen = null;
            RoundButton use = Dialogs.Put(f, Ui.Btn(Lang.T("Use this game"), 130, null, "primary"), f.ClientSize.Width - 130 - 100 - 28, f.ClientSize.Height - 46);
            RoundButton cancel = Dialogs.Put(f, Ui.Btn(Lang.T("Cancel"), 90, null, ""), f.ClientSize.Width - 90 - 16, f.ClientSize.Height - 46);
            cancel.DialogResult = DialogResult.Cancel;
            f.CancelButton = cancel;

            bool busy = false;
            EventHandler search = async delegate
            {
                string text = q.Text.Trim();
                if (text.Length == 0 || busy) return;
                busy = true;
                status.Text = Lang.T("Searching...");
                list.Items.Clear();
                List<GameInfo> res = await Api.SearchGames(text, cookie);
                foreach (GameInfo g in res)
                {
                    ListViewItem it = new ListViewItem(new string[] { g.Name, g.Players.ToString("N0"), g.PlaceId.ToString() });
                    it.Tag = g;
                    list.Items.Add(it);
                }
                status.Text = res.Count == 0 ? "No results. You can also type the Place ID directly on the Launch page." : res.Count + " result(s). Select one and press 'Use this game'.";
                busy = false;
            };
            go.Click += search;
            q.KeyDown += delegate(object s, KeyEventArgs e)
            {
                if (e.KeyCode == Keys.Enter) { e.SuppressKeyPress = true; search(null, EventArgs.Empty); }
            };
            EventHandler accept = delegate
            {
                if (list.SelectedItems.Count == 0) { status.Text = Lang.T("Select a game in the list first."); return; }
                chosen = list.SelectedItems[0].Tag as GameInfo;
                f.DialogResult = DialogResult.OK;
            };
            use.Click += accept;
            list.DoubleClick += accept;

            Theme.Apply(f);
            f.ActiveControl = q;
            f.ShowDialog(owner);
            f.Dispose();
            return chosen;
        }

        // Popup: list of running servers, sortable by players / ping / FPS. Returns the Job ID or null.
        public static string PickServer(IWin32Window owner, long placeId, string cookie)
        {
            Form f = Dialogs.Make("Pick a server - place " + placeId, 720, 540);
            DarkCombo sort = Dialogs.Put(f, Ui.Cmb(190, ServerSort.Names), 16, 14);
            RoundButton refresh = Dialogs.Put(f, Ui.Btn("Refresh", 90, null, ""), 216, 12);
            RoundButton more = Dialogs.Put(f, Ui.Btn(Lang.T("Load more"), 100, null, ""), 316, 12);
            more.Enabled = false;
            BufferedListView list = new BufferedListView();
            list.Size = new Size(688, 370);
            list.Columns.Add(Lang.T("Server (Job ID)"), 330);
            list.Columns.Add("Players", 100);
            list.Columns.Add("Ping", 100);
            list.Columns.Add("FPS", 100);
            list.MultiSelect = false;
            ListStyle.Attach(list);
            Dialogs.Put(f, list, 16, 56);
            Label status = Dialogs.Put(f, Ui.Lbl(Lang.T("Loading servers..."), "muted"), 16, 436);
            string chosen = null;
            RoundButton use = Dialogs.Put(f, Ui.Btn("Use this server", 140, null, "primary"), f.ClientSize.Width - 140 - 100 - 28, f.ClientSize.Height - 46);
            RoundButton cancel = Dialogs.Put(f, Ui.Btn("Cancel", 90, null, ""), f.ClientSize.Width - 90 - 16, f.ClientSize.Height - 46);
            cancel.DialogResult = DialogResult.Cancel;
            f.CancelButton = cancel;

            List<ServerInfo> all = new List<ServerInfo>();
            string next = "";
            bool busy = false;

            Action fill = delegate
            {
                list.BeginUpdate();
                list.Items.Clear();
                foreach (ServerInfo si in ServerSort.Apply(all, Math.Max(0, sort.SelectedIndex)))
                {
                    ListViewItem it = new ListViewItem(ServerSort.Row(si));
                    it.Tag = si;
                    list.Items.Add(it);
                }
                list.EndUpdate();
            };

            Func<bool, Task> load = async delegate(bool append)
            {
                if (busy) return;
                busy = true;
                status.Text = "Loading servers...";
                if (!append) { all.Clear(); next = ""; }
                int mode = Math.Max(0, sort.SelectedIndex);
                int pages = (!append && mode >= 2) ? 3 : 1;     // ping / FPS need a bigger sample to sort well
                string err = "";
                for (int i = 0; i < pages; i++)
                {
                    ServerPage sp = await Api.Servers(placeId, mode == 0, (i == 0 && !append) ? "" : next, cookie);
                    if (sp.Error.Length > 0) { err = sp.Error; break; }
                    all.AddRange(sp.Servers);
                    next = sp.Next;
                    if (next.Length == 0) break;
                }
                fill();
                more.Enabled = next.Length > 0;
                status.Text = err.Length > 0 ? "Error: " + err : all.Count + " server(s) loaded" + (next.Length > 0 ? " (more available)." : ".");
                busy = false;
            };

            sort.SelectedIndexChanged += delegate { load(false); };
            refresh.Click += delegate { load(false); };
            more.Click += delegate { load(true); };
            EventHandler accept = delegate
            {
                if (list.SelectedItems.Count == 0) { status.Text = Lang.T("Select a server in the list first."); return; }
                ServerInfo si = list.SelectedItems[0].Tag as ServerInfo;
                if (si == null) return;
                chosen = si.Id;
                f.DialogResult = DialogResult.OK;
            };
            use.Click += accept;
            list.DoubleClick += accept;
            f.Shown += delegate { load(false); };

            Theme.Apply(f);
            f.ShowDialog(owner);
            f.Dispose();
            return chosen;
        }
    }
}

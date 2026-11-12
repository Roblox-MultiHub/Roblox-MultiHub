using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;

namespace RobloxMultiHub
{
    public partial class MainForm
    {
        RoundTextBox tbGameQ;
        ListView lvGames;
        RoundTextBox tbSrvPlace;
        ComboBox cbSrvSort;
        ListView lvServers;
        Button btnMore;
        string srvNext = "";
        long srvPlace;

        void BuildServersPage()
        {
            Panel page = NewPage("Servers");
            PageBuilder p = new PageBuilder(page, PageW);
            p.Title("Servers && Games", "Find a game and send accounts into a server.");

            p.Section("Find a game");
            tbGameQ = Ui.Txt(300, "");
            tbGameQ.KeyDown += delegate(object s, KeyEventArgs e)
            {
                if (e.KeyCode == Keys.Enter) { e.SuppressKeyPress = true; SearchGames(); }
            };
            p.Opt("Search Roblox games by name.",
                "Type part of a game's name and press Enter. The results show players online and the Place ID.\n\nSearching is optional - you can always paste a Place ID or game link on the Launch page directly.",
                Ui.Fld("Search", 60), tbGameQ, Ui.Btn("Search", 90, delegate { SearchGames(); }, "primary"));

            lvGames = new BufferedListView();
            lvGames.Size = new Size(PageW - PageBuilder.PadX * 2, 150);
            lvGames.Columns.Add("Game", 290);
            lvGames.Columns.Add("Players", 90);
            lvGames.Columns.Add(Lang.T("Place ID"), 110);
            lvGames.MultiSelect = false;
            ListStyle.Attach(lvGames);
            p.Add(lvGames);
            p.Opt("Use a result as the game.",
                "'Use this game' puts its Place ID on the Launch page. 'Show its servers' also loads the running servers of that game in the server browser below.",
                Ui.Btn(Lang.T("Use this game"), 130, delegate { UseSelectedGame(false); }, "primary"),
                Ui.Btn("Show its servers", 140, delegate { UseSelectedGame(true); }, ""));

            p.Section("Server browser");
            tbSrvPlace = Ui.Txt(120, "");
            cbSrvSort = Ui.Cmb(190, ServerSort.Names);
            p.Opt("Lists the running servers.",
                "Shows public servers with players, ping and FPS. Sort them by fewest players, most players, lowest ping or highest FPS (ping and FPS sorting looks at the first 300 servers; press Load more for further ones).\n\nRoblox rate-limits this list. If you see an error, wait a minute and try again.",
                Ui.Fld("Place ID", 66), tbSrvPlace, cbSrvSort, Ui.Btn("Load servers", 100, delegate { LoadServers(false); }, ""));

            lvServers = new BufferedListView();
            lvServers.Size = new Size(PageW - PageBuilder.PadX * 2, 210);
            lvServers.Columns.Add(Lang.T("Server (Job ID)"), 270);
            lvServers.Columns.Add("Players", 80);
            lvServers.Columns.Add("Ping", 60);
            lvServers.Columns.Add("FPS", 60);
            lvServers.MultiSelect = false;
            ListStyle.Attach(lvServers);
            p.Add(lvServers);

            btnMore = Ui.Btn(Lang.T("Load more"), 100, delegate { LoadServers(true); }, "");
            btnMore.Enabled = false;
            p.Opt("Sends the selection into that server.",
                "'Join with selected accounts' launches every account you selected on the left into exactly this server, so they all end up together.",
                Ui.Btn("Join with selected accounts", 200, delegate { JoinSelectedServer(); }, "primary"), btnMore);
            p.Opt("Reuse or copy the Job ID.",
                "'Use Job ID on Launch page' fills in the Launch page (specific server mode). 'Copy Job ID' puts the ID on your clipboard.",
                Ui.Btn("Use Job ID on Launch page", 200, delegate { UseSelectedServer(); }, ""),
                Ui.Btn("Copy Job ID", 110, delegate { CopySelectedServer(); }, ""));
            cbSrvSort.SelectedIndexChanged += delegate { if (srvPlace > 0) LoadServers(false); };
        }

        async void SearchGames()
        {
            string q = tbGameQ.Text.Trim();
            if (q.Length == 0) return;
            lvGames.Items.Clear();
            AddLog("Searching games for '" + q + "'...");
            List<GameInfo> res = await Api.SearchGames(q, AnyCookie());
            foreach (GameInfo g in res)
            {
                ListViewItem it = new ListViewItem(new string[] { g.Name, g.Players.ToString("N0"), g.PlaceId.ToString() });
                it.Tag = g;
                lvGames.Items.Add(it);
            }
            if (res.Count == 0) AddLog("No results (or Roblox changed its search endpoint). You can always paste a Place ID directly on the Launch page.");
        }

        void UseSelectedGame(bool showServers)
        {
            if (lvGames.SelectedItems.Count == 0) { AddLog(Lang.T("Select a game in the list first.")); return; }
            GameInfo g = lvGames.SelectedItems[0].Tag as GameInfo;
            if (g == null) return;
            cbPlace.Text = g.PlaceId.ToString();
            tbSrvPlace.Text = g.PlaceId.ToString();
            AddLog("Place ID " + g.PlaceId + " (" + g.Name + ") set on the Launch page.");
            if (showServers) LoadServers(false);
        }

        List<ServerInfo> srvAll = new List<ServerInfo>();

        void FillServers()
        {
            lvServers.BeginUpdate();
            lvServers.Items.Clear();
            foreach (ServerInfo si in ServerSort.Apply(srvAll, Math.Max(0, cbSrvSort.SelectedIndex)))
            {
                ListViewItem it = new ListViewItem(ServerSort.Row(si));
                it.Tag = si;
                lvServers.Items.Add(it);
            }
            lvServers.EndUpdate();
        }

        async void LoadServers(bool more)
        {
            long pid = Util.ParsePlaceId(tbSrvPlace.Text);
            if (pid <= 0) pid = Util.ParsePlaceId(cbPlace.Text);
            if (pid <= 0) { AddLog(Lang.T("Enter a Place ID first.")); return; }
            tbSrvPlace.Text = pid.ToString();
            if (!more || pid != srvPlace)
            {
                srvAll.Clear();
                srvNext = "";
                more = false;
            }
            srvPlace = pid;
            int mode = Math.Max(0, cbSrvSort.SelectedIndex);
            int pages = (!more && mode >= 2) ? 3 : 1;      // ping / FPS sorting needs a bigger sample
            AddLog("Loading servers of place " + pid + "...");
            string err = "";
            for (int i = 0; i < pages; i++)
            {
                ServerPage sp = await Api.Servers(pid, mode == 0, (i == 0 && !more) ? "" : srvNext, AnyCookie());
                if (sp.Error.Length > 0) { err = sp.Error; break; }
                srvAll.AddRange(sp.Servers);
                srvNext = sp.Next;
                if (srvNext.Length == 0) break;
            }
            FillServers();
            btnMore.Enabled = srvNext.Length > 0;
            if (err.Length > 0) AddLog("Could not load servers: " + err);
            else AddLog("Loaded " + srvAll.Count + " server(s)" + (srvNext.Length > 0 ? " (more available)." : "."));
        }

        ServerInfo SelectedServer()
        {
            if (lvServers.SelectedItems.Count == 0) { AddLog(Lang.T("Select a server in the list first.")); return null; }
            return lvServers.SelectedItems[0].Tag as ServerInfo;
        }

        void JoinSelectedServer()
        {
            ServerInfo s = SelectedServer();
            if (s == null) return;
            JoinServerWithSelected(srvPlace, s.Id);
        }

        void UseSelectedServer()
        {
            ServerInfo s = SelectedServer();
            if (s == null) return;
            cbPlace.Text = srvPlace.ToString();
            tbJob.Text = s.Id;
            rbJob.Checked = true;
            ShowPage("Launch");
            AddLog("Job ID filled in on the Launch page.");
        }

        void CopySelectedServer()
        {
            ServerInfo s = SelectedServer();
            if (s == null) return;
            Util.Copy(s.Id);
            AddLog("Job ID copied.");
        }
    }
}

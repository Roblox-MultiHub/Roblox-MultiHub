using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace RobloxMultiHub
{
    // At start-up every saved login is checked once. Accounts that Roblox signed out since the last check
    // (password changed, logged out everywhere, account locked or reset) are shown in one popup.
    public partial class MainForm
    {
        async void CheckLoginsOnStartup()
        {
            await Task.Delay(4000);                         // let the window and the presence check start first
            List<Account> list = Store.Accounts.Where(a => !string.IsNullOrEmpty(a.Cookie)).ToList();
            if (list.Count == 0 || IsDisposed) return;
            AddLog("Checking whether the " + list.Count + " saved login(s) still work...");
            List<Account> lost = new List<Account>();
            List<Account> stillLost = new List<Account>();
            int ok = 0;
            int failures = 0;
            foreach (Account a in list)
            {
                UserInfo u = await Api.Authenticated(a.Cookie);
                if (!u.Valid && u.Error.Contains("429"))
                {
                    await Task.Delay(8000);                 // rate limited: wait and try this account once more
                    u = await Api.Authenticated(a.Cookie);
                }
                LiveInfo li = AppState.Live(a);
                if (u.Valid)
                {
                    ok++;
                    failures = 0;
                    a.Username = u.Name;
                    a.DisplayName = u.Display;
                    li.CookieBad = false;
                    li.Locked = false;
                    if (a.LoginLost) { a.LoginLost = false; AddLog(AppState.Name(a) + " is logged in again."); }
                }
                else if (u.Locked)                          // HTTP 403 = account locked / blocked by Roblox
                {
                    failures = 0;
                    li.Locked = true;
                    li.CookieBad = false;
                    if (a.LoginLost) stillLost.Add(a);
                    else { a.LoginLost = true; lost.Add(a); }
                }
                else if (u.Error.StartsWith(Lang.T("Cookie expired")))
                {
                    failures = 0;
                    li.CookieBad = true;
                    li.Locked = false;
                    if (a.LoginLost) stillLost.Add(a);
                    else { a.LoginLost = true; lost.Add(a); }
                }
                else if (++failures >= 3)
                {
                    AddLog("Login check stopped: Roblox could not be reached (" + u.Error + "). It runs again next time MultiHub starts.");
                    break;
                }
                await Task.Delay(350);
            }
            Store.SaveAccounts();
            if (IsDisposed) return;
            RefreshAccountList();
            int bad = lost.Count + stillLost.Count;
            AddLog("Login check done: " + ok + " working" + (bad > 0 ? ", " + bad + " with a problem (shown in red)." : "."));
            if (stillLost.Count > 0)
                AddLog("Still not working (you were told before): " + string.Join(", ", stillLost.Select(x => AppState.Name(x) + (AppState.Live(x).Locked ? " (locked)" : " (signed out)")).ToArray()) + ".");
            if (lost.Count > 0) ShowLostLogins(lost);
        }

        void ShowLostLogins(List<Account> lost)
        {
            List<Account> locked = lost.Where(a => AppState.Live(a).Locked).ToList();
            List<Account> signedOut = lost.Where(a => !AppState.Live(a).Locked).ToList();
            StringBuilder sb = new StringBuilder();

            if (locked.Count > 0)
            {
                sb.AppendLine(locked.Count == 1
                    ? "This account looks LOCKED or blocked by Roblox (it returned HTTP 403):"
                    : "These " + locked.Count + " accounts look LOCKED or blocked by Roblox (they returned HTTP 403):");
                foreach (Account a in locked)
                    sb.AppendLine("   -  " + DisplayName(a) + "   (@" + a.Username + ")");
                sb.AppendLine();
                sb.AppendLine("A 403 when checking or launching an account almost always means Roblox has locked, restricted or banned it. Open the account on roblox.com and follow Roblox's steps to unlock it (verify email / reset password). Running many accounts from one PC makes this more likely.");
                sb.AppendLine();
            }
            if (signedOut.Count > 0)
            {
                sb.AppendLine(signedOut.Count == 1
                    ? "Roblox signed this account out since the last check:"
                    : "Roblox signed these " + signedOut.Count + " accounts out since the last check:");
                foreach (Account a in signedOut)
                    sb.AppendLine("   -  " + DisplayName(a) + "   (@" + a.Username + ")");
                sb.AppendLine();
                sb.AppendLine("Their saved login no longer works (password changed, logged out everywhere, or reset by Roblox). Log in again: '+ Add account' > 'Log in with a browser'. The existing entry is updated - its group, game and settings are kept.");
                sb.AppendLine();
            }
            sb.AppendLine("Tip: add a verified email address (and 2-step verification) to every account in Roblox's account settings. Roblox can lock accounts used a lot from one PC (players report it from about 8 accounts). With a verified email you can always get the account back.");
            string title = locked.Count > 0 && signedOut.Count == 0 ? "Roblox MultiHub - accounts locked" : "Roblox MultiHub - account problems";
            MessageBox.Show(this, sb.ToString(), title, MessageBoxButtons.OK, MessageBoxIcon.Warning);
        }
    }
}

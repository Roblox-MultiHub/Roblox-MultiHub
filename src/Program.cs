using System;
using System.Threading;
using System.Windows.Forms;

namespace RobloxMultiHub
{
    static class Program
    {
        static Mutex single;

        [STAThread]
        static void Main(string[] args)
        {
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            Application.SetUnhandledExceptionMode(UnhandledExceptionMode.CatchException);
            Application.ThreadException += delegate(object s, ThreadExceptionEventArgs e) { OnCrash(e.Exception); };
            AppDomain.CurrentDomain.UnhandledException += delegate(object s, UnhandledExceptionEventArgs e) { OnCrash(e.ExceptionObject as Exception); };

            // The RAM cleaner runs as a separate administrator copy of this exe without any window.
            int helper = Array.IndexOf(args, "--memhelper");
            if (helper >= 0 || Array.IndexOf(args, "--memclean") >= 0)
            {
                try
                {
                    int dd = Array.IndexOf(args, "--datadir");
                    Store.Init(dd >= 0 && dd + 1 < args.Length ? args[dd + 1] : null);
                    int parent;
                    if (helper >= 0 && helper + 1 < args.Length && int.TryParse(args[helper + 1], out parent)) MemClean.RunHelper(parent);
                    else if (helper < 0) MemClean.RunOnce();
                }
                catch (Exception ex) { Log.Error("memory cleaner", ex); }
                return;
            }

            // Two copies of this app would fight over the Roblox lock, so only allow one.
            bool created;
            single = new Mutex(true, "RobloxMultiHub_SingleInstance_v1", out created);
            if (!created)
            {
                MessageBox.Show("Roblox MultiHub is already running. Look for it in your taskbar.", "Roblox MultiHub", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            Store.Init();
            Theme.Set(Store.Settings.Theme != "Light");
            if (!Unlock()) return;

            bool minimized = Array.IndexOf(args, "--minimized") >= 0;
            Application.Run(new MainForm(minimized));
            MultiInstance.Stop();
            GC.KeepAlive(single);
        }

        static bool Unlock()
        {
            try
            {
                if (Store.AccountsFileIsPasswordProtected())
                {
                    while (true)
                    {
                        string pw = Dialogs.AskPassword(null, "Roblox MultiHub - unlock", "Enter the password of your accounts file:", false);
                        if (pw == null) return false;
                        string err = Store.LoadAccounts(pw);
                        if (err == null) return true;
                        MessageBox.Show(err, "Roblox MultiHub", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    }
                }
                string problem = Store.LoadAccounts(null);
                if (problem == null) return true;
                DialogResult r = MessageBox.Show(problem + "\r\n\r\nYES = start with an empty account list (the old file is kept as accounts.dat.old)\r\nNO = quit",
                    "Roblox MultiHub", MessageBoxButtons.YesNo, MessageBoxIcon.Warning);
                if (r == DialogResult.Yes) { Store.StartFresh(); return true; }
                return false;
            }
            catch (Exception ex)
            {
                OnCrash(ex);
                return false;
            }
        }

        static void OnCrash(Exception ex)
        {
            try
            {
                if (ex != null) Log.Error("UNHANDLED", ex);
                MessageBox.Show("Something went wrong:\r\n\r\n" + (ex == null ? "unknown error" : ex.Message)
                    + "\r\n\r\nDetails are in log.txt (Settings > Open data folder). The app keeps running if it can.",
                    "Roblox MultiHub", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
            catch (Exception) { }
        }
    }
}

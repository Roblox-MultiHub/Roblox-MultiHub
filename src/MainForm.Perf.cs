using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;

namespace RobloxMultiHub
{
    public partial class MainForm
    {
        ComboBox cbPreset, cbQuality, cbTexture, cbMsaa, cbApi, cbPriority;
        CheckBox chkUnlisted;
        MultiCombo mcTweaks;
        CheckBox chkMaxAlts, chkMaxIdle, chkMaxTrim, chkMaxMin;
        TextBox tbPreview, tbCustomFlags;
        Label lblPerfLive;
        NumericUpDown numFps;
        bool loadingPerf;
        NumericUpDown numMaxRam;
        RoundButton btnProbe;
        Label lblProbe;

        static readonly string[] PresetKeys = new string[] { "Default", "Light", "LowCpu", "Lowest", "Custom" };
        static readonly int[] QualityValues = new int[] { 0, 1, 2, 3, 4, 5, 6, 8, 10, 15, 21 };
        static readonly int[] MsaaValues = new int[] { 0, 1, 2, 4, 8 };
        static readonly string[] ApiKeys = new string[] { "Default", "D3D11", "Vulkan", "OpenGL" };
        static readonly string[] PriorityKeys = new string[] { "Normal", "BelowNormal", "Idle" };

        void BuildPerfPage()
        {
            loadingPerf = true;
            GfxSettings G = Store.Settings.Gfx;
            Panel page = NewPage("Perf");
            PageBuilder p = new PageBuilder(page, PageW);
            p.Title("Performance", "Make every Roblox window cheaper for your PC: less CPU and less memory (RAM).");

            lblPerfLive = Ui.Lbl("", "good");
            p.Row(lblPerfLive);

            BuildMemorySection(p);
            BuildMaxAltsSection(p);
            BuildRamCapSection(p);

            p.Section("Frame rate");
            numFps = Ui.Num(0, 1000, Store.Settings.FpsCap, 80);
            p.Opt("Caps FPS of every window (0 = off).",
                "Caps how many frames per second each Roblox window renders. Lower values use much less CPU and GPU - 30 is a good value for many accounts.\n\nThis changes Roblox's own settings file (the same 'FramerateCap' value you can edit by hand in GlobalBasicSettings_13.xml) right before every launch, so it applies to every account you launch until you change it here. Roblox reads it when a window starts: windows that are already open keep their old cap until you relaunch them. Setting 0 gives control back to Roblox.",
                Ui.Fld("FPS cap", 120), numFps);

            p.Section("Roblox graphics");
            cbPreset = Ui.Cmb(330, "Roblox default  (change nothing)", "Light", "Low CPU / GPU  (recommended for many accounts)", "Lowest possible", "Custom");
            p.Opt("Quick presets for everything below.",
                "A preset simply fills in the options below. Change a single option and the preset switches to 'Custom'.\n\nThe settings are written into Roblox right before every launch, so they apply to EVERY account you launch from this app until you change them here again.",
                Ui.Fld("Preset", 120), cbPreset);
            cbQuality = Ui.Cmb(200, "Roblox default", "1  (lowest)", "2", "3", "4", "5", "6", "8", "10", "15", "21  (highest)");
            p.Opt("Lower = less GPU and CPU.",
                "Forces Roblox's graphics-quality level in every game. Low numbers remove most of the shadows, effects and detail, which typically cuts GPU and CPU work a lot. 'Roblox default' leaves the decision to Roblox.",
                Ui.Fld("Graphics quality", 120), cbQuality);
            cbTexture = Ui.Cmb(200, "Roblox default", "0 - lowest", "1 - low", "2 - medium", "3 - high");
            p.Opt("Lower = less video and RAM.",
                "Overrides the texture quality. Lower texture quality means smaller textures in memory, so each window needs less video memory and RAM.",
                Ui.Fld("Texture quality", 120), cbTexture);
            cbMsaa = Ui.Cmb(200, "Roblox default", "Off", "2x", "4x", "8x");
            p.Opt("Smooths jagged edges; turning it off is cheaper.",
                "Anti-aliasing (MSAA) smooths jagged edges but costs GPU time. 'Off' is the cheapest. If it has no visible effect on your PC, leave it on 'Roblox default'.",
                Ui.Fld("Anti-aliasing", 120), cbMsaa);
            mcTweaks = new MultiCombo();
            mcTweaks.Width = 240;
            mcTweaks.Items = new string[] { "No grass", "Cheapest distant unions", "Pause lighting updates", "Plain grey sky" };
            mcTweaks.Checked = new bool[] { G.NoGrass, G.LowCsg, G.PauseVoxelizer, G.GraySky };
            mcTweaks.Changed += delegate { OnGfxChanged(); };
            p.Opt("Rendering saves - tick any combination.",
                "No grass: grass is not rendered at all.\nCheapest distant unions: union parts always use their lowest-detail model.\nPause lighting updates: saves CPU; shadows and lighting stop reacting to movement.\nPlain grey sky: replaces the default sky (small saving).",
                Ui.Fld("Tweaks", 120), mcTweaks);
            cbApi = Ui.Cmb(200, "Roblox default", "Direct3D 11", "Vulkan", "OpenGL");
            p.Opt("May crash on some PCs.",
                "Chooses which graphics API Roblox prefers. Another API can change CPU use, but it can also cause crashes or glitches on some PCs. Leave it on 'Roblox default' if you are unsure.",
                Ui.Fld("Graphics API", 120), cbApi);
            p.Opt("Go back to Roblox's own settings.",
                "Resets every option on this page, removes your own flags and deletes the settings file that this app wrote into Roblox's folder.",
                Ui.Btn("Restore Roblox defaults", 210, delegate { ResetGfx(); }, "danger"));

            p.Section("What will be written to Roblox");
            tbPreview = new TextBox();
            tbPreview.Multiline = true;
            tbPreview.ReadOnly = true;
            tbPreview.ScrollBars = ScrollBars.Vertical;
            tbPreview.Font = Theme.Mono;
            tbPreview.Size = new Size(PageW - PageBuilder.PadX * 2, 120);
            p.Add(tbPreview);
            p.Hint("Only flags from Roblox's official list are written.",
                "Since 29 September 2025 Roblox only honours a short official list of 'Fast Flags'. Every other flag is ignored by the game, so this section only writes flags from that list. (The FPS cap above does not use a flag: it changes Roblox's own settings file.)\n\nRoblox can change the list at any time.");

            p.Section("Your own flags");
            tbCustomFlags = new TextBox();
            tbCustomFlags.Multiline = true;
            tbCustomFlags.ScrollBars = ScrollBars.Vertical;
            tbCustomFlags.Font = Theme.Mono;
            tbCustomFlags.Size = new Size(PageW - PageBuilder.PadX * 2, 64);
            tbCustomFlags.Text = Store.Settings.CustomClientSettings;
            p.Add(tbCustomFlags);
            chkUnlisted = Ui.Chk("Also write flags that are not on Roblox's list", G.WriteUnlisted, null);
            p.Opt("JSON like {\"FFlagDebugSkyGray\": \"True\"}.",
                "Add your own flags as a JSON object. Flags that are not on Roblox's list are skipped, unless you tick the box: then they are written anyway (useful if Roblox extends its list later). Roblox ignores them as long as they are not on its list.",
                chkUnlisted);

            p.Section(Lang.T("CPU priority");
            cbPriority = Ui.Cmb(300, "Normal", "Below normal  (keeps your PC responsive)", "Idle  (lowest)");
            p.Opt("Stops many windows from slowing down your PC.",
                "Set automatically on every window this app launches. It does not make Roblox do less work, but other programs get CPU time first, so your PC stays responsive with many windows open. No Roblox files are touched.",
                Ui.Fld("Window priority", 120), cbPriority);
            p.Opt("Apply now, or minimize all.",
                "'Apply to running windows' sets the priority on the Roblox windows that are already open. 'Minimize all' minimizes every Roblox window - minimized windows usually use less GPU. 'Arrange now' on the Layout page brings them back.",
                Ui.Btn("Apply to running windows", 200, delegate { ApplyPriorityNow(); }, ""),
                Ui.Btn("Minimize all Roblox windows", 210, delegate { MinimizeAllRoblox(); }, ""));

            LoadGfxControls();

            EventHandler gfxChanged = delegate { OnGfxChanged(); };
            cbPreset.SelectedIndexChanged += delegate { OnPresetChanged(); };
            cbQuality.SelectedIndexChanged += gfxChanged;
            cbTexture.SelectedIndexChanged += gfxChanged;
            cbMsaa.SelectedIndexChanged += gfxChanged;
            cbApi.SelectedIndexChanged += gfxChanged;
            chkUnlisted.CheckedChanged += delegate { OnCustomFlagsChanged(); };
            tbCustomFlags.Leave += delegate { OnCustomFlagsChanged(); };
            cbPriority.SelectedIndexChanged += delegate { OnPriorityChanged(); };
            numFps.ValueChanged += delegate { if (loadingPerf) return; Store.Settings.FpsCap = (int)numFps.Value; Store.SaveSettings(); };

            loadingPerf = false;
            UpdatePreview();
        }

        // settings -> controls (events are muted while this runs)
        void LoadGfxControls()
        {
            bool was = loadingPerf;
            loadingPerf = true;
            GfxSettings G = Store.Settings.Gfx;
            cbPreset.SelectedIndex = Math.Max(0, Array.IndexOf(PresetKeys, G.Preset));
            cbQuality.SelectedIndex = Math.Max(0, Array.IndexOf(QualityValues, G.Quality));
            cbTexture.SelectedIndex = G.Texture < 0 ? 0 : Math.Min(4, G.Texture + 1);
            cbMsaa.SelectedIndex = Math.Max(0, Array.IndexOf(MsaaValues, G.Msaa));
            cbApi.SelectedIndex = Math.Max(0, Array.IndexOf(ApiKeys, G.Api));
            mcTweaks.Checked = new bool[] { G.NoGrass, G.LowCsg, G.PauseVoxelizer, G.GraySky };
            mcTweaks.Invalidate();
            chkUnlisted.Checked = G.WriteUnlisted;
            cbPriority.SelectedIndex = Math.Max(0, Array.IndexOf(PriorityKeys, Store.Settings.Priority));
            loadingPerf = was;
        }

        void OnPresetChanged()
        {
            if (loadingPerf) return;
            string key = PresetKeys[Math.Max(0, cbPreset.SelectedIndex)];
            if (key != "Custom")
            {
                Gfx.ApplyPreset(Store.Settings.Gfx, key);
                LoadGfxControls();
                AddLog("Graphics preset applied: it takes effect for every account you launch from now on.");
            }
            else Store.Settings.Gfx.Preset = "Custom";
            Store.SaveSettings();
            UpdatePreview();
        }

        // controls -> settings, after the user changed a single option
        void OnGfxChanged()
        {
            if (loadingPerf) return;
            GfxSettings G = Store.Settings.Gfx;
            G.Quality = QualityValues[Math.Max(0, cbQuality.SelectedIndex)];
            G.Texture = cbTexture.SelectedIndex <= 0 ? -1 : cbTexture.SelectedIndex - 1;
            G.Msaa = MsaaValues[Math.Max(0, cbMsaa.SelectedIndex)];
            G.Api = ApiKeys[Math.Max(0, cbApi.SelectedIndex)];
            G.NoGrass = mcTweaks.IsChecked(0);
            G.LowCsg = mcTweaks.IsChecked(1);
            G.PauseVoxelizer = mcTweaks.IsChecked(2);
            G.GraySky = mcTweaks.IsChecked(3);
            G.Preset = "Custom";
            loadingPerf = true;
            cbPreset.SelectedIndex = PresetKeys.Length - 1;
            loadingPerf = false;
            Store.SaveSettings();
            UpdatePreview();
        }

        void OnCustomFlagsChanged()
        {
            if (loadingPerf) return;
            Store.Settings.Gfx.WriteUnlisted = chkUnlisted.Checked;
            string t = tbCustomFlags.Text.Trim();
            Store.Settings.CustomClientSettings = t;
            if (t.Length > 0)
            {
                try { if (Json.Obj(Json.Parse(t)) == null) AddLog("Your own flags must be a JSON object like {\"FlagName\": \"True\"}."); }
                catch (Exception) { AddLog("Your own flags are not valid JSON - they will be ignored until fixed."); }
            }
            Store.SaveSettings();
            UpdatePreview();
        }

        void UpdatePreview()
        {
            if (tbPreview != null) tbPreview.Text = Gfx.Preview();
        }

        void ResetGfx()
        {
            Gfx.ApplyPreset(Store.Settings.Gfx, "Default");
            Store.Settings.Gfx.Api = "Default";
            Store.Settings.Gfx.WriteUnlisted = false;
            Store.Settings.CustomClientSettings = "";
            Store.Settings.FpsCap = 0;
            tbCustomFlags.Text = "";
            loadingPerf = true;
            numFps.Value = 0;
            loadingPerf = false;
            LoadGfxControls();
            Store.SaveSettings();
            UpdatePreview();
            AddLog(Launcher.RemoveClientSettings());
            Launcher.ApplyFpsCap(AddLog);
        }

        void OnPriorityChanged()
        {
            if (loadingPerf) return;
            Store.Settings.Priority = PriorityKeys[Math.Max(0, cbPriority.SelectedIndex)];
            Store.SaveSettings();
        }

        void ApplyPriorityNow()
        {
            int n = Launcher.ApplyPriorityAll();
            AddLog("CPU priority (" + Store.Settings.Priority + ") applied to " + n + " running Roblox window(s).");
        }

        void MinimizeAllRoblox()
        {
            int n = 0;
            foreach (WinInfo w in WindowLayout.FindRobloxWindows())
            {
                if (!w.HasWindow) continue;
                WindowLayout.Minimize(w.Hwnd);
                n++;
            }
            AddLog("Minimized " + n + " Roblox window(s).");
            RefreshWindowList();
        }

        void BuildMaxAltsSection(PageBuilder p)
        {
            p.Section("Max alts  (throttle the windows you are not playing)");
            chkMaxAlts = Ui.Chk("Turn on Max alts", Store.Settings.MaxAlts, null);
            p.Opt("Your window stays full speed; the rest slow down.",
                "OFF by default. While this is on, the Roblox window that currently has your focus runs at full speed, and every OTHER Roblox window this app sees is made cheap for your PC: Windows gives it CPU time last and runs it in 'efficiency mode' (the same EcoQoS leaf Task Manager shows), and its memory is trimmed now and then. The moment you click a different Roblox window, that one speeds up and the previous one slows down.\n\nNothing inside Roblox is touched - only the standard Windows process controls. Best for idling or auto-farming many accounts while you actively play one. Roblox can protect a process; windows it protects are simply left alone.",
                chkMaxAlts);
            chkMaxIdle = Ui.Chk("Idle CPU priority + efficiency mode for background windows", Store.Settings.MaxAltsIdle, null);
            p.Opt("The main saving.",
                "Background clients get the lowest CPU priority and Windows' efficiency mode, so the client you are playing always gets the CPU first. Recommended on.",
                chkMaxIdle);
            chkMaxTrim = Ui.Chk("Trim the memory of background windows", Store.Settings.MaxAltsTrim, null);
            p.Opt("Frees unused RAM of background windows.",
                "Empties the working set of each background client, which pushes memory it is not actively using back to Windows. No admin rights needed - it only touches the clients this app launched.",
                chkMaxTrim);
            chkMaxMin = Ui.Chk("Also minimize background windows", Store.Settings.MaxAltsMinimize, null);
            p.Opt("Minimized Roblox windows usually render even less.",
                "Optional. Minimizes every client except the one you are in. It fights with the Layout page (which arranges windows on screen), so leave it off if you tile your windows.",
                chkMaxMin);

            chkMaxAlts.CheckedChanged += delegate
            {
                if (loadingPerf) return;
                Store.Settings.MaxAlts = chkMaxAlts.Checked;
                Store.SaveSettings();
                if (chkMaxAlts.Checked) { AddLog("Max alts ON: the window you are not playing is throttled."); MaxAlts.Tick(); }
                else { MaxAlts.Reset(); AddLog("Max alts OFF: every Roblox window runs at full speed again."); }
            };
            chkMaxIdle.CheckedChanged += delegate { if (loadingPerf) return; Store.Settings.MaxAltsIdle = chkMaxIdle.Checked; Store.SaveSettings(); if (!chkMaxIdle.Checked) MaxAlts.Reset(); };
            chkMaxTrim.CheckedChanged += delegate { if (loadingPerf) return; Store.Settings.MaxAltsTrim = chkMaxTrim.Checked; Store.SaveSettings(); };
            chkMaxMin.CheckedChanged += delegate { if (loadingPerf) return; Store.Settings.MaxAltsMinimize = chkMaxMin.Checked; Store.SaveSettings(); };
        }

        void BuildRamCapSection(PageBuilder p)
        {
            p.Section("Lang.T(Lang.T(Max RAM per window");
            numMaxRam = Ui.Num(0, 32768, Math.Max(0, Store.Settings.MaxRamMb), 80);
            p.Opt("Maximum RAM per window (0 = off).",
                "Windows stops every window from keeping more than this many MB of RAM: pages the game is not using right now are pushed out to the pagefile and stay there until the game really needs them. The RAM counter of each window stays near this number instead of growing all day.\n\n"
                + "Start high (800 MB is a good first try) and lower it step by step; when a window starts to stutter you went too low, go a little back up. 0 turns it off.\n\n"
                + "The cap is applied to every Roblox window within about 2 seconds (also right after it starts) and is removed when MultiHub closes.",
                Ui.Fld("Max RAM (MB)", 110), numMaxRam);
            numMaxRam.ValueChanged += delegate
            {
                if (loadingPerf) return;
                Store.Settings.MaxRamMb = (int)numMaxRam.Value;
                Store.SaveSettings();
                RamCap.Tick();
            };

            btnProbe = Ui.Btn("Find my real cap", 160, delegate
            {
                if (UsageMonitor.Count == 0) { AddLog("Cap probe: launch your accounts first - it measures them while they idle."); return; }
                CapProber.Start(AddLog);
            }, "primary");
            p.Opt("Measures your clients to find the real floor.",
                "While your accounts idle like normal, this lowers 'Max RAM per window' by 50 MB every ~3 minutes and watches hard page faults (pages being read back from disk). Above the floor they stay near zero; below it they climb - the last cap that stayed clean IS your number, measured on your PC with your games. It ends there automatically; click again to stop early.",
                btnProbe);
            lblProbe = Ui.Lbl(Store.Settings.RamCapFloorMb > 0
                ? "Measured floor on this PC: " + Store.Settings.RamCapFloorMb + " MB per window."
                : Lang.T("Not measured yet on this PC."), "muted");
            p.Row(lblProbe);
        }

        void UpdateProbeLabel()
        {
            if (lblProbe == null) return;
            if (CapProber.State == CapProber.Phase.Settle || CapProber.State == CapProber.Phase.Measure)
            {
                lblProbe.Text = "Cap probe: " + CapProber.Status;
                if (numMaxRam != null && numMaxRam.Value != Store.Settings.MaxRamMb)
                {
                    loadingPerf = true;
                    numMaxRam.Value = Math.Max(0, Math.Min(32768, Store.Settings.MaxRamMb));
                    loadingPerf = false;
                }
            }
            else if (Store.Settings.RamCapFloorMb > 0)
                lblProbe.Text = "Measured floor on this PC: " + Store.Settings.RamCapFloorMb + " MB per window.";
            else
                lblProbe.Text = Lang.T("Not measured yet on this PC.");
        }
    }
}

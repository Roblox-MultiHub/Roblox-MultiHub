using System;
using System.Collections.Generic;
using System.Threading;
using System.Windows.Forms;

namespace RobloxMultiHub
{
    // ONE clock for every animation in the app, shared by everything: page slides, card moves,
    // footer buttons, ripples and the nav-highlight evolve. A WinForms Timer (WM_TIMER) is
    // delivered at ~15.6ms and only when the message queue is idle, which makes animations judder;
    // this uses a threading timer at 8ms that posts to the UI thread instead. Jobs are timestamped,
    // so a late tick still draws the correct frame.
    public static class Anim
    {
        // one duration+ease for everything a click triggers: that is what makes it feel sequenced
        public const int Ms = 220;

        class Job
        {
            public object Tag;
            public int Start, Ms;
            public Action<float> Step;
            public Action Done;
        }

        static readonly List<Job> jobs = new List<Job>();
        static System.Threading.Timer clock;       // 8ms, off the UI thread
        static SynchronizationContext ui;

        /// <summary>Raised on the UI thread every tick (about 125 fps) - for controls that repaint
        /// themselves while animating (button ripples), instead of each owning a timer.</summary>
        public static event Action Frame;

        public static void Go(object tag, int ms, Action<float> step, Action done)
        {
            if (!Store.Settings.Animations) { step(1f); if (done != null) done(); return; }
            Cancel(tag);
            jobs.Add(new Job { Tag = tag, Start = Environment.TickCount, Ms = Math.Max(1, ms), Step = step, Done = done });
            EnsureClock();
            step(0f);
        }

        internal static void EnsureClock()
        {
            if (clock != null) return;
            ui = SynchronizationContext.Current ?? new SynchronizationContext();
            clock = new System.Threading.Timer(delegate(object s)
            {
                if (jobs.Count == 0 && !frameAlive()) return;     // nothing wants ticks - skip the post
                ui.Post(delegate(object s2) { Pump(); }, null);
            }, null, 0, 8);
        }

        static bool frameAlive() { return Frame != null && frameHasSubscribers; }
        internal static bool frameHasSubscribers;

        static void Pump()
        {
            for (int i = jobs.Count - 1; i >= 0; i--)
            {
                Job j = jobs[i];
                float t = Math.Min(1f, (Environment.TickCount - j.Start) / (float)j.Ms);
                j.Step(EaseOutCubic(t));
                if (t >= 1f)
                {
                    jobs.RemoveAt(i);
                    if (j.Done != null) j.Done();
                }
            }
            if (Frame != null) Frame();
        }

        public static bool Running(object tag)
        {
            foreach (Job j in jobs) if (Equals(j.Tag, tag)) return true;
            return false;
        }

        public static void Cancel(object tag)
        {
            for (int i = jobs.Count - 1; i >= 0; i--)
                if (Equals(jobs[i].Tag, tag)) jobs.RemoveAt(i);
        }

        // fast start, gentle landing - reads as "snappy but soft" at 150-300 ms durations
        public static float EaseOutCubic(float t) { float u = 1f - t; return 1f - u * u * u; }
    }
}

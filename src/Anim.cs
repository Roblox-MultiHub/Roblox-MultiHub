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
        static SynchronizationContext ui;
        static readonly System.Diagnostics.Stopwatch sw = new System.Diagnostics.Stopwatch();

        /// <summary>Set true while any control needs repaint ticks (button ripples). The loop only
        /// runs while this is true or jobs exist, so idle cost is zero.</summary>
        internal static volatile bool FrameDemand;
        static bool suspended;                    // window move/resize: no frames, or drag lags

        /// <summary>Raised on the UI thread every frame while animating - for controls that repaint
        /// themselves (button ripples), instead of each owning a timer.</summary>
        public static event Action Frame;

        public static void Go(object tag, int ms, Action<float> step, Action done)
        {
            if (!Store.Settings.Animations) { step(1f); if (done != null) done(); return; }
            Cancel(tag);
            jobs.Add(new Job { Tag = tag, Start = Environment.TickCount, Ms = Math.Max(1, ms), Step = step, Done = done });
            EnsureClock();
            step(0f);
        }

        // called around window move/resize: the 250fps loop posts into the move loop's queue and
        // makes the drag fight a stream of messages - suspend frames for the duration
        internal static void Suspend() { suspended = true; }
        internal static void Resume() { suspended = false; }

        internal static void EnsureClock()
        {
            if (ui == null) ui = SynchronizationContext.Current ?? new SynchronizationContext();
            StartLoop();
        }

        // Frame source: an Application.Idle loop. Idle fires every time the message queue empties,
        // so WM_PAINT and WM_TIMER always run BETWEEN frames (a self-posting loop starves them and
        // freezes settles + the app tick). We re-post one message per frame to keep Idle coming,
        // Stopwatch-spaced to ~250 fps. Timers on this machine bottom out at ~15.4ms regardless of
        // interval (measured); the empty queue spins at 90kHz+, so the loop drives the frames.
        static bool idleHooked;
        static void StartLoop()
        {
            if (idleHooked) return;
            idleHooked = true;
            sw.Reset();
            sw.Start();
            Application.Idle += delegate
            {
                if (suspended) return;                          // dragging the window: no frames
                if (jobs.Count == 0 && !FrameDemand) return;    // nothing animates: stop posting
                if (sw.ElapsedMilliseconds >= 4)
                {
                    Pump();
                    sw.Reset();
                    sw.Start();
                }
                ui.Post(delegate(object s) { }, null);          // queue one more spin
            };
        }

        static bool frameAlive() { return FrameDemand; }

        static void Pump()
        {
            // A frame is derived state, recomputed from timestamps on every tick - so a bad frame is
            // DROPPED, never propagated: one frame's exception must never take the app down, and the
            // next tick redraws the correct state by itself.
            try
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
            catch (Exception ex) { Log.Error("Anim frame dropped", ex); }
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

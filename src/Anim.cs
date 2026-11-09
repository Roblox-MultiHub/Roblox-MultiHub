using System;
using System.Collections.Generic;
using System.Windows.Forms;

namespace RobloxMultiHub
{
    // One shared ~60 fps timer for every animation in the app (pages sliding in, cards moving to
    // their new slots, footer buttons appearing). Each job gets an eased 0..1 value; re-Go with the
    // same tag cancels the old job, and the caller decides what "from current state" means.
    public static class Anim
    {
        class Job
        {
            public object Tag;
            public int Start, Ms;
            public Action<float> Step;
            public Action Done;
            public float Last;
        }

        static readonly List<Job> jobs = new List<Job>();
        static Timer tick;

        public static void Go(object tag, int ms, Action<float> step, Action done)
        {
            if (!Store.Settings.Animations) { step(1f); if (done != null) done(); return; }
            Cancel(tag);
            jobs.Add(new Job { Tag = tag, Start = Environment.TickCount, Ms = Math.Max(1, ms), Step = step, Done = done });
            if (tick == null)
            {
                tick = new Timer();
                tick.Interval = 15;
                tick.Tick += delegate { Pump(); };
            }
            if (!tick.Enabled) tick.Start();
            step(0f);
        }

        public static void Cancel(object tag)
        {
            for (int i = jobs.Count - 1; i >= 0; i--)
                if (Equals(jobs[i].Tag, tag)) jobs.RemoveAt(i);
        }

        static void Pump()
        {
            for (int i = jobs.Count - 1; i >= 0; i--)
            {
                Job j = jobs[i];
                float t = Math.Min(1f, (Environment.TickCount - j.Start) / (float)j.Ms);
                j.Last = EaseOutCubic(t);
                j.Step(j.Last);
                if (t >= 1f)
                {
                    jobs.RemoveAt(i);
                    if (j.Done != null) j.Done();
                }
            }
            if (jobs.Count == 0) tick.Stop();
        }

        // fast start, gentle landing - reads as "snappy but soft" at 150-300 ms durations
        public static float EaseOutCubic(float t) { float u = 1f - t; return 1f - u * u * u; }
    }
}

using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace RobloxMultiHub
{
    // Profile pictures: memory cache -> disk cache (3 days) -> Roblox thumbnail API.
    public static class Avatars
    {
        static readonly Dictionary<long, Image> mem = new Dictionary<long, Image>();
        static readonly HashSet<long> inflight = new HashSet<long>();
        static readonly Dictionary<long, DateTime> retryAt = new Dictionary<long, DateTime>();
        static readonly List<long> queue = new List<long>();
        static bool running;

        public static event Action Updated;

        static string Dir { get { return Path.Combine(Store.Dir, "avatars"); } }

        // Returns the picture if it is already available, otherwise null (and starts loading it).
        public static Image Get(long userId)
        {
            if (userId <= 0) return null;
            Image img;
            if (mem.TryGetValue(userId, out img)) return img;
            string f = Path.Combine(Dir, userId + ".png");
            try
            {
                if (File.Exists(f))
                {
                    using (FileStream fs = File.OpenRead(f))
                    using (Image raw = Image.FromStream(fs))
                    {
                        img = new Bitmap(raw);
                    }
                    mem[userId] = img;
                    if (File.GetLastWriteTime(f) < DateTime.Now.AddDays(-3)) Request(userId);
                    return img;
                }
            }
            catch (Exception) { }
            Request(userId);
            return null;
        }

        static void Request(long id)
        {
            if (inflight.Contains(id)) return;
            DateTime until;
            if (retryAt.TryGetValue(id, out until) && until > DateTime.Now) return;
            inflight.Add(id);
            queue.Add(id);
            if (!running) Pump();
        }

        static async void Pump()
        {
            running = true;
            try
            {
                while (queue.Count > 0)
                {
                    await Task.Delay(150);   // lets requests made at the same moment travel together
                    List<long> batch = queue.Take(50).ToList();
                    queue.RemoveRange(0, batch.Count);
                    Dictionary<long, string> urls = await Api.AvatarUrls(batch);
                    foreach (long id in batch)
                    {
                        bool ok = false;
                        string url;
                        if (urls.TryGetValue(id, out url))
                        {
                            byte[] data = await Api.Download(url);
                            if (data != null)
                            {
                                try
                                {
                                    using (MemoryStream ms = new MemoryStream(data))
                                    using (Image raw = Image.FromStream(ms))
                                    {
                                        mem[id] = new Bitmap(raw);
                                    }
                                    Directory.CreateDirectory(Dir);
                                    File.WriteAllBytes(Path.Combine(Dir, id + ".png"), data);
                                    ok = true;
                                }
                                catch (Exception) { }
                            }
                        }
                        inflight.Remove(id);
                        if (!ok) retryAt[id] = DateTime.Now.AddMinutes(2);
                    }
                    Action h = Updated;
                    if (h != null) h();
                }
            }
            catch (Exception ex) { Log.Error("Avatars", ex); }
            running = false;
        }
    }
}

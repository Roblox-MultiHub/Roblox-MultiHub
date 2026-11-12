using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;

namespace SecretDetector
{
    public class MatchResult
    {
        public double Score;
        public int X;
        public int Y;
        public string Template;
    }

    public class Template
    {
        public string Name;
        public int W;
        public int H;
        public byte[] Gray;
        public int[] OffX;
        public int[] OffY;
        public double[] TplDiff;
        public int Count;
        public double Mean;
        public double Var;

        public static Template Load(string path, string name)
        {
            using (Bitmap bmp = new Bitmap(path))
            {
                Template t = new Template();
                t.Name = name;
                t.W = bmp.Width;
                t.H = bmp.Height;
                int n = t.W * t.H;
                t.Gray = new byte[n];
                bool[] mask = new bool[n];
                Pixel.Read(bmp, t.Gray, mask);

                int count = 0;
                for (int i = 0; i < n; i++) if (mask[i]) count++;
                t.Count = count;
                t.OffX = new int[count];
                t.OffY = new int[count];
                t.TplDiff = new double[count];

                int idx = 0;
                double sum = 0;
                for (int y = 0; y < t.H; y++)
                    for (int x = 0; x < t.W; x++)
                    {
                        int i = y * t.W + x;
                        if (!mask[i]) continue;
                        t.OffX[idx] = x;
                        t.OffY[idx] = y;
                        sum += t.Gray[i];
                        idx++;
                    }

                t.Mean = sum / count;
                double varSum = 0;
                for (int k = 0; k < count; k++)
                {
                    double d = t.Gray[t.OffY[k] * t.W + t.OffX[k]] - t.Mean;
                    t.TplDiff[k] = d;
                    varSum += d * d;
                }
                t.Var = varSum;
                return t;
            }
        }
    }

    public static class Pixel
    {
        public static void Read(Bitmap bmp, byte[] gray, bool[] mask)
        {
            Bitmap src = bmp;
            Bitmap tmp = null;
            if (bmp.PixelFormat != PixelFormat.Format32bppArgb)
            {
                tmp = new Bitmap(bmp.Width, bmp.Height, PixelFormat.Format32bppArgb);
                using (Graphics g = Graphics.FromImage(tmp)) g.DrawImage(bmp, 0, 0, bmp.Width, bmp.Height);
                src = tmp;
            }

            int w = src.Width;
            int h = src.Height;
            BitmapData data = src.LockBits(new Rectangle(0, 0, w, h), ImageLockMode.ReadOnly, PixelFormat.Format32bppArgb);
            try
            {
                int stride = data.Stride;
                byte[] buf = new byte[stride * h];
                Marshal.Copy(data.Scan0, buf, 0, buf.Length);
                bool anyTransparent = false;
                for (int y = 0; y < h; y++)
                {
                    int row = y * stride;
                    int gr = y * w;
                    for (int x = 0; x < w; x++)
                    {
                        int i = row + x * 4;
                        byte b = buf[i];
                        byte g = buf[i + 1];
                        byte r = buf[i + 2];
                        byte a = buf[i + 3];
                        gray[gr + x] = (byte)((r * 299 + g * 587 + b * 114) / 1000);
                        bool opaque = a >= 128;
                        mask[gr + x] = opaque;
                        if (!opaque) anyTransparent = true;
                    }
                }
                if (!anyTransparent)
                    for (int i = 0; i < mask.Length; i++) mask[i] = true;
            }
            finally
            {
                src.UnlockBits(data);
                if (tmp != null) tmp.Dispose();
            }
        }

        public static byte[] ToGray(Bitmap bmp)
        {
            int n = bmp.Width * bmp.Height;
            byte[] gray = new byte[n];
            bool[] mask = new bool[n];
            Read(bmp, gray, mask);
            return gray;
        }

        public static bool IsUniform(byte[] gray)
        {
            if (gray.Length < 2) return true;
            byte first = gray[0];
            for (int i = 1; i < gray.Length; i++)
                if (gray[i] != first) return false;
            return true;
        }
    }

    public static class Matcher
    {
        public static MatchResult Find(byte[] src, int sw, int sh, Template t)
        {
            int maxX = sw - t.W;
            int maxY = sh - t.H;
            if (maxX < 0 || maxY < 0) return null;

            int step = 3;
            List<MatchResult> best = new List<MatchResult>();

            for (int y = 0; y <= maxY; y += step)
                for (int x = 0; x <= maxX; x += step)
                {
                    MatchResult m = new MatchResult();
                    m.Score = Zncc(src, sw, x, y, t);
                    m.X = x;
                    m.Y = y;
                    m.Template = t.Name;
                    Consider(best, m, 6);
                }

            MatchResult result = null;
            foreach (MatchResult c in best)
            {
                int x0 = Math.Max(0, c.X - step);
                int x1 = Math.Min(maxX, c.X + step);
                int y0 = Math.Max(0, c.Y - step);
                int y1 = Math.Min(maxY, c.Y + step);
                for (int y = y0; y <= y1; y++)
                    for (int x = x0; x <= x1; x++)
                    {
                        double s = Zncc(src, sw, x, y, t);
                        if (result == null || s > result.Score)
                        {
                            result = new MatchResult();
                            result.Score = s;
                            result.X = x;
                            result.Y = y;
                            result.Template = t.Name;
                        }
                    }
            }
            return result;
        }

        static void Consider(List<MatchResult> list, MatchResult m, int keep)
        {
            int at = list.Count;
            for (int i = 0; i < list.Count; i++)
                if (m.Score > list[i].Score) { at = i; break; }
            list.Insert(at, m);
            if (list.Count > keep) list.RemoveAt(list.Count - 1);
        }

        static double Zncc(byte[] src, int sw, int x0, int y0, Template t)
        {
            int c = t.Count;
            double sumS = 0;
            for (int k = 0; k < c; k++)
                sumS += src[(y0 + t.OffY[k]) * sw + (x0 + t.OffX[k])];
            double meanS = sumS / c;

            double num = 0;
            double denS = 0;
            for (int k = 0; k < c; k++)
            {
                double ds = src[(y0 + t.OffY[k]) * sw + (x0 + t.OffX[k])] - meanS;
                num += ds * t.TplDiff[k];
                denS += ds * ds;
            }

            double den = Math.Sqrt(denS * t.Var);
            if (den < 1e-9) return 0;
            return num / den;
        }
    }
}

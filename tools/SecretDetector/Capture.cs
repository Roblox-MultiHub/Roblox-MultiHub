using System;
using System.Drawing;
using System.Drawing.Imaging;

namespace SecretDetector
{
    public static class WindowCapture
    {
        public static Bitmap CaptureWindow(IntPtr hwnd)
        {
            Native.RECT r;
            if (!Native.GetWindowRect(hwnd, out r)) return null;
            int w = r.Width;
            int h = r.Height;
            if (w <= 0 || h <= 0) return null;

            Bitmap bmp = new Bitmap(w, h, PixelFormat.Format32bppArgb);
            using (new DpiScope())
            using (Graphics g = Graphics.FromImage(bmp))
            {
                IntPtr hdc = g.GetHdc();
                try { Native.PrintWindow(hwnd, hdc, Native.PW_RENDERFULLCONTENT); }
                finally { g.ReleaseHdc(hdc); }
            }
            return bmp;
        }
    }
}

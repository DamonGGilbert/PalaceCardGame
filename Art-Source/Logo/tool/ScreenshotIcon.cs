using System;
using System.IO;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;

// Turns a clean in-game screenshot (1920x1080) into the Shedhead icons.
// args: <screenshot> <output dir> <cropX> <cropY> <cropSize>
static class P
{
    [STAThread]
    static void Main(string[] args)
    {
        string src = args[0], outDir = args[1];
        int cx = int.Parse(args[2]), cy = int.Parse(args[3]), cs = int.Parse(args[4]);
        Directory.CreateDirectory(outDir);

        var frame = new FormatConvertedBitmap(BitmapFrame.Create(new Uri(Path.GetFullPath(src)), BitmapCreateOptions.None, BitmapCacheOption.OnLoad), PixelFormats.Bgra32, null, 0);
        int W = frame.PixelWidth, H = frame.PixelHeight;
        var px = new byte[W * H * 4];
        frame.CopyPixels(px, W * 4, 0);

        // Paint over the empty info plate beside the pile with the felt just below it (feathered edges).
        Patch(px, W, 1158, 482, 150, 64, 0, 74, 10);
        var shot = BitmapSource.Create(W, H, 96, 96, PixelFormats.Bgra32, null, px, W * 4);

        // Square icon: the chosen crop, full bleed.
        var icon = Render(1024, 1024, dc => dc.DrawImage(new CroppedBitmap(shot, new Int32Rect(cx, cy, cs, cs)), new Rect(0, 0, 1024, 1024)), true);
        Save(icon, Path.Combine(outDir, "shedhead_icon.png"));
        Save(Render(512, 512, dc => dc.DrawImage(icon, new Rect(0, 0, 512, 512)), true), Path.Combine(outDir, "play-icon-512.png"));

        // Android adaptive: the launcher shows only the middle two thirds of the layer, so the background layer
        // is a wider crop around the same centre (as much as the screenshot allows), and the foreground is empty.
        int big = Math.Min(H, (int)(cs * 1.5));
        int bx = Math.Max(0, Math.Min(W - big, cx + cs / 2 - big / 2)), by = Math.Max(0, Math.Min(H - big, cy + cs / 2 - big / 2));
        var bg = Render(1024, 1024, dc => dc.DrawImage(new CroppedBitmap(shot, new Int32Rect(bx, by, big, big)), new Rect(0, 0, 1024, 1024)), true);
        Save(bg, Path.Combine(outDir, "shedhead_icon_android_bg.png"));
        Save(Render(1024, 1024, dc => { }, false), Path.Combine(outDir, "shedhead_icon_android_fg.png"));

        // In-game logo: the icon as a rounded tile with a thin gold rim and see-through corners.
        Save(Render(1024, 1024, dc =>
        {
            dc.PushClip(new RectangleGeometry(new Rect(24, 24, 976, 976), 190, 190));
            dc.DrawImage(icon, new Rect(0, 0, 1024, 1024));
            dc.Pop();
            dc.DrawGeometry(null, new Pen(new SolidColorBrush(Color.FromRgb(0xE8, 0xC0, 0x5A)), 10), new RectangleGeometry(new Rect(29, 29, 966, 966), 186, 186));
        }, false), Path.Combine(outDir, "shedhead_logo.png"));

        // Previews.
        Save(Render(512, 512, dc =>
        {
            dc.PushClip(new RectangleGeometry(new Rect(0, 0, 512, 512), 512 * 0.2237, 512 * 0.2237));
            dc.DrawImage(icon, new Rect(0, 0, 512, 512));
            dc.Pop();
        }, true, Colors.White), Path.Combine(outDir, "preview_ios.png"));
        Save(Render(512, 512, dc =>
        {
            double layer = 512 * 108.0 / 72.0, lo = (512 - layer) / 2;
            dc.PushClip(new EllipseGeometry(new Rect(0, 0, 512, 512)));
            dc.DrawImage(bg, new Rect(lo, lo, layer, layer));
            dc.Pop();
        }, true, Colors.White), Path.Combine(outDir, "preview_android_circle.png"));
        Save(Render(420, 160, dc =>
        {
            double x = 20;
            foreach (int size in new[] { 144, 96, 64, 48 })
            {
                dc.PushClip(new RectangleGeometry(new Rect(x, 8, size, size), size * 0.22, size * 0.22));
                dc.DrawImage(icon, new Rect(x, 8, size, size));
                dc.Pop();
                x += size + 20;
            }
        }, true, Color.FromRgb(0x20, 0x20, 0x24)), Path.Combine(outDir, "preview_sizes.png"));
        Console.WriteLine("done");
    }

    /// <summary>Copies the w x h block found (dx, dy) away over (x, y), blending in over 'feather' pixels at the edges.</summary>
    static void Patch(byte[] px, int W, int x, int y, int w, int h, int dx, int dy, int feather)
    {
        for (int j = 0; j < h; j++)
            for (int i = 0; i < w; i++)
            {
                double edge = Math.Min(Math.Min(i, w - 1 - i), Math.Min(j, h - 1 - j));
                double a = Math.Min(1.0, (edge + 1) / feather);
                int d = ((y + j) * W + x + i) * 4, s = ((y + j + dy) * W + x + i + dx) * 4;
                for (int c = 0; c < 3; c++) px[d + c] = (byte)(px[s + c] * a + px[d + c] * (1 - a));
            }
    }

    static BitmapSource Render(int w, int h, Action<DrawingContext> draw, bool opaque, Color? background = null)
    {
        var visual = new DrawingVisual();
        RenderOptions.SetBitmapScalingMode(visual, BitmapScalingMode.HighQuality);
        using (var dc = visual.RenderOpen())
        {
            if (background.HasValue) dc.DrawRectangle(new SolidColorBrush(background.Value), null, new Rect(0, 0, w, h));
            draw(dc);
        }
        var rtb = new RenderTargetBitmap(w, h, 96, 96, PixelFormats.Pbgra32);
        rtb.Render(visual);
        return opaque ? new FormatConvertedBitmap(rtb, PixelFormats.Bgr24, null, 0) : (BitmapSource)rtb;
    }

    static void Save(BitmapSource bmp, string path)
    {
        var enc = new PngBitmapEncoder();
        enc.Frames.Add(BitmapFrame.Create(bmp));
        using (var f = File.Create(path)) enc.Save(f);
    }
}

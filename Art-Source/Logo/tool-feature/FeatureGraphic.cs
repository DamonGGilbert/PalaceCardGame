using System;
using System.Globalization;
using System.IO;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;

// Makes the Google Play feature graphic (1024 x 500) from a clean in-game screenshot (1920x1080):
// the table fills the banner, with the game's name on a dark fade down the left side.
// args: <screenshot> <Alegreya.ttf> <output png>
static class P
{
    [STAThread]
    static void Main(string[] args)
    {
        string src = args[0], fontFile = args[1], outPath = args[2];
        var frame = new FormatConvertedBitmap(BitmapFrame.Create(new Uri(Path.GetFullPath(src)), BitmapCreateOptions.None, BitmapCacheOption.OnLoad), PixelFormats.Bgra32, null, 0);
        int W = frame.PixelWidth, H = frame.PixelHeight;
        var px = new byte[W * H * 4];
        frame.CopyPixels(px, W * 4, 0);
        Patch(px, W, 1158, 482, 150, 64, 0, 74, 10); // the empty info plate beside the pile
        var shot = BitmapSource.Create(W, H, 96, 96, PixelFormats.Bgra32, null, px, W * 4);

        var dir = Path.GetDirectoryName(Path.GetFullPath(fontFile)).Replace('\\', '/') + "/";
        var family = new FontFamily(new Uri("file:///" + dir), "./#Alegreya");
        var bold = new Typeface(family, FontStyles.Normal, FontWeights.ExtraBold, FontStretches.Normal);
        var regular = new Typeface(family, FontStyles.Normal, FontWeights.SemiBold, FontStretches.Normal);

        const int OW = 1024, OH = 500;
        var visual = new DrawingVisual();
        RenderOptions.SetBitmapScalingMode(visual, BitmapScalingMode.HighQuality);
        using (var dc = visual.RenderOpen())
        {
            // The table, shifted right a little so the name has room on the left.
            var crop = new CroppedBitmap(shot, new Int32Rect(0, 40, 1680, 820));
            dc.DrawImage(crop, new Rect(0, 0, OW, OH));

            // Dark fade down the left for the name.
            var fade = new LinearGradientBrush { StartPoint = new Point(0, 0), EndPoint = new Point(1, 0) };
            fade.GradientStops.Add(new GradientStop(Color.FromArgb(250, 10, 8, 6), 0.0));
            fade.GradientStops.Add(new GradientStop(Color.FromArgb(238, 10, 8, 6), 0.36));
            fade.GradientStops.Add(new GradientStop(Color.FromArgb(0, 10, 8, 6), 0.6));
            dc.DrawRectangle(fade, null, new Rect(0, 0, OW, OH));

            double cx = 222;
            Spaced(dc, "SHEDHEAD", bold, 66, 4, cx, 178, Color.FromRgb(0xF2, 0xC1, 0x4E), 7);
            // A thin gold rule with a diamond, then the subtitle.
            var gold = new SolidColorBrush(Color.FromRgb(0xE8, 0xC0, 0x5A));
            dc.DrawRectangle(gold, null, new Rect(cx - 150, 284, 125, 2));
            dc.DrawRectangle(gold, null, new Rect(cx + 25, 284, 125, 2));
            dc.PushTransform(new RotateTransform(45, cx, 285));
            dc.DrawRectangle(gold, null, new Rect(cx - 6, 279, 12, 12));
            dc.Pop();
            Spaced(dc, "THE CLASSIC CARD GAME", regular, 21, 3, cx, 310, Color.FromRgb(0xF3, 0xE6, 0xC8), 0);
        }
        var rtb = new RenderTargetBitmap(OW, OH, 96, 96, PixelFormats.Pbgra32);
        rtb.Render(visual);
        var enc = new PngBitmapEncoder();
        enc.Frames.Add(BitmapFrame.Create(new FormatConvertedBitmap(rtb, PixelFormats.Bgr24, null, 0)));
        using (var f = File.Create(outPath)) enc.Save(f);
        Console.WriteLine("done");
    }

    /// <summary>Draws text centred on cx with extra letter spacing, optionally with a dark outline.</summary>
    static void Spaced(DrawingContext dc, string s, Typeface tf, double size, double spacing, double cx, double top, Color fill, double outline)
    {
        var glyphs = new FormattedText[s.Length];
        double total = 0;
        for (int i = 0; i < s.Length; i++)
        {
            glyphs[i] = new FormattedText(s[i].ToString(), CultureInfo.InvariantCulture, FlowDirection.LeftToRight, tf, size, Brushes.White, 1.0);
            total += glyphs[i].WidthIncludingTrailingWhitespace + (i < s.Length - 1 ? spacing : 0);
        }
        double x = cx - total / 2;
        var geo = new GeometryGroup();
        for (int i = 0; i < s.Length; i++)
        {
            geo.Children.Add(glyphs[i].BuildGeometry(new Point(x, top)));
            x += glyphs[i].WidthIncludingTrailingWhitespace + spacing;
        }
        if (outline > 0)
        {
            dc.PushTransform(new TranslateTransform(0, 4));
            dc.DrawGeometry(new SolidColorBrush(Color.FromArgb(150, 0, 0, 0)), new Pen(new SolidColorBrush(Color.FromArgb(150, 0, 0, 0)), outline), geo); // soft drop
            dc.Pop();
            dc.DrawGeometry(null, new Pen(new SolidColorBrush(Color.FromRgb(0x1A, 0x10, 0x06)), outline) { LineJoin = PenLineJoin.Round }, geo);
        }
        dc.DrawGeometry(new SolidColorBrush(fill), null, geo);
    }

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
}

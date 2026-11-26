using System;
using System.IO;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;

// Draws the Shedhead icon as a little picture of the game, using the game's own card art:
// the draw deck and discard pile at the top, and a player's table below (three face-up cards
// sitting on three face-down ones), on green felt.
// args: <PalaceCardGame root> <output dir>
static class P
{
    static string root;
    static BitmapImage face, back;
    static readonly Typeface RankFont = new Typeface(new FontFamily("Cambria"), FontStyles.Normal, FontWeights.Bold, FontStretches.Normal);
    static readonly Typeface SuitFont = new Typeface(new FontFamily("Segoe UI Symbol"), FontStyles.Normal, FontWeights.Normal, FontStretches.Normal);
    static readonly Color Red = Color.FromRgb(0xC4, 0x1E, 0x2A), Black = Color.FromRgb(0x18, 0x18, 0x1C);

    [STAThread]
    static void Main(string[] args)
    {
        root = args[0];
        string outDir = args[1];
        Directory.CreateDirectory(outDir);
        string art = Path.Combine(root, "Assets/Palace/Art/Generated");
        face = Load(Path.Combine(art, "pub_card_face.png"));
        back = Load(Path.Combine(art, "pub_card_back.png"));

        // 1. Square icon, full bleed, no transparency (iOS, older Android, Play Store).
        var icon = Render(1024, 1024, dc => { Felt(dc, 1024); Cards(dc, 1.0); }, true);
        Save(icon, Path.Combine(outDir, "shedhead_icon.png"));
        Save(Render(512, 512, dc => dc.DrawImage(icon, new Rect(0, 0, 512, 512)), true), Path.Combine(outDir, "play-icon-512.png"));

        // 2. Android adaptive icon: felt background layer, cards foreground layer kept in the safe zone.
        var bg = Render(1024, 1024, dc => Felt(dc, 1024), true);
        Save(bg, Path.Combine(outDir, "shedhead_icon_android_bg.png"));
        var fg = Render(1024, 1024, dc =>
        {
            const double s = 0.6;
            dc.PushTransform(new TranslateTransform(512 * (1 - s), 512 * (1 - s)));
            dc.PushTransform(new ScaleTransform(s, s));
            Cards(dc, 1.0);
            dc.Pop(); dc.Pop();
        }, false);
        Save(fg, Path.Combine(outDir, "shedhead_icon_android_fg.png"));

        // 3. In-game logo: the same picture as a rounded tile with a thin gold rim and see-through corners.
        var logo = Render(1024, 1024, dc =>
        {
            var tile = new RectangleGeometry(new Rect(24, 24, 976, 976), 190, 190);
            dc.PushClip(tile);
            dc.DrawImage(icon, new Rect(0, 0, 1024, 1024));
            dc.Pop();
            dc.DrawGeometry(null, new Pen(new SolidColorBrush(Color.FromRgb(0xE8, 0xC0, 0x5A)), 10), new RectangleGeometry(new Rect(29, 29, 966, 966), 186, 186));
        }, false);
        Save(logo, Path.Combine(outDir, "shedhead_logo.png"));

        // 4. Previews: iOS rounded square, Android circle, and small sizes as they'd look on a phone.
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
            dc.DrawImage(fg, new Rect(lo, lo, layer, layer));
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

    // ------------------------------------------------------------------ felt

    static void Felt(DrawingContext dc, double size)
    {
        var grad = new RadialGradientBrush(Color.FromRgb(0x2A, 0x7D, 0x48), Color.FromRgb(0x0E, 0x3D, 0x24))
        {
            Center = new Point(0.5, 0.42), GradientOrigin = new Point(0.5, 0.42), RadiusX = 0.75, RadiusY = 0.75
        };
        dc.DrawRectangle(grad, null, new Rect(0, 0, size, size));
        // A fine speckle so it reads as cloth, not plastic.
        var rnd = new Random(7);
        var light = new SolidColorBrush(Color.FromArgb(14, 255, 255, 255));
        var dark = new SolidColorBrush(Color.FromArgb(18, 0, 0, 0));
        for (int i = 0; i < 9000; i++)
        {
            double x = rnd.NextDouble() * size, y = rnd.NextDouble() * size;
            dc.DrawRectangle(rnd.Next(2) == 0 ? light : dark, null, new Rect(x, y, 2, 2));
        }
    }

    // ------------------------------------------------------------------ cards

    const double CW = 214, CH = CW * 392.0 / 280.0; // card size, same shape as the game's cards

    static void Cards(DrawingContext dc, double k)
    {
        const double shiftY = 30;
        // Top: the draw deck (a few backs stacked) and the discard pile fanned out.
        for (int i = 3; i >= 0; i--) Back(dc, 262 - i * 5, 250 + shiftY - i * 6, 0);
        Face(dc, 610, 262 + shiftY, -15, "4", '♣');
        Face(dc, 676, 250 + shiftY, -2, "9", '♦');
        Face(dc, 748, 262 + shiftY, 12, "Q", '♠', "court_queen_spades");

        // Bottom: a player's table, three face-down cards with three face-up cards sitting on them.
        double[] xs = { 272, 512, 752 };
        double downY = 724 + shiftY, upY = 636 + shiftY;
        foreach (double x in xs) Back(dc, x, downY, 0);
        Face(dc, xs[0], upY, 0, "10", '♦');
        Face(dc, xs[1], upY, 0, "K", '♥', "court_king_hearts");
        Face(dc, xs[2], upY, 0, "A", '♠');
    }

    static void Shadow(DrawingContext dc, Rect r)
    {
        for (int i = 0; i < 6; i++)
        {
            var rr = new Rect(r.X + 5 - i * 2, r.Y + 9 - i * 2, r.Width + i * 4, r.Height + i * 4);
            dc.DrawRoundedRectangle(new SolidColorBrush(Color.FromArgb(22, 0, 0, 0)), null, rr, 18 + i * 2, 18 + i * 2);
        }
    }

    static void Back(DrawingContext dc, double cx, double cy, double angle)
    {
        var r = new Rect(cx - CW / 2, cy - CH / 2, CW, CH);
        dc.PushTransform(new RotateTransform(angle, cx, cy));
        Shadow(dc, r);
        dc.DrawImage(back, r);
        dc.Pop();
    }

    static void Face(DrawingContext dc, double cx, double cy, double angle, string rank, char suit, string court = null)
    {
        var r = new Rect(cx - CW / 2, cy - CH / 2, CW, CH);
        Color ink = suit == '♥' || suit == '♦' ? Red : Black;
        var brush = new SolidColorBrush(ink);
        dc.PushTransform(new RotateTransform(angle, cx, cy));
        Shadow(dc, r);
        dc.DrawImage(face, r);

        // Corner index: rank with the suit under it, top left and (upside down) bottom right.
        Corner(dc, r, rank, suit, brush);
        dc.PushTransform(new RotateTransform(180, cx, cy));
        Corner(dc, r, rank, suit, brush);
        dc.Pop();

        if (court != null)
        {
            var pic = Load(Path.Combine(root, "Assets/Palace/Resources/Courts/" + court + ".png"));
            double pw = CW * 0.56, ph = pw * pic.PixelHeight / pic.PixelWidth;
            var pr = new Rect(cx - pw / 2, cy - ph / 2, pw, ph);
            dc.DrawRectangle(null, new Pen(new SolidColorBrush(Color.FromArgb(90, ink.R, ink.G, ink.B)), 2), pr);
            dc.DrawImage(pic, pr);
        }
        else
        {
            var big = Text(suit.ToString(), SuitFont, CH * 0.46, brush);
            dc.DrawText(big, new Point(cx - big.Width / 2, cy - big.Height / 2 + CH * 0.02));
        }
        dc.Pop();
    }

    static void Corner(DrawingContext dc, Rect r, string rank, char suit, Brush brush)
    {
        double colX = r.X + CW * 0.13;
        var rt = Text(rank, RankFont, CH * (rank.Length > 1 ? 0.15 : 0.17), brush);
        if (rank.Length > 1) rt.SetFontStretch(FontStretches.Condensed);
        dc.DrawText(rt, new Point(colX - rt.Width / 2, r.Y + CH * 0.035));
        var st = Text(suit.ToString(), SuitFont, CH * 0.13, brush);
        dc.DrawText(st, new Point(colX - st.Width / 2, r.Y + CH * 0.2));
    }

    static FormattedText Text(string s, Typeface tf, double size, Brush brush) =>
        new FormattedText(s, System.Globalization.CultureInfo.InvariantCulture, FlowDirection.LeftToRight, tf, size, brush, 1.0);

    // ------------------------------------------------------------------ helpers

    static BitmapImage Load(string path)
    {
        var b = new BitmapImage();
        b.BeginInit();
        b.CacheOption = BitmapCacheOption.OnLoad;
        b.UriSource = new Uri(Path.GetFullPath(path));
        b.EndInit();
        b.Freeze();
        return b;
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
        Console.WriteLine("wrote " + path);
    }
}

using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.IO;

namespace CodexQuotaTray.App;

internal static class TrayGlyph
{
    public static Icon Create(string text, double? remaining)
    {
        // Native-size frames avoid shrinking one 32px glyph into a blurry 16px icon.
        int[] sizes = [16, 20, 24, 32, 40, 48, 64];
        var frames = new byte[sizes.Length][];
        for (int i = 0; i < sizes.Length; i++)
        {
            using var bitmap = Render(sizes[i], text, remaining);
            using var png = new MemoryStream();
            bitmap.Save(png, ImageFormat.Png);
            frames[i] = png.ToArray();
        }
        using var stream = new MemoryStream();
        using var writer = new BinaryWriter(stream, System.Text.Encoding.UTF8, leaveOpen: true);
        writer.Write((ushort)0); writer.Write((ushort)1); writer.Write((ushort)sizes.Length);
        int offset = 6 + sizes.Length * 16;
        for (int i = 0; i < sizes.Length; i++)
        {
            writer.Write((byte)sizes[i]); writer.Write((byte)sizes[i]);
            writer.Write((byte)0); writer.Write((byte)0);
            writer.Write((ushort)1); writer.Write((ushort)32);
            writer.Write(frames[i].Length); writer.Write(offset);
            offset += frames[i].Length;
        }
        foreach (var frame in frames) writer.Write(frame);
        writer.Flush(); stream.Position = 0;
        using var icon = new Icon(stream, System.Windows.Forms.SystemInformation.SmallIconSize);
        return (Icon)icon.Clone();
    }

    internal static Bitmap Render(int size, string text, double? remaining)
    {
        var bitmap = new Bitmap(size, size, PixelFormat.Format32bppArgb);
        using var g = Graphics.FromImage(bitmap);
        g.Clear(Color.FromArgb(19, 25, 32));
        float thickness = Math.Max(2, (float)Math.Round(size * 3 / 32f));
        float inset = thickness / 2;
        float far = size - inset;
        // Remaining fills counterclockwise, so the consumed (gray) portion grows clockwise.
        PointF[] corners = [new(size / 2f, inset), new(inset, inset), new(inset, far), new(far, far), new(far, inset), new(size / 2f, inset)];
        g.SmoothingMode = SmoothingMode.None;
        using var track = new Pen(Color.FromArgb(85, 96, 110), thickness) { LineJoin = LineJoin.Miter };
        g.DrawLines(track, corners);
        if (remaining is >= 0 and <= 100)
        {
            float length = 4 * (size - thickness) * (float)remaining.Value / 100;
            var color = remaining.Value > 50 ? Color.FromArgb(77, 233, 130)
                : remaining.Value >= 20 ? Color.FromArgb(255, 217, 70)
                : Color.FromArgb(255, 91, 91);
            using var progress = new Pen(color, thickness) { StartCap = LineCap.Flat, EndCap = LineCap.Flat };
            for (int i = 1; i < corners.Length && length > 0; i++)
            {
                var start = corners[i - 1]; var end = corners[i];
                float segment = Math.Abs(end.X - start.X) + Math.Abs(end.Y - start.Y);
                float fraction = Math.Min(1, length / segment);
                g.DrawLine(progress, start, new PointF(start.X + (end.X - start.X) * fraction, start.Y + (end.Y - start.Y) * fraction));
                length -= segment;
            }
        }
        // Fit actual glyph outlines, excluding font ascent/descent padding. Three digits
        // are condensed horizontally instead of becoming shorter (100 remains readable).
        using var path = new GraphicsPath();
        using var family = new FontFamily("Segoe UI");
        path.AddString(text, family, (int)FontStyle.Bold, 32, Point.Empty, StringFormat.GenericTypographic);
        var bounds = path.GetBounds();
        if (bounds.Width > 0 && bounds.Height > 0)
        {
            // Keep a visible dark gutter between the heavier border and every glyph.
            float margin = thickness + Math.Max(1.5f, size / 16f);
            var target = new RectangleF(margin, margin, size - 2 * margin, size - 2 * margin);
            float scaleY = target.Height / bounds.Height;
            float scaleX = Math.Min(scaleY, target.Width / bounds.Width);
            using var matrix = new Matrix(scaleX, 0, 0, scaleY,
                target.X + (target.Width - bounds.Width * scaleX) / 2 - bounds.X * scaleX,
                target.Y - bounds.Y * scaleY);
            path.Transform(matrix);
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.FillPath(Brushes.White, path);
        }
        return bitmap;
    }
}

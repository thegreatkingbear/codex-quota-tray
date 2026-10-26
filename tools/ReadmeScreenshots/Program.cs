using System;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using CodexQuotaTray.App;
using CodexQuotaTray.Core;

internal static class Program
{
    [STAThread]
    private static void Main(string[] args)
    {
        string output = args.Length == 1 ? args[0] : "docs/images";
        Directory.CreateDirectory(output);
        var app = new Application();
#pragma warning disable WPF0001
        app.ThemeMode = ThemeMode.Dark;
#pragma warning restore WPF0001
        DesktopTheme.ApplyPalette(app.Resources, true);
        var now = DateTimeOffset.UtcNow;
        var sample = new QuotaSnapshot(now, true, [new("codex", [
            new("primary", 25, 300, now.AddHours(5)),
            new("secondary", 65, 10080, now.AddDays(7))])]);
        var details = new StackPanel { Margin = new Thickness(24) };
        DetailsView.Populate(details, sample, QuotaState.Demo, ErrorCode.None, true, () => { }, () => { }, () => { }, true);
        Save(details, 400, Path.Combine(output, "details.png"));
        var settings = new SettingsView(5, true, false, true, @"C:\Example\Codex\codex.exe", new string('A', 64), sample);
        Save(settings.Root, 460, Path.Combine(output, "settings.png"));

        var tray = new StackPanel { Margin = new Thickness(24) };
        tray.Children.Add(DetailsView.Label("TRAY ICON  /  SAMPLE VALUES", 10, muted: true, mono: true));
        var icons = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 20, 0, 12) };
        foreach (int remaining in new[] { 75, 35, 9 })
        {
            using var bitmap = TrayGlyph.Render(32, remaining.ToString(), remaining);
            using var stream = new MemoryStream();
            bitmap.Save(stream, System.Drawing.Imaging.ImageFormat.Png); stream.Position = 0;
            var source = new BitmapImage(); source.BeginInit(); source.CacheOption = BitmapCacheOption.OnLoad; source.StreamSource = stream; source.EndInit(); source.Freeze();
            var icon = new Image { Source = source, Width = 64, Height = 64 };
            RenderOptions.SetBitmapScalingMode(icon, BitmapScalingMode.NearestNeighbor);
            var column = new StackPanel { Width = 112 };
            column.Children.Add(icon);
            var label = DetailsView.Label($"{remaining}% left", 12, mono: true); label.HorizontalAlignment = HorizontalAlignment.Center; label.Margin = new Thickness(0, 12, 0, 0);
            column.Children.Add(label); icons.Children.Add(column);
        }
        tray.Children.Add(icons);
        tray.Children.Add(DetailsView.Label("32 px icons enlarged 2× · Illustrative values", 11, muted: true));
        Save(tray, 400, Path.Combine(output, "tray-icons.png"));
        Console.WriteLine("Saved three sample-only README images.");
    }

    private static void Save(UIElement content, double width, string path)
    {
        var root = new Border { Width = width, Child = content, UseLayoutRounding = true };
        root.SetResourceReference(Border.BackgroundProperty, "Quota.Background");
        root.Measure(new Size(width, double.PositiveInfinity)); root.Arrange(new Rect(root.DesiredSize)); root.UpdateLayout();
        var bitmap = new RenderTargetBitmap((int)width * 2, (int)Math.Ceiling(root.ActualHeight) * 2, 192, 192, PixelFormats.Pbgra32);
        bitmap.Render(root);
        var encoder = new PngBitmapEncoder(); encoder.Frames.Add(BitmapFrame.Create(bitmap));
        using var file = File.Create(path); encoder.Save(file);
    }
}

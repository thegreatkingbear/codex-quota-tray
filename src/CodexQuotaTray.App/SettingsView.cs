using System.Linq;
using System.Collections.Generic;
using CodexQuotaTray.Core;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace CodexQuotaTray.App;

internal sealed class SettingsView
{
    public DockPanel Root { get; } = new() { Margin = new Thickness(24) };
    public ScrollViewer Scroll { get; }
    public CheckBox Startup { get; }
    public CheckBox Demo { get; }
    public ComboBox Interval { get; }
    public ComboBox TrayQuota { get; }
    public TextBox ExecutablePath { get; }
    public TextBox Hash { get; }
    public Button Choose { get; } = ActionButtons.Create("Change executable…", "\uE8B7");
    public Button Save { get; } = ActionButtons.Create("Save");
    public Button Cancel { get; } = ActionButtons.Create("Cancel");
    public Expander Advanced { get; }

    public SettingsView(int interval, bool demo, bool startup, bool startupReadable, string? path, string? hash, QuotaSnapshot? snapshot = null, string? trayQuotaKey = null)
    {
        var footer = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right, Margin = new Thickness(0, 16, 0, 0) };
        Save.SetResourceReference(Control.BackgroundProperty, "Quota.Surface");
        Save.SetResourceReference(Control.BorderBrushProperty, "Quota.Border");
        Save.MinWidth = 64;
        Cancel.Margin = new Thickness(0, 0, 8, 0); footer.Children.Add(Cancel); footer.Children.Add(Save);
        DockPanel.SetDock(footer, Dock.Bottom); Root.Children.Add(footer);
        var body = new StackPanel();
        Scroll = new ScrollViewer { Content = body, VerticalScrollBarVisibility = ScrollBarVisibility.Auto, HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled };
        Root.Children.Add(Scroll);
        body.Children.Add(DetailsView.Label("CODEX  /  PREFERENCES", 10, muted: true, mono: true));
        var title = DetailsView.Label("Settings", 25); title.FontWeight = FontWeights.SemiBold; title.Margin = new Thickness(0, 4, 0, 6); body.Children.Add(title);
        var intro = DetailsView.Label("Customize how Quota Tray works.", 12, muted: true); intro.Margin = new Thickness(0, 0, 0, 18); body.Children.Add(intro);

        var general = new StackPanel();
        general.Children.Add(Heading("General"));
        Startup = new CheckBox { Content = "Start with Windows", IsChecked = startup, IsEnabled = startupReadable, FontSize = 13, Margin = new Thickness(0, 0, 0, 6) };
        general.Children.Add(Startup);
        general.Children.Add(Help(startupReadable ? "Start in the tray without opening a window." : "Unable to read the Windows startup setting."));
        var intervalLabel = DetailsView.Label("Refresh interval", 13); intervalLabel.Margin = new Thickness(0, 18, 0, 8); general.Children.Add(intervalLabel);
        Interval = new ComboBox
        {
            ItemsSource = new[] { 2, 5, 10, 15, 30 }.Select(n => new IntervalOption(n, n == 5 ? "5 minutes · Default" : $"{n} minutes")).ToArray(),
            DisplayMemberPath = "Label", SelectedValuePath = "Minutes", SelectedValue = interval,
            HorizontalAlignment = HorizontalAlignment.Stretch
        };
        System.Windows.Automation.AutomationProperties.SetName(Interval, "Refresh interval");
        general.Children.Add(Interval);
        body.Children.Add(DetailsView.Card(general));

        var tray = new StackPanel(); tray.Children.Add(Heading("Tray quota"));
        var options = new List<TrayChoice> { new(null, "Auto · Shortest period (default)") };
        options.AddRange(TraySelection.Options(snapshot).Select(o => new TrayChoice(o.Key, o.Label)));
        bool missing = trayQuotaKey is not null && !options.Any(o => o.Key == trayQuotaKey);
        if (missing) options.Add(new(trayQuotaKey, "Saved quota · Currently unavailable"));
        TrayQuota = new ComboBox { ItemsSource = options, DisplayMemberPath = "Label", HorizontalAlignment = HorizontalAlignment.Stretch,
            SelectedItem = options.First(o => o.Key == trayQuotaKey) };
        System.Windows.Automation.AutomationProperties.SetName(TrayQuota, "Quota to display in the tray");
        tray.Children.Add(TrayQuota);
        tray.Children.Add(Help("Auto selects the shortest period: 5 hours before 7 days.\nIf your selected quota is unavailable, Auto is used temporarily."));
        if (snapshot is null) tray.Children.Add(Help("Refresh usage, then reopen Settings to select a specific quota."));
        body.Children.Add(DetailsView.Card(tray));

        var advancedBody = new StackPanel { Margin = new Thickness(0, 14, 0, 0) };
        Demo = new CheckBox { Content = "Demo mode", IsChecked = demo, FontSize = 13, Margin = new Thickness(0, 0, 0, 6) };
        advancedBody.Children.Add(Demo);
        advancedBody.Children.Add(Help("Show sample data instead of your account usage."));
        var pathLabel = DetailsView.Label("Codex executable", 12, muted: true); pathLabel.Margin = new Thickness(0, 18, 0, 6); advancedBody.Children.Add(pathLabel);
        ExecutablePath = ReadOnly(path ?? "No verified executable"); advancedBody.Children.Add(ExecutablePath);
        var hashLabel = DetailsView.Label("SHA-256", 10, muted: true, mono: true); hashLabel.Margin = new Thickness(0, 12, 0, 6); advancedBody.Children.Add(hashLabel);
        Hash = ReadOnly(hash ?? "—"); advancedBody.Children.Add(Hash);
        Choose.HorizontalAlignment = HorizontalAlignment.Left; Choose.Margin = new Thickness(0, 12, 0, 12); advancedBody.Children.Add(Choose);
        advancedBody.Children.Add(Help("Choose an executable from an official Codex installation. This app never asks for or stores sign-in credentials."));
        var note = Help("If Windows blocks startup, enable it in Startup apps. After moving the app folder, turn startup off and save, then turn it on and save again to update the path."); note.Margin = new Thickness(0, 12, 0, 0); advancedBody.Children.Add(note);
        Advanced = new Expander { Header = "Advanced", Content = advancedBody, IsExpanded = false, HorizontalContentAlignment = HorizontalAlignment.Stretch, FontSize = 13 };
        body.Children.Add(DetailsView.Card(Advanced, new Thickness(0)));
    }
    private sealed record IntervalOption(int Minutes, string Label);
    private sealed record TrayChoice(string? Key, string Label);
    public string? SelectedTrayQuotaKey => (TrayQuota.SelectedItem as TrayChoice)?.Key;
    private static TextBlock Heading(string text)
    {
        var label = DetailsView.Label(text, 11, muted: true); label.FontWeight = FontWeights.SemiBold; label.Margin = new Thickness(0, 0, 0, 12); return label;
    }
    private static TextBlock Help(string text)
    {
        var label = DetailsView.Label(text, 11, muted: true); label.Margin = new Thickness(0, 4, 0, 0); return label;
    }
    private static TextBox ReadOnly(string text) => new()
    {
        Text = text, IsReadOnly = true, TextWrapping = TextWrapping.Wrap, FontFamily = new FontFamily("Cascadia Mono, Consolas"), FontSize = 11,
        Padding = new Thickness(8), HorizontalAlignment = HorizontalAlignment.Stretch
    };
}



using System;
using System.Linq;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Media;
using CodexQuotaTray.Core;

namespace CodexQuotaTray.App;

internal static class DetailsView
{
    private static readonly FontFamily Mono = new("Cascadia Mono, Consolas");
    internal static string Status(QuotaState state) => state switch
    {
        QuotaState.Demo => "Sample data — not your account",
        QuotaState.Loading => "Refreshing…",
        QuotaState.Available => "Connected via local Codex",
        QuotaState.Stale => "Stale — data may be outdated",
        QuotaState.NotInstalled => "Codex CLI not found",
        QuotaState.AuthRequired => "Sign-in required",
        QuotaState.Unsupported => "Usage limits unavailable",
        QuotaState.Offline => "Unable to reach Codex usage",
        _ => "Locating Codex…"
    };
    internal static TextBlock Label(string text, double size = 13, bool muted = false, bool mono = false)
    {
        var label = new TextBlock { Text = text, FontSize = size, TextWrapping = TextWrapping.Wrap, FontFamily = mono ? Mono : new FontFamily("Segoe UI"), LineHeight = size * 1.45 };
        label.SetResourceReference(TextBlock.ForegroundProperty, muted ? "Quota.Muted" : "Quota.Text");
        return label;
    }
    internal static Border Card(UIElement child, Thickness? margin = null)
    {
        var card = new Border { Child = child, CornerRadius = new CornerRadius(10), BorderThickness = new Thickness(1), Padding = new Thickness(18), Margin = margin ?? new Thickness(0, 0, 0, 12) };
        card.SetResourceReference(Border.BackgroundProperty, "Quota.Surface");
        card.SetResourceReference(Border.BorderBrushProperty, "Quota.Border");
        return card;
    }
    private static FrameworkElement Row(UIElement left, UIElement right)
    {
        var grid = new Grid();
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        grid.Children.Add(left); Grid.SetColumn(right, 1); grid.Children.Add(right);
        return grid;
    }
    internal static void Populate(Panel content, QuotaSnapshot? snapshot, QuotaState state, ErrorCode error, bool demo, Action refresh, Action settings, Action exit, bool canRefresh)
    {
        content.Children.Clear();
        var heading = new StackPanel { Margin = new Thickness(0, 0, 0, 18) };
        var eyebrow = Label("CODEX  /  QUOTA MONITOR", 10, muted: true, mono: true);
        heading.Children.Add(eyebrow);
        var title = Label("Usage limits", 25); title.FontWeight = FontWeights.SemiBold;
        title.Margin = new Thickness(0, 4, 0, 8); heading.Children.Add(title);
        var status = Label(Status(state), 12, muted: true);
        var dot = new Border { Width = 6, Height = 6, CornerRadius = new CornerRadius(3), Margin = new Thickness(0, 0, 8, 0), VerticalAlignment = VerticalAlignment.Center };
        dot.SetResourceReference(Border.BackgroundProperty, state == QuotaState.Available ? "Quota.Green" : state is QuotaState.Offline or QuotaState.AuthRequired ? "Quota.Red" : "Quota.Muted");
        // Grid keeps long status messages wrapping within the available width.
        var statusGrid = new Grid(); statusGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto }); statusGrid.ColumnDefinitions.Add(new ColumnDefinition());
        statusGrid.Children.Add(dot); Grid.SetColumn(status, 1); statusGrid.Children.Add(status); heading.Children.Add(statusGrid);
        content.Children.Add(heading);
        if (demo && state != QuotaState.Demo) content.Children.Add(Card(Label("Sample data — not your account", 12)));
        if (error != ErrorCode.None)
        {
            var notice = new StackPanel();
            var code = Label(error.ToString(), 11, mono: true); code.SetResourceReference(TextBlock.ForegroundProperty, "Quota.Red");
            notice.Children.Add(code);
            var remedy = Label(QuotaFormat.Remedy(error), 12); remedy.Margin = new Thickness(0, 6, 0, 0); notice.Children.Add(remedy);
            content.Children.Add(Card(notice));
        }
        if (snapshot is not null)
        {
            foreach (var bucket in snapshot.Buckets)
            {
                var panel = new StackPanel();
                var name = Label(bucket.Name, 12, muted: true, mono: true); name.Margin = new Thickness(0, 0, 0, 14); panel.Children.Add(name);
                if (bucket.Windows.Count == 0) panel.Children.Add(Label("No quota windows reported", 13, muted: true));
                for (int index = 0; index < bucket.Windows.Count; index++)
                {
                    var w = bucket.Windows[index];
                    if (index > 0)
                    {
                        var divider = new Border { Height = 1, Margin = new Thickness(0, 18, 0, 18) };
                        divider.SetResourceReference(Border.BackgroundProperty, "Quota.Border"); panel.Children.Add(divider);
                    }
                    var duration = Label(QuotaFormat.Duration(w.DurationMinutes), 15); duration.FontWeight = FontWeights.SemiBold;
                    panel.Children.Add(duration);
                    var metric = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 7, 0, 10) };
                    var amount = Label(w.RemainingPercent is { } r ? $"{r:0.#}%" : "Unknown", w.RemainingPercent is null ? 25 : 38, mono: true);
                    amount.FontWeight = FontWeights.SemiBold;
                    metric.Children.Add(amount);
                    metric.Children.Add(new TextBlock { Text = "remaining", FontSize = 12, VerticalAlignment = VerticalAlignment.Bottom, Margin = new Thickness(10, 0, 0, 9) }.Muted());
                    panel.Children.Add(metric);
                    if (w.RemainingPercent is { } remaining)
                    {
                        var bar = new ProgressBar { Value = remaining, Maximum = 100, Height = 6, Margin = new Thickness(0, 0, 0, 12) };
                        bar.SetResourceReference(Control.BackgroundProperty, "Quota.Track");
                        bar.SetResourceReference(Control.ForegroundProperty, state == QuotaState.Stale ? "Quota.Muted" : remaining > 50 ? "Quota.Green" : remaining >= 20 ? "Quota.Yellow" : "Quota.Red");
                        bar.BorderThickness = new Thickness(0);
                        AutomationProperties.SetName(bar, $"{bucket.Name}, {QuotaFormat.Duration(w.DurationMinutes)}, {remaining:0.#}% remaining");
                        panel.Children.Add(bar);
                    }
                    panel.Children.Add(Row(Label("Used", 12, muted: true), Label(w.UsedPercent is { } u ? $"{u:0.#}%" : "Unknown", 12, mono: true)));
                    var resetLabel = Label("Resets at · local time", 11, muted: true); resetLabel.Margin = new Thickness(0, 10, 0, 3); panel.Children.Add(resetLabel);
                    panel.Children.Add(Label(QuotaFormat.Reset(w.ResetsAt, DateTimeOffset.UtcNow, TimeZoneInfo.Local), 12, mono: true));
                }
                content.Children.Add(Card(panel));
            }
            var updated = Label($"Updated {snapshot.ObservedAt.ToLocalTime():yyyy-MM-dd HH:mm:ss zzz}", 10, muted: true, mono: true);
            updated.Margin = new Thickness(0, 0, 0, 15); content.Children.Add(updated);
        }
        var buttons = new StackPanel { Orientation = Orientation.Horizontal };
        var refreshButton = ActionButtons.Create("Refresh", "\uE72C");
        refreshButton.IsEnabled = canRefresh; refreshButton.Click += (_, _) => refresh(); buttons.Children.Add(refreshButton);
        var settingsButton = ActionButtons.Create("Settings", "\uE713");
        settingsButton.Margin = new Thickness(4, 0, 0, 0); settingsButton.Click += (_, _) => settings(); buttons.Children.Add(settingsButton);
        var more = ActionButtons.Create("More actions", "\uE712", showLabel: false);
        var menu = new ContextMenu();
        var exitItem = new MenuItem { Header = "Exit Codex Quota Tray" }; exitItem.Click += (_, _) => exit(); menu.Items.Add(exitItem);
        more.Click += (_, _) => { menu.PlacementTarget = more; menu.Placement = System.Windows.Controls.Primitives.PlacementMode.Bottom; menu.IsOpen = true; };
        var actions = Row(buttons, more); actions.Margin = new Thickness(0, 2, 0, 12);
        content.Children.Add(actions);
        content.Children.Add(Label("Independent app · Not endorsed by OpenAI", 10, muted: true));
    }
    private static TextBlock Muted(this TextBlock label)
    {
        label.SetResourceReference(TextBlock.ForegroundProperty, "Quota.Muted"); return label;
    }
}


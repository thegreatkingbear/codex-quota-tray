using System;
using System.IO;
using System.Linq;
using System.Security.Principal;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using CodexQuotaTray.Core;
using CodexQuotaTray.Codex;
using CodexQuotaTray.Demo;
using Forms = System.Windows.Forms;

namespace CodexQuotaTray.App;

public static class Program
{
    [STAThread]
    public static void Main(string[] args)
    {
        using var mutex = new Mutex(true, @"Local\CodexQuotaTray-" + WindowsIdentity.GetCurrent().User!.Value, out var first);
        if (!first) return;
        var app = new Application { ShutdownMode = ShutdownMode.OnExplicitShutdown };
        using var theme = new DesktopTheme(app);
        TrayApplication? tray = null;
        app.Startup += (_, _) => tray = new TrayApplication(app, args.Contains("--demo"), args.Contains("--startup"));
        try { app.Run(); }
        finally { tray?.Dispose(); mutex.ReleaseMutex(); }
    }
}

internal sealed class MissingProvider : IQuotaProvider
{
    public Task<QuotaSnapshot> ReadAsync(CancellationToken cancellationToken) => throw new QuotaException(ErrorCode.CODEX_NOT_FOUND);
}

internal sealed class TrayApplication : IDisposable
{
    private readonly Application app;
    private readonly Forms.NotifyIcon tray;
    private readonly Window popup;
    private readonly StackPanel content = new() { Margin = new Thickness(24) };
    private readonly CancellationTokenSource lifetime = new();
    private readonly DispatcherTimer timer = new() { Interval = TimeSpan.FromSeconds(15) };
    private Settings settings;
    private VerifiedExecutable? executable;
    private QuotaService? service;
    private Task active = Task.CompletedTask;
    private DateTimeOffset nextPoll = DateTimeOffset.MinValue;
    private bool stopping;
    private Window? settingsWindow;
    private QuotaState renderedState;
    private System.Drawing.Icon? glyph;
    private string glyphKey = "";
    private Forms.Screen popupScreen = Forms.Screen.PrimaryScreen!;
    private readonly ScrollViewer detailsScroll;

    public TrayApplication(Application application, bool demo, bool startInTray)
    {
        app = application;
        settings = Settings.Load();
        if (demo) settings = settings with { Demo = true };
        detailsScroll = new ScrollViewer { Content = content, HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled, VerticalScrollBarVisibility = ScrollBarVisibility.Auto };
        popup = new Window
        {
            Title = "Codex Quota Tray", Width = 400, SizeToContent = SizeToContent.Height,
            ShowInTaskbar = true, Content = detailsScroll,
            FontFamily = new FontFamily("Segoe UI"), UseLayoutRounding = true, SnapsToDevicePixels = true,
            Topmost = false, ResizeMode = ResizeMode.NoResize
        };
        popup.SetResourceReference(Window.BackgroundProperty, "Quota.Background");
        popup.SetResourceReference(Window.ForegroundProperty, "Quota.Text");
        popup.SizeChanged += (_, _) => PositionPopup();
        popup.DpiChanged += (_, _) => FitPopup();
        popup.Closing += (_, e) => { if (!stopping) { e.Cancel = true; popup.Hide(); } };
        popup.KeyDown += (_, e) => { if (e.Key == Key.Escape) popup.Hide(); };
        var menu = new Forms.ContextMenuStrip();
        menu.Items.Add("Refresh now", null, (_, _) => QueueRefresh(true));
        menu.Items.Add("Show details", null, (_, _) => Show());
        menu.Items.Add("Settings", null, (_, _) => OpenSettings());
        menu.Items.Add("Exit", null, async (_, _) => await Exit());
        tray = new Forms.NotifyIcon { Icon = System.Drawing.SystemIcons.Information, Text = "Codex Quota Tray — Disconnected", Visible = true, ContextMenuStrip = menu };
        tray.MouseClick += (_, e) => { if (e.Button == Forms.MouseButtons.Left) Show(); };
        SynchronizationContext.SetSynchronizationContext(new DispatcherSynchronizationContext(app.Dispatcher));
        timer.Tick += (_, _) =>
        {
            if (DateTimeOffset.UtcNow >= nextPoll) QueueRefresh(false);
            if (service is not null && service.DisplayState(TimeSpan.FromMinutes(settings.IntervalMinutes * 2 + 1)) != renderedState) Render();
        };
        app.Dispatcher.InvokeAsync(async () =>
        {
            Render();
            if (!startInTray) Show();
            executable = settings.ExecutablePath is not null && settings.ExecutableHash is not null
                ? new(settings.ExecutablePath, settings.ExecutableHash) : await Task.Run(ExecutableTrust.Discover);
            if (stopping) return;
            SetProvider(); QueueRefresh(false); timer.Start();
        });
    }
    private void SetProvider()
    {
        IQuotaProvider provider = settings.Demo ? new FixtureProvider() : executable is null
            ? new MissingProvider() : new AppServerProvider(new CodexProcessFactory(executable), executable);
        service = new(provider);
    }
    private void QueueRefresh(bool manual)
    {
        if (stopping || service is null || !active.IsCompleted) return;
        active = Refresh(manual);
    }
    private async Task Refresh(bool manual)
    {
        try
        {
            var pending = service!.RefreshAsync(manual, lifetime.Token);
            Render();
            if (await pending) nextPoll = DateTimeOffset.UtcNow + service.NextDelay(settings.IntervalMinutes);
            Render();
        }
        catch (OperationCanceledException) { }
    }
    private void Show()
    {
        popupScreen = Forms.Screen.FromPoint(Forms.Cursor.Position);
        Render();
        FitPopup();
        popup.Show(); popup.Activate();
        FitPopup();
    }
    private void FitPopup()
    {
        var dpi = VisualTreeHelper.GetDpi(popup);
        var area = popupScreen.WorkingArea;
        double availableWidth = Math.Max(200, area.Width / dpi.DpiScaleX - 24);
        double naturalWidth = 400;
        if (service?.Snapshot is { } snapshot)
        {
            foreach (var bucket in snapshot.Buckets)
            {
                var measure = DetailsView.Label(bucket.Name, 12, mono: true);
                measure.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
                naturalWidth = Math.Max(naturalWidth, measure.DesiredSize.Width + 88);
            }
        }
        popup.Width = Math.Min(availableWidth, Math.Min(480, naturalWidth));
        popup.MaxHeight = Math.Max(150, area.Height / dpi.DpiScaleY - 24);
        detailsScroll.MaxHeight = Math.Max(100, popup.MaxHeight - 48);
        PositionPopup();
    }
    private void PositionPopup()
    {
        if (!popup.IsVisible) return;
        var area = popupScreen.WorkingArea;
        var dpi = VisualTreeHelper.GetDpi(popup);
        popup.Left = Math.Max(area.Left / dpi.DpiScaleX + 12, area.Right / dpi.DpiScaleX - popup.ActualWidth - 12);
        popup.Top = Math.Max(area.Top / dpi.DpiScaleY + 12, area.Bottom / dpi.DpiScaleY - popup.ActualHeight - 12);
    }
    private void Render()
    {
        var state = service?.DisplayState(TimeSpan.FromMinutes(settings.IntervalMinutes * 2 + 1)) ?? QuotaState.Disconnected;
        renderedState = state;
        var snapshot = service?.Snapshot;
        var status = DetailsView.Status(state);
        DetailsView.Populate(content, snapshot, state, service?.Error ?? ErrorCode.None, settings.Demo,
            () => QueueRefresh(true), OpenSettings, async () => await Exit(), service is not null && !service.IsBusy);
        FitPopup();
        var chosen = TraySelection.Select(snapshot, settings.TrayQuotaKey);
        bool fallback = settings.TrayQuotaKey is not null && chosen?.Key != settings.TrayQuotaKey;
        string summary = chosen is null ? status : $"{status} · {(fallback ? "Auto fallback · " : "")}{chosen.Label}: {(chosen.Window.RemainingPercent is { } p ? $"{p:0.#}% left" : "Unknown")}";
        tray.Text = summary.Length > 127 ? summary[..124] + "…" : summary;
        var remaining = chosen?.Window.RemainingPercent;
        string mark = state == QuotaState.Demo ? "D" : state == QuotaState.Available && remaining.HasValue
            ? Math.Floor(remaining.Value).ToString(System.Globalization.CultureInfo.InvariantCulture)
            : state == QuotaState.Loading ? "…" : "?";
        double? borderRemaining = state == QuotaState.Available ? remaining : null;
        string key = mark + borderRemaining?.ToString("R", System.Globalization.CultureInfo.InvariantCulture);
        if (key != glyphKey)
        {
            var next = TrayGlyph.Create(mark, borderRemaining);
            tray.Icon = next; glyph?.Dispose(); glyph = next; glyphKey = key;
        }
    }
    private void OpenSettings()
    {
        if (service is null) return; // Discovery must finish before a saved selection can be changed.
        if (settingsWindow is not null) { settingsWindow.Activate(); return; }
        var registration = new StartupRegistration();
        bool startupWasEnabled = false;
        bool startupReadable = true;
        try { startupWasEnabled = registration.IsRegistered(); }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException or System.Security.SecurityException) { startupReadable = false; }
        var selected = executable;
        var view = new SettingsView(settings.IntervalMinutes, settings.Demo, startupWasEnabled, startupReadable, selected?.Path, selected?.Sha256, service.Snapshot, settings.TrayQuotaKey);
        var screen = Forms.Screen.FromPoint(Forms.Cursor.Position);
        var dialog = new Window
        {
            Title = "Quota Tray Settings", Width = 460, SizeToContent = SizeToContent.Height, ResizeMode = ResizeMode.NoResize,
            Content = view.Root, Owner = popup.IsLoaded ? popup : null, UseLayoutRounding = true,
            WindowStartupLocation = popup.IsVisible ? WindowStartupLocation.CenterOwner : WindowStartupLocation.CenterScreen
        };
        void FitSettings()
        {
            var dpi = VisualTreeHelper.GetDpi(dialog);
            var area = screen.WorkingArea;
            dialog.Width = Math.Min(460, Math.Max(220, area.Width / dpi.DpiScaleX - 24));
            dialog.MaxHeight = Math.Max(180, area.Height / dpi.DpiScaleY - 24);
            view.Scroll.MaxHeight = Math.Max(80, dialog.MaxHeight - 150);
            if (!dialog.IsVisible) return;
            dialog.Left = Math.Clamp(dialog.Left, area.Left / dpi.DpiScaleX + 12, Math.Max(area.Left / dpi.DpiScaleX + 12, area.Right / dpi.DpiScaleX - dialog.ActualWidth - 12));
            dialog.Top = Math.Clamp(dialog.Top, area.Top / dpi.DpiScaleY + 12, Math.Max(area.Top / dpi.DpiScaleY + 12, area.Bottom / dpi.DpiScaleY - dialog.ActualHeight - 12));
        }
        FitSettings();
        dialog.SizeChanged += (_, _) => FitSettings();
        dialog.DpiChanged += (_, _) => FitSettings();
        dialog.SetResourceReference(Window.BackgroundProperty, "Quota.Background");
        dialog.SetResourceReference(Window.ForegroundProperty, "Quota.Text");
        settingsWindow = dialog;
        view.Cancel.Click += (_, _) => dialog.Close();
        dialog.KeyDown += (_, e) => { if (e.Key == Key.Escape) dialog.Close(); };
        view.Choose.Click += (_, _) =>
        {
            var picker = new Microsoft.Win32.OpenFileDialog { Filter = "Codex executable|codex.exe", CheckFileExists = true };
            if (picker.ShowDialog(dialog) != true) return;
            try
            {
                var candidate = ExecutableTrust.Fingerprint(picker.FileName);
                if (MessageBox.Show(dialog, $"Run this executable with your normal user permissions? Only select Codex from an official installation you trust.\n\n{candidate.Path}\n\nSHA-256: {candidate.Sha256}", "Trust selected Codex", MessageBoxButton.YesNo, MessageBoxImage.Warning) != MessageBoxResult.Yes) return;
                selected = candidate; view.ExecutablePath.Text = candidate.Path; view.Hash.Text = candidate.Sha256;
            }
            catch (Exception e) when (e is IOException or UnauthorizedAccessException) { MessageBox.Show(dialog, "Unable to verify the selected executable."); }
        };
        view.Save.Click += (_, _) =>
        {
            if (!active.IsCompleted) { MessageBox.Show(dialog, "Wait for the current refresh to finish."); return; }
            var updated = new Settings((int?)view.Interval.SelectedValue ?? 5, view.Demo.IsChecked == true, selected?.Path, selected?.Sha256, view.SelectedTrayQuotaKey);
            try
            {
                updated.Save();
                if (view.Startup.IsEnabled && view.Startup.IsChecked != startupWasEnabled)
                    registration.SetEnabled(view.Startup.IsChecked == true, Environment.ProcessPath);
            }
            catch (Exception e) when (e is IOException or UnauthorizedAccessException or System.Security.SecurityException or ArgumentException)
            {
                try { settings.Save(); } catch (Exception restoreError) when (restoreError is IOException or UnauthorizedAccessException) { }
                MessageBox.Show(dialog, "Unable to save settings or change startup. Check file/registry permissions. To enable startup, run CodexQuotaTray.App.exe from a permanent folder with a short path.");
                return;
            }
            bool providerChanged = settings.Demo != updated.Demo || executable != selected;
            settings = updated; executable = selected; dialog.Close();
            if (providerChanged) { SetProvider(); QueueRefresh(false); }
            else { nextPoll = DateTimeOffset.UtcNow.AddMinutes(settings.IntervalMinutes); Render(); }
        };
        dialog.Closed += (_, _) => settingsWindow = null;
        dialog.Show();
    }
    private async Task Exit()
    {
        if (stopping) return;
        stopping = true; timer.Stop(); lifetime.Cancel();
        await active;
        settingsWindow?.Close(); popup.Close(); app.Shutdown();
    }
    public void Dispose()
    {
        lifetime.Cancel(); timer.Stop();
        if (service?.IsBusy == true)
        {
            var deadline = DateTime.UtcNow.AddSeconds(6);
            while (service.IsBusy && DateTime.UtcNow < deadline) Thread.Sleep(20);
        }
        tray.Visible = false; tray.ContextMenuStrip?.Dispose(); tray.Dispose(); glyph?.Dispose(); lifetime.Dispose();
    }
}



using System;
using System.ComponentModel;
using System.IO;
using System.Windows;
using System.Windows.Media;
using Microsoft.Win32;

namespace CodexQuotaTray.App;

internal sealed class DesktopTheme : IDisposable
{
    private readonly Application app;
    public DesktopTheme(Application application)
    {
        app = application;
#pragma warning disable WPF0001 // Built-in Fluent theme, verified against the pinned .NET 10 SDK.
        app.ThemeMode = ThemeMode.System;
#pragma warning restore WPF0001
        Update();
        SystemEvents.UserPreferenceChanged += Changed;
        SystemParameters.StaticPropertyChanged += ParametersChanged;
    }
    private void Changed(object sender, UserPreferenceChangedEventArgs e) => ScheduleUpdate();
    private void ParametersChanged(object? sender, PropertyChangedEventArgs e) => ScheduleUpdate();
    private void ScheduleUpdate()
    {
        if (!app.Dispatcher.HasShutdownStarted) app.Dispatcher.InvokeAsync(Update);
    }
    private void Update()
    {
        bool dark = false;
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize");
            dark = key?.GetValue("AppsUseLightTheme") is int value && value == 0;
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException or System.Security.SecurityException) { }
        ApplyPalette(app.Resources, dark, SystemParameters.HighContrast);
    }
    internal static void ApplyPalette(ResourceDictionary resources, bool dark, bool highContrast = false)
    {
        void Put(string name, string light, string night, Brush contrast)
        {
            var brush = highContrast ? contrast : new SolidColorBrush((Color)ColorConverter.ConvertFromString(dark ? night : light));
            resources[name] = brush;
        }
        Put("Quota.Background", "#F5F6F8", "#17191D", SystemColors.WindowBrush);
        Put("Quota.Surface", "#FFFFFF", "#21242A", SystemColors.WindowBrush);
        Put("Quota.Border", "#DDE1E7", "#353A43", SystemColors.WindowTextBrush);
        Put("Quota.Text", "#202630", "#F0F2F6", SystemColors.WindowTextBrush);
        Put("Quota.Muted", "#596473", "#ADB5C2", SystemColors.WindowTextBrush);
        Put("Quota.Track", "#E7EBEF", "#373E48", SystemColors.WindowTextBrush);
        Put("Quota.Green", "#117844", "#64DC9B", SystemColors.HighlightBrush);
        Put("Quota.Yellow", "#916100", "#F5CD64", SystemColors.HighlightBrush);
        Put("Quota.Red", "#BD3535", "#FF8585", SystemColors.HighlightBrush);
    }
    public void Dispose()
    {
        SystemEvents.UserPreferenceChanged -= Changed;
        SystemParameters.StaticPropertyChanged -= ParametersChanged;
    }
}

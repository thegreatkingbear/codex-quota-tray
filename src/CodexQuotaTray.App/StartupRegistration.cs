using System;
using System.IO;
using System.Runtime.Versioning;
using Microsoft.Win32;

namespace CodexQuotaTray.App;

/// <summary>Only our named HKCU value is changed; never touches Windows StartupApproved overrides.</summary>
[SupportedOSPlatform("windows")]
internal sealed class StartupRegistration(string keyPath = @"Software\Microsoft\Windows\CurrentVersion\Run")
{
    internal const string ValueName = "CodexQuotaTray";
    public bool IsRegistered()
    {
        using var key = Registry.CurrentUser.OpenSubKey(keyPath, writable: false);
        return key?.GetValue(ValueName, null, RegistryValueOptions.DoNotExpandEnvironmentNames) is string value && !string.IsNullOrWhiteSpace(value);
    }

    public static string Command(string executablePath)
    {
        if (string.IsNullOrWhiteSpace(executablePath) || !Path.IsPathFullyQualified(executablePath)
            || executablePath.IndexOfAny(['"', '\r', '\n']) >= 0
            || !Path.GetExtension(executablePath).Equals(".exe", StringComparison.OrdinalIgnoreCase)
            || Path.GetFileName(executablePath).Equals("dotnet.exe", StringComparison.OrdinalIgnoreCase))
            throw new ArgumentException("Launch the installed app executable to enable startup.");
        var command = $"\"{Path.GetFullPath(executablePath)}\" --startup";
        if (command.Length > 260) throw new ArgumentException("Move the app to a shorter path before enabling startup.");
        return command;
    }

    public void SetEnabled(bool enabled, string? executablePath)
    {
        if (!enabled)
        {
            using var existing = Registry.CurrentUser.OpenSubKey(keyPath, writable: true);
            existing?.DeleteValue(ValueName, throwOnMissingValue: false);
            return;
        }
        var command = Command(executablePath ?? "");
        if (!File.Exists(executablePath)) throw new IOException("App executable not found.");
        using var key = Registry.CurrentUser.CreateSubKey(keyPath, writable: true);
        key.SetValue(ValueName, command, RegistryValueKind.String);
    }
}

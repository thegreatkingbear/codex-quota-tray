using System;
using System.IO;
using System.Text.Json;

namespace CodexQuotaTray.App;

internal sealed record Settings(int IntervalMinutes = 5, bool Demo = false, string? ExecutablePath = null, string? ExecutableHash = null, string? TrayQuotaKey = null)
{
    private static string Folder => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "CodexQuotaTray");
    public static Settings Load()
    {
        try
        {
            var path = Path.Combine(Folder, "settings.json");
            if (!File.Exists(path) || new FileInfo(path).Length > 8192) return new();
            var s = JsonSerializer.Deserialize<Settings>(File.ReadAllText(path)) ?? new();
            return s with { IntervalMinutes = Math.Clamp(s.IntervalMinutes, 2, 30) };
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException or JsonException) { return new(); }
    }
    public void Save()
    {
        Directory.CreateDirectory(Folder);
        var path = Path.Combine(Folder, "settings.json");
        File.WriteAllText(path + ".tmp", JsonSerializer.Serialize(this));
        File.Move(path + ".tmp", path, true);
    }
}

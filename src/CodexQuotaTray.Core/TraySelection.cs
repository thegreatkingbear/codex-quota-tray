using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace CodexQuotaTray.Core;

public sealed record TrayQuotaOption(string Key, string Label, QuotaWindow Window);

public static class TraySelection
{
    public static IReadOnlyList<TrayQuotaOption> Options(QuotaSnapshot? snapshot) => snapshot?.Buckets
        .SelectMany(b => b.Windows.Select(w => new TrayQuotaOption(
            Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(
                new { Bucket = b.Id ?? b.Name, w.Kind, w.DurationMinutes })))),
            $"{b.Name} · {QuotaFormat.Duration(w.DurationMinutes)} · {w.Kind}", w)))
        .OrderBy(o => o.Window.DurationMinutes is > 0 ? o.Window.DurationMinutes : int.MaxValue)
        .ThenBy(o => o.Key, StringComparer.Ordinal).ToArray() ?? [];

    // Duration, not the changing reset timestamp, determines automatic selection.
    // Preserve unknown percentages on the selected window instead of substituting another quota.
    public static TrayQuotaOption? Select(QuotaSnapshot? snapshot, string? key)
    {
        var options = Options(snapshot);
        return options.FirstOrDefault(o => o.Key == key) ?? options.FirstOrDefault();
    }
}

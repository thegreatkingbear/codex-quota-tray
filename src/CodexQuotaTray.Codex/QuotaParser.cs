using System.Globalization;
using System.Text.Json;
using CodexQuotaTray.Core;

namespace CodexQuotaTray.Codex;

public static class QuotaParser
{
    public static QuotaSnapshot Parse(string json, DateTimeOffset observedAt, bool demo = false)
    {
        if (json.Length > 262144) throw new QuotaException(ErrorCode.OUTPUT_LIMIT);
        try
        {
            using var doc = JsonDocument.Parse(json, new JsonDocumentOptions { MaxDepth = 32 });
            var root = doc.RootElement;
            if (root.ValueKind != JsonValueKind.Object) throw new QuotaException(ErrorCode.INVALID_QUOTA);
            if (root.TryGetProperty("result", out var result)) root = result;
            if (root.ValueKind != JsonValueKind.Object) throw new QuotaException(ErrorCode.INVALID_QUOTA);
            var buckets = new List<QuotaBucket>();
            if (root.TryGetProperty("rateLimitsByLimitId", out var map) && map.ValueKind == JsonValueKind.Object && map.EnumerateObject().Any())
            {
                foreach (var item in map.EnumerateObject().OrderBy(p => p.Name == "codex" ? 0 : 1))
                {
                    if (buckets.Count >= 64) throw new QuotaException(ErrorCode.OUTPUT_LIMIT);
                    buckets.Add(Bucket(item.Value, item.Name, item.Name));
                }
            }
            else if (root.TryGetProperty("rateLimits", out var single) && single.ValueKind == JsonValueKind.Object)
                buckets.Add(Bucket(single, "Codex", single.TryGetProperty("limitId", out var id) && id.ValueKind == JsonValueKind.String ? id.GetString()! : "codex"));
            else throw new QuotaException(ErrorCode.PROTOCOL_UNSUPPORTED);
            return new(observedAt, demo, buckets);
        }
        catch (JsonException) { throw new QuotaException(ErrorCode.INVALID_QUOTA); }
    }

    private static QuotaBucket Bucket(JsonElement data, string fallback, string id)
    {
        if (data.ValueKind != JsonValueKind.Object) throw new QuotaException(ErrorCode.INVALID_QUOTA);
        string name = fallback;
        if (data.TryGetProperty("limitName", out var n) && n.ValueKind == JsonValueKind.String && !string.IsNullOrWhiteSpace(n.GetString())) name = n.GetString()!;
        // Remove formatting controls (including bidi), not just newlines. Never retain account fields.
        name = new string(name.Where(c => !char.IsControl(c) && char.GetUnicodeCategory(c) != UnicodeCategory.Format).Take(64).ToArray());
        if (string.IsNullOrWhiteSpace(name)) name = "Quota";
        var windows = new List<QuotaWindow>();
        foreach (var key in new[] { "primary", "secondary" })
        {
            if (!data.TryGetProperty(key, out var w) || w.ValueKind == JsonValueKind.Null) continue;
            if (w.ValueKind != JsonValueKind.Object) throw new QuotaException(ErrorCode.INVALID_QUOTA);
            double? used = w.TryGetProperty("usedPercent", out var u) && u.ValueKind == JsonValueKind.Number && u.TryGetDouble(out var d) && double.IsFinite(d) && d is >= 0 and <= 100 ? d : null;
            int? duration = w.TryGetProperty("windowDurationMins", out var m) && m.ValueKind == JsonValueKind.Number && m.TryGetInt32(out var i) && i > 0 ? i : null;
            DateTimeOffset? reset = null;
            if (w.TryGetProperty("resetsAt", out var r) && r.ValueKind == JsonValueKind.Number && r.TryGetInt64(out var s) && s is >= -62135596800 and <= 253402300799)
                reset = DateTimeOffset.FromUnixTimeSeconds(s);
            windows.Add(new(key, used, duration, reset));
        }
        // Keep only a stable digest of the quota identifier, never account fields.
        var identity = Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(id)));
        return new(name, windows, identity);
    }
}

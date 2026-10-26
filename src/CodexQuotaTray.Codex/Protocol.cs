using System.Text;
using System.Text.Json;
using CodexQuotaTray.Core;

namespace CodexQuotaTray.Codex;

public static class MethodPolicy
{
    public static void Validate(string method, bool notification)
    {
        if (!(notification ? method == "initialized" : method is "initialize" or "account/rateLimits/read"))
            throw new QuotaException(ErrorCode.PROTOCOL_UNSUPPORTED);
    }
    public static string Encode(string method, int? id, object? parameters = null)
    {
        Validate(method, id is null);
        return id is null
            ? JsonSerializer.Serialize(new { method, @params = parameters })
            : JsonSerializer.Serialize(new { method, id, @params = parameters });
    }
}

/// <summary>Reads fixed chunks; ReadLineAsync alone would allocate unbounded lines.</summary>
public sealed class BoundedLines(TextReader reader, int maxLine = 262144, int maxTotal = 1048576)
{
    private readonly char[] buffer = new char[4096];
    private int offset, available, total;
    public async Task<string> ReadAsync(CancellationToken cancellationToken)
    {
        var line = new StringBuilder();
        while (true)
        {
            if (offset == available)
            {
                available = await reader.ReadAsync(buffer.AsMemory(), cancellationToken).ConfigureAwait(false);
                offset = 0;
                if (available == 0) throw new QuotaException(ErrorCode.PROCESS_EXITED);
                total += available;
                if (total > maxTotal) throw new QuotaException(ErrorCode.OUTPUT_LIMIT);
            }
            char c = buffer[offset++];
            if (c == '\n') return line.ToString().TrimEnd('\r');
            if (line.Length >= maxLine) throw new QuotaException(ErrorCode.OUTPUT_LIMIT);
            line.Append(c);
        }
    }
}

public sealed class ProtocolSession(TextReader output, TextWriter input)
{
    private readonly BoundedLines lines = new(output);
    private async Task Send(string method, int? id, object? parameters, CancellationToken token)
    {
        await input.WriteLineAsync(MethodPolicy.Encode(method, id, parameters).AsMemory(), token).ConfigureAwait(false);
        await input.FlushAsync(token).ConfigureAwait(false);
    }
    private async Task<string> Response(int id, CancellationToken token)
    {
        for (int count = 0; count < 256; count++)
        {
            var line = await lines.ReadAsync(token).ConfigureAwait(false);
            try
            {
                using var doc = JsonDocument.Parse(line, new JsonDocumentOptions { MaxDepth = 32 });
                var root = doc.RootElement;
                if (root.ValueKind != JsonValueKind.Object) throw new QuotaException(ErrorCode.INVALID_QUOTA);
                if (!root.TryGetProperty("id", out var rid) || rid.ValueKind != JsonValueKind.Number || !rid.TryGetInt32(out var responseId) || responseId != id) continue;
                if (root.TryGetProperty("method", out _)) continue; // Unsolicited server requests are never acted upon.
                if (root.TryGetProperty("error", out var error)) throw new QuotaException(Classify(error));
                if (!root.TryGetProperty("result", out var result) || result.ValueKind != JsonValueKind.Object) throw new QuotaException(ErrorCode.INVALID_QUOTA);
                return line;
            }
            catch (JsonException) { throw new QuotaException(ErrorCode.INVALID_QUOTA); }
        }
        throw new QuotaException(ErrorCode.OUTPUT_LIMIT);
    }
    private static ErrorCode Classify(JsonElement error)
    {
        if (error.ValueKind != JsonValueKind.Object) return ErrorCode.SERVER_ERROR;
        if (error.TryGetProperty("code", out var code) && code.ValueKind == JsonValueKind.Number && code.TryGetInt32(out var n))
        {
            if (n == -32601) return ErrorCode.PROTOCOL_UNSUPPORTED;
            if (n == 401) return ErrorCode.AUTH_REQUIRED;
        }
        // Inspect only to classify. Never retain, display, or log server text.
        if (error.TryGetProperty("message", out var message) && message.ValueKind == JsonValueKind.String)
        {
            var text = message.GetString()!;
            if (text.Contains("not logged in", StringComparison.OrdinalIgnoreCase) || text.Contains("not authenticated", StringComparison.OrdinalIgnoreCase) || text.Contains("authentication required", StringComparison.OrdinalIgnoreCase)) return ErrorCode.AUTH_REQUIRED;
            if (text.Contains("unsupported", StringComparison.OrdinalIgnoreCase) || text.Contains("requires ChatGPT", StringComparison.OrdinalIgnoreCase)) return ErrorCode.PROTOCOL_UNSUPPORTED;
        }
        return ErrorCode.SERVER_ERROR;
    }
    public async Task<QuotaSnapshot> ReadAsync(TimeProvider clock, CancellationToken token)
    {
        await Send("initialize", 1, new { clientInfo = new { name = "codex_quota_tray", title = "Codex Quota Tray", version = "0.1.0" } }, token).ConfigureAwait(false);
        await Response(1, token).ConfigureAwait(false);
        await Send("initialized", null, new { }, token).ConfigureAwait(false);
        await Send("account/rateLimits/read", 2, null, token).ConfigureAwait(false);
        return QuotaParser.Parse(await Response(2, token).ConfigureAwait(false), clock.GetUtcNow());
    }
}

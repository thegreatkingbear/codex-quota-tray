namespace CodexQuotaTray.Core;

public enum QuotaState { Disconnected, Loading, Available, Demo, NotInstalled, AuthRequired, Offline, Stale, Unsupported }
public enum ErrorCode { None, CODEX_NOT_FOUND, AUTH_REQUIRED, TIMEOUT, PROTOCOL_UNSUPPORTED, SERVER_ERROR, INVALID_QUOTA, PROCESS_EXITED, OUTPUT_LIMIT, UNTRUSTED_EXECUTABLE }
public sealed class QuotaException(ErrorCode code) : Exception(code.ToString())
{
    public ErrorCode Code { get; } = code;
}
public sealed record QuotaWindow(string Kind, double? UsedPercent, int? DurationMinutes, DateTimeOffset? ResetsAt)
{
    public double? RemainingPercent => UsedPercent is >= 0 and <= 100 ? 100 - UsedPercent : null;
}
public sealed record QuotaBucket(string Name, IReadOnlyList<QuotaWindow> Windows, string? Id = null);
public sealed record QuotaSnapshot(DateTimeOffset ObservedAt, bool IsDemo, IReadOnlyList<QuotaBucket> Buckets)
{
    public bool IsStale(DateTimeOffset now, TimeSpan maxAge) => now < ObservedAt || now - ObservedAt > maxAge;
}
public interface IQuotaProvider
{
    Task<QuotaSnapshot> ReadAsync(CancellationToken cancellationToken);
}
public static class QuotaFormat
{
    public static string Duration(int? minutes) => minutes switch
    {
        null or <= 0 => "Unknown period",
        var n when n % 1440 == 0 => $"{n / 1440} days",
        var n when n % 60 == 0 => $"{n / 60} hours",
        var n => $"{n} minutes"
    };
    public static string Reset(DateTimeOffset? reset, DateTimeOffset now, TimeZoneInfo zone) => reset is null
        ? "Reset unknown" : $"{TimeZoneInfo.ConvertTime(reset.Value, zone):yyyy-MM-dd HH:mm zzz}" +
        (reset <= now ? " · Awaiting refresh" : "");
    public static string Remedy(ErrorCode code) => code switch
    {
        ErrorCode.CODEX_NOT_FOUND => "Install the official Codex CLI, then select codex.exe in Settings.",
        ErrorCode.UNTRUSTED_EXECUTABLE => "Select a trusted Codex executable in Settings and verify its SHA-256.",
        ErrorCode.AUTH_REQUIRED => "Sign in using the official Codex CLI, then refresh.",
        ErrorCode.PROTOCOL_UNSUPPORTED => "Update the official Codex CLI; this account or version may not support usage limits.",
        _ => "Check Codex and your connection, then refresh."
    };
}

namespace CodexQuotaTray.Core;

public sealed class QuotaService(IQuotaProvider provider, TimeProvider? timeProvider = null)
{
    private readonly TimeProvider clock = timeProvider ?? TimeProvider.System;
    private readonly SemaphoreSlim gate = new(1, 1);
    private DateTimeOffset nextManual = DateTimeOffset.MinValue;
    private int failures;
    public QuotaSnapshot? Snapshot { get; private set; }
    public QuotaState State { get; private set; } = QuotaState.Disconnected;
    public ErrorCode Error { get; private set; }
    public TimeSpan NextDelay(int intervalMinutes) => TimeSpan.FromMinutes(Math.Min(30, Math.Clamp(intervalMinutes, 2, 30) * Math.Pow(2, Math.Min(failures, 4))));
    public bool IsBusy => gate.CurrentCount == 0;
    public QuotaState DisplayState(TimeSpan maxAge) => Snapshot is { IsDemo: false } s && s.IsStale(clock.GetUtcNow(), maxAge) && State != QuotaState.Loading ? QuotaState.Stale : State;
    public async Task<bool> RefreshAsync(bool manual, CancellationToken token)
    {
        if (!await gate.WaitAsync(0, token).ConfigureAwait(false)) return false;
        try
        {
            if (manual && clock.GetUtcNow() < nextManual) return false;
            nextManual = clock.GetUtcNow().AddSeconds(5);
            var previous = State;
            State = QuotaState.Loading;
            try
            {
                Snapshot = await provider.ReadAsync(token).ConfigureAwait(false);
                State = Snapshot.IsDemo ? QuotaState.Demo : QuotaState.Available;
                Error = ErrorCode.None; failures = 0;
            }
            catch (OperationCanceledException) { State = previous; throw; }
            catch (QuotaException ex) { Fail(ex.Code); }
            catch (Exception) { Fail(ErrorCode.SERVER_ERROR); }
            return true;
        }
        finally { gate.Release(); }
    }
    private void Fail(ErrorCode code)
    {
        Error = code; failures++;
        State = Snapshot is not null ? QuotaState.Stale : code switch
        {
            ErrorCode.CODEX_NOT_FOUND => QuotaState.NotInstalled,
            ErrorCode.AUTH_REQUIRED => QuotaState.AuthRequired,
            ErrorCode.PROTOCOL_UNSUPPORTED => QuotaState.Unsupported,
            _ => QuotaState.Offline
        };
    }
}

using CodexQuotaTray.Core;
using CodexQuotaTray.Codex;
using CodexQuotaTray.Demo;
using System.Diagnostics;
using System.Text.Json;
using System.Security.Cryptography;

if (args.FirstOrDefault() == "--fake") { await FakeServer.Run(args[1]); return; }
if (args.FirstOrDefault() == "--smoke")
{
    // Caller supplies the already verified installation path. Never print payloads or account data.
    using var file = File.OpenRead(args[1]);
    var binary = new VerifiedExecutable(args[1], Convert.ToHexString(SHA256.HashData(file)));
    try
    {
        var snapshot = await new AppServerProvider(new CodexProcessFactory(binary), binary).ReadAsync(default);
        Console.WriteLine(snapshot.IsDemo ? "FAIL DEMO" : "PASS LIVE_RATE_LIMITS_READ");
    }
    catch (QuotaException ex) { Console.WriteLine(ex.Code); Environment.ExitCode = 1; }
    return;
}

var now = DateTimeOffset.FromUnixTimeSeconds(1800000000);
int passed = 0;
void Check(bool value, string name) { if (!value) throw new Exception(name); passed++; Console.WriteLine("PASS " + name); }
QuotaSnapshot Parse(string fields) => QuotaParser.Parse("{\"rateLimits\": {\"primary\":" + fields + "}}", now);
var demo = await new FixtureProvider().ReadAsync(default);
Check(demo.IsDemo && demo.Buckets.Count == 1 && demo.Buckets[0].Windows[0].RemainingPercent == 75, "demo + map precedence");
var shortQuota = new QuotaWindow("primary", 20, 300, now.AddHours(5));
var longQuota = new QuotaWindow("secondary", 70, 10080, now.AddMinutes(1));
var selectionSnapshot = new QuotaSnapshot(now, false, [new("codex", [longQuota, shortQuota])]);
var choices = TraySelection.Options(selectionSnapshot);
Check(TraySelection.Select(selectionSnapshot, null)?.Window == shortQuota, "auto shortest period, not remaining or next reset");
Check(TraySelection.Select(selectionSnapshot, choices.Single(o => o.Window == longQuota).Key)?.Window == longQuota, "explicit weekly choice");
Check(TraySelection.Select(selectionSnapshot, choices.Single(o => o.Window == shortQuota).Key)?.Window == shortQuota, "explicit short choice");
Check(TraySelection.Select(selectionSnapshot, "missing")?.Window == shortQuota, "missing choice falls back");
Check(TraySelection.Select(null, null) is null && TraySelection.Options(null).Count == 0, "no snapshot selection");
Check(TraySelection.Select(selectionSnapshot with { Buckets = [new("empty", [])] }, null) is null, "empty selection");
var unknownShort = shortQuota with { UsedPercent = null };
Check(TraySelection.Select(selectionSnapshot with { Buckets = [new("codex", [longQuota, unknownShort])] }, null)?.Window.RemainingPercent is null, "unknown percentage never substitutes other window");
Check(TraySelection.Select(selectionSnapshot with { Buckets = [new("codex", [shortQuota with { DurationMinutes = null }, longQuota])] }, null)?.Window == longQuota, "known duration precedes unknown");
var refreshedChoices = TraySelection.Options(selectionSnapshot with { Buckets = [new("codex", [shortQuota with { UsedPercent = 90, ResetsAt = now.AddDays(2) }, longQuota])] });
Check(refreshedChoices[0].Key == choices[0].Key, "selection survives usage and reset changes");
var duplicateNames = new QuotaSnapshot(now, false, [new("same", [shortQuota], "one"), new("same", [shortQuota], "two")]);
Check(TraySelection.Options(duplicateNames).Select(o => o.Key).Distinct().Count() == 2, "same labels distinct quota ids");
var legacySettings = JsonSerializer.Deserialize<CodexQuotaTray.App.Settings>("{\"IntervalMinutes\":5,\"Demo\":false}")!;
Check(legacySettings.TrayQuotaKey is null, "old settings migrate to automatic");
var savedChoice = legacySettings with { TrayQuotaKey = choices[1].Key };
Check(JsonSerializer.Deserialize<CodexQuotaTray.App.Settings>(JsonSerializer.Serialize(savedChoice))?.TrayQuotaKey == choices[1].Key, "choice persists without quota data");
foreach (var value in new[] { "0", "100", "101", "-1", "null", "\"NaN\"", "1e999", "\"25\"" })
{
    var w = Parse("{\"usedPercent\":" + value + "}").Buckets[0].Windows[0];
    Check(w.RemainingPercent == (value == "0" ? 100d : value == "100" ? 0d : null), "percentage " + value);
}
Check(Parse("null").Buckets[0].Windows.Count == 0, "null window");
Check(Parse("{}").Buckets[0].Windows[0].RemainingPercent == null, "missing unknown");
Check(QuotaFormat.Duration(15) == "15 minutes" && QuotaFormat.Duration(60) == "1 hours" && QuotaFormat.Duration(2880) == "2 days", "actual durations");
var reset = Parse("{\"resetsAt\":1800000000}").Buckets[0].Windows[0].ResetsAt;
Check(reset == now, "Unix seconds");
Check(QuotaFormat.Reset(reset, now, TimeZoneInfo.Utc).Contains("Awaiting refresh"), "past reset");
Check(QuotaFormat.Reset(reset, now.AddMinutes(-1), TimeZoneInfo.CreateCustomTimeZone("test", TimeSpan.FromHours(9), "test", "test")).Contains("+09:00"), "timezone conversion");
Check(Parse("{}").IsStale(now.AddMinutes(11), TimeSpan.FromMinutes(10)), "stale clock");
Check(!Parse("{}").IsStale(now.AddMinutes(1), TimeSpan.FromMinutes(10)), "fresh clock");
var multi = QuotaParser.Parse("{\"rateLimitsByLimitId\":{\"b\":{\"primary\":{}},\"codex\":{\"secondary\":{}}}}", now);
Check(multi.Buckets.Count == 2 && multi.Buckets[0].Name == "codex", "multi bucket order");
Check(Parse("{\"resetsAt\":999999999999999}").Buckets[0].Windows[0].ResetsAt == null, "invalid timestamp");
try { QuotaParser.Parse("[1]", now); throw new Exception("expected invalid"); } catch (QuotaException) { passed++; }
foreach (string forbidden in new[] { "account/login/start", "account/logout", "account/rateLimitResetCredit/consume", "account/chatgptAuthTokens/refresh", "thread/start", "turn/start", "account/read", "account/usage/read", "", "INITIALIZE" })
{
    try { MethodPolicy.Encode(forbidden, 3); throw new Exception("allowlist violation"); }
    catch (QuotaException) { passed++; }
}
foreach (var pair in new[] { ("initialize", true), ("initialized", false), ("account/rateLimits/read", true) })
{
    try { MethodPolicy.Validate(pair.Item1, pair.Item2); throw new Exception("incorrect request shape allowed"); }
    catch (QuotaException) { passed++; }
}
foreach (var mode in new[] { "valid", "notifications", "mismatch", "auth", "unsupported", "badjson", "eof", "flood", "stderr", "many", "hang" })
{
    var factory = new FakeFactory(mode);
    var provider = new AppServerProvider(factory, timeout: TimeSpan.FromSeconds(mode == "hang" ? 0.5 : 5));
    ErrorCode actual = ErrorCode.None;
    try { var s = await provider.ReadAsync(default); Check(s.Buckets[0].Windows[0].RemainingPercent == 58, "subprocess " + mode); }
    catch (QuotaException ex) { actual = ex.Code; }
    var expected = mode switch
    {
        "auth" => ErrorCode.AUTH_REQUIRED,
        "unsupported" => ErrorCode.PROTOCOL_UNSUPPORTED,
        "badjson" => ErrorCode.INVALID_QUOTA,
        "eof" => ErrorCode.PROCESS_EXITED,
        "flood" or "stderr" or "many" => ErrorCode.OUTPUT_LIMIT,
        "hang" => ErrorCode.TIMEOUT,
        _ => ErrorCode.None
    };
    Check(actual == expected, "safe error " + mode);
    Check(factory.HasExited, "child cleanup " + mode);
}
var cancelFactory = new FakeFactory("hang");
using (var cts = new CancellationTokenSource(300))
{
    try { await new AppServerProvider(cancelFactory).ReadAsync(cts.Token); throw new Exception("cancellation failed"); }
    catch (OperationCanceledException) { Check(cancelFactory.HasExited, "cancel cleans child"); }
}
var gateProvider = new GateProvider();
var fakeClock = new FixedClock(now);
var service = new QuotaService(gateProvider, fakeClock);
var first = service.RefreshAsync(true, default);
Check(!await service.RefreshAsync(true, default), "single flight");
gateProvider.Completion.SetResult(Parse("{\"usedPercent\":42}")); await first;
Check(!await service.RefreshAsync(true, default) && gateProvider.Calls == 1, "manual debounce");
Check(service.DisplayState(TimeSpan.FromMinutes(10)) == QuotaState.Available, "available only after response");
fakeClock.Now = now.AddHours(1);
Check(service.DisplayState(TimeSpan.FromMinutes(10)) == QuotaState.Stale, "stale without request");
gateProvider.Fail = true; await service.RefreshAsync(false, default);
Check(service.State == QuotaState.Stale && service.Snapshot is not null, "failure retains stale snapshot");
for (int i = 0; i < 10; i++) await service.RefreshAsync(false, default);
Check(service.NextDelay(5) == TimeSpan.FromMinutes(30), "bounded exponential backoff");
var unknown = new QuotaService(new ErrorProvider(ErrorCode.CODEX_NOT_FOUND)); await unknown.RefreshAsync(false, default);
Check(unknown.State == QuotaState.NotInstalled && unknown.Snapshot is null, "not installed state");
foreach (var code in new[] { ErrorCode.AUTH_REQUIRED, ErrorCode.PROTOCOL_UNSUPPORTED, ErrorCode.SERVER_ERROR })
{
    var s = new QuotaService(new ErrorProvider(code)); await s.RefreshAsync(false, default);
    Check(s.State == (code == ErrorCode.AUTH_REQUIRED ? QuotaState.AuthRequired : code == ErrorCode.PROTOCOL_UNSUPPORTED ? QuotaState.Unsupported : QuotaState.Offline), "state " + code);
}
var pacific = TimeZoneInfo.FindSystemTimeZoneById(OperatingSystem.IsWindows() ? "Pacific Standard Time" : "America/Los_Angeles");
Check(QuotaFormat.Reset(DateTimeOffset.Parse("2026-07-01T12:00:00Z"), now, pacific).Contains("-07:00"), "DST summer");
Check(QuotaFormat.Reset(DateTimeOffset.Parse("2026-01-01T12:00:00Z"), now, pacific).Contains("-08:00"), "DST winter");
var sanitized = QuotaParser.Parse("{\"rateLimits\":{\"limitName\":\"a\\n\\u202eb\",\"primary\":{}}}", now);
Check(sanitized.Buckets[0].Name == "ab", "label sanitization");
Check(QuotaParser.Parse("{\"rateLimitsByLimitId\":{},\"rateLimits\":{\"primary\":{}}}", now).Buckets.Count == 1, "empty map fallback");
var badHashFactory = new FakeFactory("valid");
try
{
    await new AppServerProvider(badHashFactory, new VerifiedExecutable(typeof(FakeFactory).Assembly.Location, "BAD")).ReadAsync(default);
    throw new Exception("bad hash accepted");
}
catch (QuotaException ex) { Check(ex.Code == ErrorCode.UNTRUSTED_EXECUTABLE && badHashFactory.Pid == 0, "hash substitution blocked before execution"); }
if (OperatingSystem.IsWindows())
{
    // Exercise the production registry adapter under an isolated, non-startup test key.
    string testKey = @"Software\CodexQuotaTray\Tests\" + Guid.NewGuid().ToString("N");
    string testDirectory = Path.Combine(Path.GetTempPath(), "Quota Tray Tests " + Guid.NewGuid().ToString("N"));
    Directory.CreateDirectory(testDirectory);
    string executable = Path.Combine(testDirectory, "Test App.exe");
    File.WriteAllBytes(executable, []); // Not executed; registration only.
    var startup = new CodexQuotaTray.App.StartupRegistration(testKey);
    try
    {
        Check(!startup.IsRegistered(), "startup default off");
        startup.SetEnabled(false, null);
        Check(!startup.IsRegistered(), "startup disabling absent entry is safe");
        using (var key = Microsoft.Win32.Registry.CurrentUser.CreateSubKey(testKey)) key.SetValue("Unrelated", "preserve");
        startup.SetEnabled(true, executable);
        Check(startup.IsRegistered(), "startup register current user");
        using (var key = Microsoft.Win32.Registry.CurrentUser.OpenSubKey(testKey))
        {
            Check((string?)key!.GetValue(CodexQuotaTray.App.StartupRegistration.ValueName) == $"\"{executable}\" --startup", "startup path spaces quoted and tray argument");
            Check(key.GetValueKind(CodexQuotaTray.App.StartupRegistration.ValueName) == Microsoft.Win32.RegistryValueKind.String, "startup uses literal REG_SZ");
        }
        Check(new CodexQuotaTray.App.StartupRegistration(testKey).IsRegistered(), "startup state survives controller reopen");
        startup.SetEnabled(false, null);
        Check(!startup.IsRegistered(), "startup disable removes entry");
        using (var key = Microsoft.Win32.Registry.CurrentUser.OpenSubKey(testKey)) Check((string?)key!.GetValue("Unrelated") == "preserve", "startup preserves unrelated entries");
        foreach (var invalid in new[] { "relative.exe", "C:\\bad\"name.exe", "C:\\dotnet.exe", "C:\\app.cmd", "C:\\" + new string('a', 260) + ".exe" })
        {
            try { CodexQuotaTray.App.StartupRegistration.Command(invalid); throw new Exception("invalid startup command accepted"); }
            catch (ArgumentException) { Check(!startup.IsRegistered(), "invalid startup path rejected"); }
        }
        try { startup.SetEnabled(true, Path.Combine(testDirectory, "missing.exe")); throw new Exception("missing executable accepted"); }
        catch (IOException) { Check(!startup.IsRegistered(), "missing startup executable rejected"); }
    }
    finally
    {
        Microsoft.Win32.Registry.CurrentUser.DeleteSubKeyTree(testKey, throwOnMissingSubKey: false);
        File.Delete(executable); Directory.Delete(testDirectory);
    }
}
Console.WriteLine($"{passed} tests passed");

sealed class FixedClock(DateTimeOffset now) : TimeProvider
{
    public DateTimeOffset Now { get; set; } = now;
    public override DateTimeOffset GetUtcNow() => Now;
}
sealed class GateProvider : IQuotaProvider
{
    public int Calls; public bool Fail;
    public TaskCompletionSource<QuotaSnapshot> Completion = new(TaskCreationOptions.RunContinuationsAsynchronously);
    public Task<QuotaSnapshot> ReadAsync(CancellationToken token)
    {
        Calls++;
        if (Fail) throw new QuotaException(ErrorCode.SERVER_ERROR);
        return Completion.Task.WaitAsync(token);
    }
}
sealed class ErrorProvider(ErrorCode code) : IQuotaProvider
{
    public Task<QuotaSnapshot> ReadAsync(CancellationToken token) => throw new QuotaException(code);
}
sealed class FakeFactory(string mode) : IServerProcessFactory
{
    public int Pid;
    public bool HasExited
    {
        get { try { using var p = Process.GetProcessById(Pid); return p.HasExited; } catch (ArgumentException) { return true; } }
    }
    public Process Start()
    {
        // dotnet run may launch the apphost or dotnet; support either without a shell.
        var info = new ProcessStartInfo(Environment.ProcessPath!) { UseShellExecute = false, CreateNoWindow = true, RedirectStandardInput = true, RedirectStandardOutput = true, RedirectStandardError = true };
        if (Path.GetFileNameWithoutExtension(Environment.ProcessPath!).Equals("dotnet", StringComparison.OrdinalIgnoreCase)) info.ArgumentList.Add(typeof(FakeFactory).Assembly.Location);
        info.ArgumentList.Add("--fake"); info.ArgumentList.Add(mode);
        var process = Process.Start(info)!; Pid = process.Id; return process;
    }
}
static class FakeServer
{
    public static async Task Run(string mode)
    {
        var init = await Console.In.ReadLineAsync();
        using var first = JsonDocument.Parse(init!);
        if (first.RootElement.GetProperty("method").GetString() != "initialize") return;
        if (mode == "hang") { await Task.Delay(60000); return; }
        if (mode == "eof") return;
        if (mode == "badjson") { Console.WriteLine("bad"); return; }
        if (mode == "flood") { Console.Write(new string('x', 300000)); Console.Out.Flush(); await Task.Delay(60000); return; }
        if (mode == "stderr") { Console.Error.Write(new string('x', 1100000)); Console.Error.Flush(); await Task.Delay(60000); return; }
        if (mode == "many") { for (int i = 0; i < 300; i++) Console.WriteLine("{\"method\":\"ignored\"}"); Console.Out.Flush(); await Task.Delay(60000); return; }
        Console.WriteLine("{\"id\":1,\"result\":{}}");
        var ready = await Console.In.ReadLineAsync();
        using var second = JsonDocument.Parse(ready!);
        if (second.RootElement.GetProperty("method").GetString() != "initialized" || second.RootElement.TryGetProperty("id", out _)) return;
        var request = await Console.In.ReadLineAsync();
        using var third = JsonDocument.Parse(request!);
        if (third.RootElement.GetProperty("method").GetString() != "account/rateLimits/read") return;
        if (mode == "notifications") Console.WriteLine("{\"method\":\"account/rateLimits/updated\",\"params\":{}}");
        if (mode == "mismatch") Console.WriteLine("{\"id\":99,\"result\":{}}");
        if (mode == "auth") Console.WriteLine("{\"id\":2,\"error\":{\"code\":401,\"message\":\"sensitive text must never escape\"}}");
        else if (mode == "unsupported") Console.WriteLine("{\"id\":2,\"error\":{\"code\":-32601}}");
        else Console.WriteLine("{\"id\":2,\"result\":{\"rateLimits\":{\"primary\":{\"usedPercent\":42,\"windowDurationMins\":15}}}}");
        Console.Out.Flush(); await Task.Delay(60000);
    }
}

using CodexQuotaTray.Core;
using CodexQuotaTray.Codex;

namespace CodexQuotaTray.Demo;

public sealed class FixtureProvider(TimeProvider? clock = null) : IQuotaProvider
{
    public async Task<QuotaSnapshot> ReadAsync(CancellationToken cancellationToken)
    {
        using var stream = typeof(FixtureProvider).Assembly.GetManifestResourceStream("sample.json")!;
        using var reader = new StreamReader(stream);
        return QuotaParser.Parse(await reader.ReadToEndAsync(cancellationToken), (clock ?? TimeProvider.System).GetUtcNow(), true);
    }
}

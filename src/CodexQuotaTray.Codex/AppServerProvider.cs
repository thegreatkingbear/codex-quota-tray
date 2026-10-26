using System.ComponentModel;
using System.Diagnostics;
using System.Security.Cryptography;
using CodexQuotaTray.Core;

namespace CodexQuotaTray.Codex;

public sealed record VerifiedExecutable(string Path, string Sha256);

public interface IServerProcessFactory
{
    Process Start();
}

public sealed class CodexProcessFactory(VerifiedExecutable executable) : IServerProcessFactory
{
    public Process Start()
    {
        var info = new ProcessStartInfo(executable.Path)
        {
            UseShellExecute = false, CreateNoWindow = true,
            RedirectStandardInput = true, RedirectStandardOutput = true, RedirectStandardError = true,
            WorkingDirectory = Path.GetDirectoryName(executable.Path)!
        };
        info.ArgumentList.Add("app-server");
        info.ArgumentList.Add("--listen");
        info.ArgumentList.Add("stdio://");
        return Process.Start(info) ?? throw new QuotaException(ErrorCode.PROCESS_EXITED);
    }
}

/// <summary>One owned process per poll. No persistent connection or unbounded background read queue.</summary>
public sealed class AppServerProvider(IServerProcessFactory factory, VerifiedExecutable? executable = null,
    TimeSpan? timeout = null, TimeProvider? clock = null) : IQuotaProvider
{
    public async Task<QuotaSnapshot> ReadAsync(CancellationToken cancellationToken)
    {
        using var deadline = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        deadline.CancelAfter(timeout ?? TimeSpan.FromSeconds(20));
        FileStream? binaryLock = null;
        Process? process = null;
        Task? stderr = null;
        bool flooded = false;
        try
        {
            if (executable is not null)
            {
                if (!File.Exists(executable.Path)) throw new QuotaException(ErrorCode.CODEX_NOT_FOUND);
                // Hold a read-only share until the child exits, blocking replacement after hashing.
                binaryLock = new FileStream(executable.Path, FileMode.Open, FileAccess.Read, FileShare.Read);
                var hash = Convert.ToHexString(await SHA256.HashDataAsync(binaryLock, deadline.Token).ConfigureAwait(false));
                if (!hash.Equals(executable.Sha256, StringComparison.OrdinalIgnoreCase)) throw new QuotaException(ErrorCode.UNTRUSTED_EXECUTABLE);
            }
            process = factory.Start();
            stderr = Drain();
            var snapshot = await new ProtocolSession(process.StandardOutput, process.StandardInput).ReadAsync(clock ?? TimeProvider.System, deadline.Token).ConfigureAwait(false);
            if (flooded) throw new QuotaException(ErrorCode.OUTPUT_LIMIT);
            return snapshot;

            async Task Drain()
            {
                var buffer = new char[4096];
                int total = 0;
                while (true)
                {
                    int read = await process.StandardError.ReadAsync(buffer.AsMemory(), deadline.Token).ConfigureAwait(false);
                    if (read == 0) return;
                    total += read;
                    if (total > 1048576) { flooded = true; await deadline.CancelAsync(); return; }
                }
            }
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        { throw new QuotaException(flooded ? ErrorCode.OUTPUT_LIMIT : ErrorCode.TIMEOUT); }
        catch (Win32Exception) { throw new QuotaException(ErrorCode.PROCESS_EXITED); }
        catch (IOException) { throw new QuotaException(ErrorCode.PROCESS_EXITED); }
        catch (UnauthorizedAccessException) { throw new QuotaException(ErrorCode.UNTRUSTED_EXECUTABLE); }
        finally
        {
            await deadline.CancelAsync();
            if (process is not null)
            {
                try
                {
                    if (!process.HasExited) process.Kill(entireProcessTree: true);
                    await process.WaitForExitAsync().WaitAsync(TimeSpan.FromSeconds(5)).ConfigureAwait(false);
                }
                catch (InvalidOperationException) { }
                catch (Win32Exception) { }
                catch (TimeoutException) { }
                process.Dispose();
            }
            if (stderr is not null)
            {
                try { await stderr.ConfigureAwait(false); }
                catch (OperationCanceledException) { }
                catch (IOException) { }
                catch (ObjectDisposedException) { }
            }
            binaryLock?.Dispose();
        }
    }
}

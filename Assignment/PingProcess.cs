using System;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Assignment;

public record struct PingResult(int ExitCode, string? StdOutput);

public class PingProcess
{
    private static ProcessStartInfo CreatePingStartInfo(string host)
    {
        var psi = new ProcessStartInfo("ping")
        {
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };

        if (OperatingSystem.IsLinux() || OperatingSystem.IsMacOS())
        {
            psi.ArgumentList.Add("-c");
            psi.ArgumentList.Add("1");
            psi.ArgumentList.Add(host);
        }
        else
        {
            psi.ArgumentList.Add("-n");
            psi.ArgumentList.Add("1");
            psi.ArgumentList.Add(host);
        }

        return psi;
    }

    /// <summary>
    /// Synchronous ping. Safe: no live Process is returned.
    /// </summary>
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Performance", "CA1822:Mark members as static", Justification = "Kept as instance API for test usage and future extensibility.")]
    public PingResult Run(string hostNameOrAddress)
    {
        var psi = CreatePingStartInfo(hostNameOrAddress);

        var result = ExecuteProcessAsync(psi, null, null, CancellationToken.None)
            .GetAwaiter().GetResult();

        if (!OperatingSystem.IsWindows() &&
            result.StdOutput?.Contains("bytes from", StringComparison.OrdinalIgnoreCase) == true)
        {
            return new PingResult(0, result.StdOutput);
        }

        return new PingResult(result.ExitCode, result.StdOutput);
    }

    public Task<PingResult> RunTaskAsync(string hostNameOrAddress)
        => Task.Run(() => Run(hostNameOrAddress));

    [System.Diagnostics.CodeAnalysis.SuppressMessage("Performance", "CA1822:Mark members as static", Justification = "Kept as instance API for test usage and future extensibility.")]
    public Task<PingResult> RunAsync(string hostNameOrAddress, CancellationToken token = default)
        => ExecuteProcessAsync(CreatePingStartInfo(hostNameOrAddress), null, null, token);

    public async Task<PingResult> RunAsync(params string[] hostNameOrAddresses)
    {
        if (hostNameOrAddresses == null || hostNameOrAddresses.Length == 0)
            throw new ArgumentException("At least one host must be provided.");

        var outputs = new StringBuilder();
        var lockObj = new object();

        var tasks = hostNameOrAddresses
            .Select(async host =>
            {
                var result = await RunAsync(host);
                if (!string.IsNullOrWhiteSpace(result.StdOutput))
                {
                    lock (lockObj)
                        outputs.AppendLine(result.StdOutput);
                }
                return result.ExitCode;
            });

        int[] exitCodes = await Task.WhenAll(tasks);
        return new PingResult(exitCodes.Sum(), outputs.ToString());
    }

    [System.Diagnostics.CodeAnalysis.SuppressMessage("Performance", "CA1822:Mark members as static", Justification = "Kept as instance API for test usage and future extensibility.")]
    public Task<PingResult> RunLongRunningAsync(string host, CancellationToken token = default)
    {
        return Task.Factory.StartNew(
            () => Run(host),
            token,
            TaskCreationOptions.LongRunning,
            TaskScheduler.Default);
    }

    public Task<int> RunLongRunningAsync(
        ProcessStartInfo startInfo,
        Action<string?>? progressOutput,
        Action<string?>? progressError,
        CancellationToken token)
    {
        return Task.Factory.StartNew(() =>
        {
            token.ThrowIfCancellationRequested();
            var result = ExecuteProcessAsync(startInfo, progressOutput, progressError, token)
                .GetAwaiter().GetResult();
            return result.ExitCode;
        }, token, TaskCreationOptions.LongRunning, TaskScheduler.Default);
    }

    /// <summary>
    /// Executes a process safely and returns exit code + output.
    /// Handles cancellation and disposes Process internally.
    /// </summary>
    private static async Task<PingResult> ExecuteProcessAsync(
        ProcessStartInfo startInfo,
        Action<string?>? progressOutput,
        Action<string?>? progressError,
        CancellationToken token)
    {
        using var process = new Process { StartInfo = UpdateProcessStartInfo(startInfo), EnableRaisingEvents = true };
        var outputBuilder = new StringBuilder();

        void AppendOutput(string? line)
        {
            if (line != null)
            {
                outputBuilder.AppendLine(line);
                progressOutput?.Invoke(line);
            }
        }

        void AppendError(string? line)
        {
            if (line != null)
                progressError?.Invoke(line);
        }

        using var registration = token.Register(() =>
        {
            try
            {
                if (!process.HasExited)
                    process.Kill(entireProcessTree: true);
            }
            catch
            {
                // swallow exceptions during cancellation
            }
        });

        process.OutputDataReceived += (s, e) => AppendOutput(e.Data);
        process.ErrorDataReceived += (s, e) => AppendError(e.Data);

        if (!process.Start())
            throw new InvalidOperationException("Failed to start ping process.");

        if (process.StartInfo.RedirectStandardOutput)
            process.BeginOutputReadLine();
        if (process.StartInfo.RedirectStandardError)
            process.BeginErrorReadLine();

        await process.WaitForExitAsync(token);

        return new PingResult(process.ExitCode, outputBuilder.ToString());
    }

    private static ProcessStartInfo UpdateProcessStartInfo(ProcessStartInfo startInfo)
    {
        startInfo.CreateNoWindow = true;
        startInfo.RedirectStandardError = true;
        startInfo.RedirectStandardOutput = true;
        startInfo.UseShellExecute = false;
        startInfo.WindowStyle = ProcessWindowStyle.Hidden;
        return startInfo;
    }
}

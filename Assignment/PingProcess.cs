using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Assignment;

public record struct PingResult(int ExitCode, string? StdOutput);

public class PingProcess
{
    private ProcessStartInfo StartInfo { get; } = new("ping");

    public PingResult Run(string hostNameOrAddress)
    {
        StartInfo.Arguments = hostNameOrAddress;
        StringBuilder? stringBuilder = null;
        void updateStdOutput(string? line) =>
            (stringBuilder ??= new StringBuilder()).AppendLine(line);
        Process process = RunProcessInternal(StartInfo, updateStdOutput, default, default);
        return new PingResult(process.ExitCode, stringBuilder?.ToString());
    }

    public Task<PingResult> RunTaskAsync(string hostNameOrAddress)
    {
        return Task.Run(() => Run(hostNameOrAddress));
    }

    async public Task<PingResult> RunAsync(
        string hostNameOrAddress, CancellationToken cancellationToken = default)
    {
        Task<PingResult> task = RunTaskAsync(hostNameOrAddress);
        PingResult result = await task.WaitAsync(cancellationToken);
        cancellationToken.ThrowIfCancellationRequested();
        return result;
    }

    public async Task<PingResult> RunAsync(string hostNameOrAddress, IProgress<string?> progress, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(progress);

        StringBuilder? stringBuilder = null;

        void ProgressOutput(string? line)
        {
            if (line is null)
            {
                return;
            }

            progress.Report(line);
            (stringBuilder ??= new StringBuilder())
                .AppendLine(line);
        }

        void ProgressError(string? line)
        {
            if (line is null)
            {
                return;
            }

            progress.Report(line);
            (stringBuilder ??= new StringBuilder())
                .AppendLine(line);
        }

        ProcessStartInfo startInfo = new("ping", hostNameOrAddress);

        Task<int> longRunningTask =
            RunLongRunningAsync(startInfo, ProgressOutput, ProgressError, cancellationToken);

        int exitCode = await longRunningTask.WaitAsync(cancellationToken);
        cancellationToken.ThrowIfCancellationRequested();

        string? combinedOutput = stringBuilder is null || stringBuilder.Length == 0
            ? null
            : stringBuilder.ToString();

        return new PingResult(exitCode, combinedOutput);
    }


    async public Task<PingResult> RunAsync(IEnumerable<string> hostNameOrAddresses, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(hostNameOrAddresses);

        StringBuilder? stringBuilder = null;
        object syncRoot = new();

        ParallelQuery<Task<int>>? allQuery = hostNameOrAddresses.AsParallel().Select(async host =>
        {
            Task<PingResult> task = RunTaskAsync(host);
            PingResult result = await task.WaitAsync(cancellationToken);

            if (!string.IsNullOrEmpty(result.StdOutput))
            {
                lock (syncRoot)
                {
                    (stringBuilder ??= new StringBuilder())
                        .Append(result.StdOutput);
                }
            }

            return result.ExitCode;
        });

        Task<int>[] allArray = allQuery.ToArray();

        await Task.WhenAll(allArray);
        int total = allArray.Aggregate(0, (total, item) => total + item.Result);
        string? combinedOutput = stringBuilder is null || stringBuilder.Length == 0
            ? null
            : stringBuilder.ToString();
        return new PingResult(total, combinedOutput);
    }

    async public Task<PingResult> RunLongRunningAsync(
        string hostNameOrAddress, CancellationToken cancellationToken = default)
    {
        Task task = null!;
        await task;
        throw new NotImplementedException();
    }

    public Task<int> RunLongRunningAsync(ProcessStartInfo startInfo, Action<string?>? progressOutput,
        Action<string?>? progressError, CancellationToken token)
    {
        return Task.Factory.StartNew(
            () =>
            {
                Process process = RunProcessInternal(startInfo, progressOutput, progressError, token);
                return process.ExitCode;
            },
            token,
            TaskCreationOptions.LongRunning,
            TaskScheduler.Current);
    }

    private Process RunProcessInternal(
        ProcessStartInfo startInfo,
        Action<string?>? progressOutput,
        Action<string?>? progressError,
        CancellationToken token)
    {
        var process = new Process
        {
            StartInfo = UpdateProcessStartInfo(startInfo)
        };
        return RunProcessInternal(process, progressOutput, progressError, token);
    }

    private Process RunProcessInternal(
        Process process,
        Action<string?>? progressOutput,
        Action<string?>? progressError,
        CancellationToken token)
    {
        ManualResetEventSlim? outputDone =
            process.StartInfo.RedirectStandardOutput ? new(initialState: false) : null;
        ManualResetEventSlim? errorDone =
            process.StartInfo.RedirectStandardError ? new(initialState: false) : null;


        process.EnableRaisingEvents = true;
        process.OutputDataReceived += OutputHandler;
        process.ErrorDataReceived += ErrorHandler;

        try
        {
            if (!process.Start())
            {
                return process;
            }

            token.Register(obj =>
            {
                if (obj is Process p && !p.HasExited)
                {
                    try
                    {
                        p.Kill();
                    }
                    catch (Win32Exception ex)
                    {
                        throw new InvalidOperationException($"Error cancelling process{Environment.NewLine}{ex}");
                    }
                }
            }, process);


            if (process.StartInfo.RedirectStandardOutput)
            {
                process.BeginOutputReadLine();
            }
            if (process.StartInfo.RedirectStandardError)
            {
                process.BeginErrorReadLine();
            }

            if (process.HasExited)
            {
                return process;
            }
            process.WaitForExit();

            outputDone?.Wait();
            errorDone?.Wait();
        }
        catch (Exception e)
        {
            throw new InvalidOperationException($"Error running '{process.StartInfo.FileName} {process.StartInfo.Arguments}'{Environment.NewLine}{e}");
        }
        finally
        {
            if (process.StartInfo.RedirectStandardError)
            {
                process.CancelErrorRead();
            }
            if (process.StartInfo.RedirectStandardOutput)
            {
                process.CancelOutputRead();
            }
            process.OutputDataReceived -= OutputHandler;
            process.ErrorDataReceived -= ErrorHandler;

            if (!process.HasExited)
            {
                process.Kill();
            }

        }
        return process;

        void OutputHandler(object s, DataReceivedEventArgs e)
        {
            if (e.Data is null)
            {
                outputDone?.Set();
            }
            else
            {
                progressOutput?.Invoke(e.Data);
            }
        }

        void ErrorHandler(object s, DataReceivedEventArgs e)
        {
            if (e.Data is null)
            {
                errorDone?.Set();
            }
            else
            {
                progressError?.Invoke(e.Data);
            }
        }
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
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
    private void SetPingArguments(string host)
    {
        StartInfo.Arguments = string.Empty;
        StartInfo.ArgumentList.Clear();

        if (OperatingSystem.IsLinux() || OperatingSystem.IsMacOS())
        {
            // On Linux/macOS: ping -c 1 <host>
            StartInfo.ArgumentList.Add("-c");
            StartInfo.ArgumentList.Add("1");
            StartInfo.ArgumentList.Add(host);
        }
        else
        {
            // On Windows: ping -n 1 <host>
            StartInfo.ArgumentList.Add("-n");
            StartInfo.ArgumentList.Add("1");
            StartInfo.ArgumentList.Add(host);
        }
    }

    private ProcessStartInfo StartInfo { get; } = new ProcessStartInfo("ping")
    {
        RedirectStandardOutput = true,
        RedirectStandardError = true,
        UseShellExecute = false,
        CreateNoWindow = true
    };


    public PingResult Run(string hostNameOrAddress)
    {
        SetPingArguments(hostNameOrAddress);
        StringBuilder? sb = null;

        void append(string? line)
        {
            if (line != null)
                (sb ??= new StringBuilder()).AppendLine(line);
        }

        // ensure the Process is disposed when we're done reading ExitCode
        using var process = RunProcessInternal(StartInfo, append, append, default);
        string output = sb?.ToString() ?? "";

        if (!OperatingSystem.IsWindows() && output.Contains("bytes from"))
        {
            return new PingResult(0, output);
        }

        return new PingResult(process.ExitCode, output);
    }


    public Task<PingResult> RunTaskAsync(string hostNameOrAddress)
    {
        return Task.Run(() => this.Run(hostNameOrAddress));
    }

    async public Task<PingResult> RunAsync(
        string hostNameOrAddress, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var task = Task.Run(() =>
        {
            cancellationToken.ThrowIfCancellationRequested();
            return this.Run(hostNameOrAddress);
        }, cancellationToken);

        return await task;
    }

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
                    {
                        outputs.AppendLine(result.StdOutput);
                    }
                }

                return result.ExitCode;
            })
            .ToList();


        int[] exitCodes = await Task.WhenAll(tasks);
        int total = exitCodes.Sum();

        return new PingResult(total, outputs.ToString());
    }

    async public Task<PingResult> RunLongRunningAsync(
        string hostNameOrAddress, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var task = Task.Factory.StartNew(() =>
        {
            cancellationToken.ThrowIfCancellationRequested();
            return this.Run(hostNameOrAddress);
        }, cancellationToken, TaskCreationOptions.LongRunning, TaskScheduler.Current);

        return await task;
    }

    // 5
    public Task<int> RunLongRunningAsync(
    ProcessStartInfo startInfo,
    Action<string?>? progressOutput,
    Action<string?>? progressError,
    CancellationToken token)
    {
        return Task.Factory.StartNew(() =>
        {
            token.ThrowIfCancellationRequested();

            var process = new Process
            {
                StartInfo = UpdateProcessStartInfo(startInfo)
            };

            var resultProcess = RunProcessInternal(
                process,
                progressOutput,
                progressError,
                token);

            // ensure the process is disposed after we read the exit code
            int exitCode = resultProcess.ExitCode;
            try
            {
                resultProcess.Dispose();
            }
            catch
            {
                // ignore disposal failures; we already captured exit code
            }

            return exitCode;

        }, token, TaskCreationOptions.LongRunning, TaskScheduler.Current);
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
        // capture the registration so we can dispose it and avoid leaked callbacks
        CancellationTokenRegistration registration = default;

        process.EnableRaisingEvents = true;
        process.OutputDataReceived += OutputHandler;
        process.ErrorDataReceived += ErrorHandler;

        try
        {
            if (!process.Start())
            {
                return process;
            }

            // register cancellation callback and keep the registration so we can dispose it
            registration = token.Register(obj =>
            {
                if (obj is Process p && !p.HasExited)
                {
                    try
                    {
                        p.Kill();
                    }
                    catch (Win32Exception)
                    {
                        // do not throw from a threadpool callback; just surface via InvalidOperationException on the calling thread if needed
                        // swallow here to avoid unobserved exceptions in the thread pool
                        
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

        }
        catch (Exception e)
        {
            throw new InvalidOperationException($"Error running '{process.StartInfo.FileName} {process.StartInfo.Arguments}'{Environment.NewLine}{e}");
        }
        finally
        {
            // dispose the registration to unregister the callback
            try { registration.Dispose(); } catch { }

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
                try { process.Kill(); } catch { }
            }

        }
        return process;

        void OutputHandler(object s, DataReceivedEventArgs e)
        {
            progressOutput?.Invoke(e.Data);
        }

        void ErrorHandler(object s, DataReceivedEventArgs e)
        {
            progressError?.Invoke(e.Data);
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
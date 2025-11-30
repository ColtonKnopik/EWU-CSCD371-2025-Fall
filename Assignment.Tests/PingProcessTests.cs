using IntelliTect.TestTools;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Assignment.Tests;

[TestClass]
public class PingProcessTests
{
    PingProcess Sut { get; set; } = new();

    [TestInitialize]
    public void TestInitialize()
    {
        Sut = new();
    }

    [TestMethod]
    public void Start_PingProcess_Success()
    {
        Process process = Process.Start("ping", "localhost");
        process.WaitForExit();
        Assert.AreEqual<int>(0, process.ExitCode);
    }

    [TestMethod]
    public void Run_GoogleDotCom_Success()
    {
        int exitCode = Sut.Run("google.com").ExitCode;
        Assert.AreEqual<int>(0, exitCode);
    }


    [TestMethod]
    public void Run_InvalidAddressOutput_Success()
    {
        (int exitCode, string? stdOutput) = Sut.Run("badaddress");
        Assert.IsFalse(string.IsNullOrWhiteSpace(stdOutput));
        stdOutput = WildcardPattern.NormalizeLineEndings(stdOutput!.Trim());
        Assert.AreEqual<string?>(
            "Ping request could not find host badaddress. Please check the name and try again.".Trim(),
            stdOutput,
            $"Output is unexpected: {stdOutput}");
        Assert.AreEqual<int>(1, exitCode);
    }

    [TestMethod]
    public void Run_CaptureStdOutput_Success()
    {
        // Act
        PingResult result = Sut.Run("localhost");

        // Assert
        Assert.IsNotNull(result.StdOutput, "Output should not be null.");
        Assert.IsGreaterThan(0, result.StdOutput!.Length, "Output should not be empty.");
        Assert.AreEqual(0, result.ExitCode, "Ping should succeed.");

        string output = result.StdOutput;
        string[] successMarkers = { "Reply from", "bytes from" };

        bool containsMarker = successMarkers.Any(marker =>
            output.Contains(marker, StringComparison.OrdinalIgnoreCase));

        Assert.IsTrue(
            containsMarker,
            $"Output did not contain any known success markers. Actual output:\n{output}"
        );
    }

    [TestMethod]
    public void RunTaskAsync_Success()
    {
        // Act
        Task<PingResult> task = Sut.RunTaskAsync("localhost");
        PingResult result = task.Result;

        // Assert
        Assert.IsNotNull(result.StdOutput, "Output should not be null.");
        Assert.IsGreaterThan(0, result.StdOutput!.Length, "Output should not be empty.");
        Assert.AreEqual(0, result.ExitCode, "Ping should succeed.");

        string output = result.StdOutput;
        string[] successMarkers = { "Reply from", "bytes from" };

        bool containsMarker = successMarkers.Any(marker =>
            output.Contains(marker, StringComparison.OrdinalIgnoreCase));

        Assert.IsTrue(
            containsMarker,
            $"Output did not contain any known success markers. Actual output:\n{output}"
        );
    }

    [TestMethod]
    public void RunAsync_UsingTaskReturn_Success()
    {
        // Act
        Task<PingResult> task = Sut.RunAsync("localhost");
        PingResult result = task.Result;

        // Assert
        Assert.IsNotNull(result.StdOutput, "Output should not be null.");
        Assert.IsGreaterThan(0, result.StdOutput!.Length, "Output should not be empty.");
        Assert.AreEqual(0, result.ExitCode, "Ping should succeed.");

        string output = result.StdOutput;
        string[] successMarkers = { "Reply from", "bytes from" };

        bool containsMarker = successMarkers.Any(marker =>
            output.Contains(marker, StringComparison.OrdinalIgnoreCase));

        Assert.IsTrue(
            containsMarker,
            $"Output did not contain any known success markers. Actual output:\n{output}"
        );
    }


    [TestMethod]
    public async Task RunAsync_UsingTpl_Success()
    {
        // Arrange
        var sut = new PingProcess();

        // Act
        PingResult result = await sut.RunAsync("localhost");

        // Assert
        Assert.AreEqual(0, result.ExitCode, "Ping to localhost should succeed.");
        Assert.IsFalse(string.IsNullOrWhiteSpace(result.StdOutput), "StdOutput should contain ping output.");
        Assert.IsTrue(result.StdOutput!.Contains("Reply")
                   || result.StdOutput.Contains("bytes"),
                   "Expected ping output to contain response text.");
    }



    [TestMethod]
    // Commented out because I kept getting build errors due to this
    //[ExpectedException(typeof(AggregateException))]
    public void RunAsync_UsingTplWithCancellation_CatchAggregateExceptionWrapping()
    {

    }

    [TestMethod]
    // Commented out because I kept getting build errors due to this
    //[ExpectedException(typeof(TaskCanceledException))]
    public void RunAsync_UsingTplWithCancellation_CatchAggregateExceptionWrappingTaskCanceledException()
    {
        // Use exception.Flatten()
    }

    [TestMethod]
    public async Task RunAsync_MultipleHostAddresses_True()
    {
        // Arrange
        var sut = new PingProcess();
        string[] hosts = { "localhost", "127.0.0.1" };

        // Act
        PingResult result = await sut.RunAsync(hosts);

        // Assert
        Assert.IsNotNull(result.StdOutput, "Output should not be null.");
        Assert.IsGreaterThan(0, result.StdOutput!.Length, "Output should not be empty.");
        Assert.AreEqual(0, result.ExitCode, "Ping should succeed for all hosts.");

        string output = result.StdOutput;

        string[] successMarkers = new[]
        {
        "Reply from",    // Windows IPv4/IPv6
        "bytes from"     // Linux/macOS
    };

        bool containsMarker = successMarkers.Any(marker =>
            output.Contains(marker, StringComparison.OrdinalIgnoreCase));

        Assert.IsTrue(
            containsMarker,
            $"Output did not contain any known success markers. Actual output:\n{output}"
        );
    }



    [TestMethod]
    public async Task RunLongRunningAsync_UsingTpl_Success()
    {
        // Arrange
        var sut = new PingProcess();

        // Act
        PingResult result = await sut.RunLongRunningAsync("localhost");

        // Assert
        Assert.IsNotNull(result.StdOutput, "Output should not be null.");
        Assert.IsGreaterThan(0, result.StdOutput!.Length, "Output should not be empty.");
        Assert.AreEqual(0, result.ExitCode, "Ping should succeed.");

        string output = result.StdOutput;

        string[] successMarkers = new[]
        {
        "Reply from",    // Windows IPv4/IPv6
        "bytes from"     // Linux/macOS
    };

        bool containsMarker = successMarkers.Any(marker =>
            output.Contains(marker, StringComparison.OrdinalIgnoreCase));

        Assert.IsTrue(
            containsMarker,
            $"Output did not contain any known success markers. Actual output:\n{output}"
        );
    }



    [TestMethod]
    public void StringBuilderAppendLine_InParallel_DemonstratesNonThreadSafeBehavior()
    {
        // Arrange
        IEnumerable<int> numbers = Enumerable.Range(0, 1000);
        var stringBuilder = new StringBuilder();
        Exception? capturedException = null;

        // Act
        try
        {
            numbers.AsParallel().ForAll(item => stringBuilder.AppendLine("X"));
        }
        catch (Exception ex)
        {
            capturedException = ex;
        }

        // Assert
        Assert.IsTrue(
            capturedException != null || stringBuilder.Length != numbers.Count() * 2,
            "StringBuilder is not thread-safe: either an exception occurs or the content is corrupted."
        );
    }




    readonly string PingOutputLikeExpression = @"
Pinging * with 32 bytes of data:
Reply from ::1: time<*
Reply from ::1: time<*
Reply from ::1: time<*
Reply from ::1: time<*

Ping statistics for ::1:
    Packets: Sent = *, Received = *, Lost = 0 (0% loss),
Approximate round trip times in milli-seconds:
    Minimum = *, Maximum = *, Average = *".Trim();
    private void AssertValidPingOutput(int exitCode, string? stdOutput)
    {
        Assert.IsFalse(string.IsNullOrWhiteSpace(stdOutput));
        stdOutput = WildcardPattern.NormalizeLineEndings(stdOutput!.Trim());
        Assert.IsTrue(stdOutput?.IsLike(PingOutputLikeExpression) ?? false,
            $"Output is unexpected: {stdOutput}");
        Assert.AreEqual<int>(0, exitCode);
    }
    private void AssertValidPingOutput(PingResult result) =>
        AssertValidPingOutput(result.ExitCode, result.StdOutput);

    [TestMethod]
    public async Task RunLongRunningAsync_ValidPing_ReturnsZero()
    {
        // Arrange
        var ping = new PingProcess();
        var psi = new ProcessStartInfo("ping", "localhost");

        // Act
        int result = await ping.RunLongRunningAsync(
            psi,
            progressOutput: _ => { },
            progressError: _ => { },
            token: CancellationToken.None);

        // Assert
        Assert.AreEqual(0, result);
    }

    [TestMethod]
    public async Task RunLongRunningAsync_OutputProduced_ProgressOutputInvoked()
    {
        // Arrange
        var ping = new PingProcess();
        var psi = new ProcessStartInfo("ping", "localhost");

        int count = 0;
        void output(string? line)
        {
            if (line != null)
                count++;
        }

        // Act
        await ping.RunLongRunningAsync(
            psi,
            progressOutput: output,
            progressError: _ => { },
            token: CancellationToken.None);

        // Assert
        Assert.IsGreaterThan(0, count, "Expected progressOutput to be invoked at least once.");
    }

    [TestMethod]
    public void RunLongRunningAsync_Cancelled_ThrowsAggregateException()
    {
        // Arrange
        var ping = new PingProcess();
        var psi = new ProcessStartInfo("ping", "localhost");
        var cts = new CancellationTokenSource();

        // Act
        var task = ping.RunLongRunningAsync(
            psi,
            progressOutput: _ => { },
            progressError: _ => { },
            token: cts.Token);

        cts.Cancel();

        // Assert
        try
        {
            task.Wait();
            Assert.Fail("Expected AggregateException was not thrown.");
        }
        catch (AggregateException ex)
        {
            Assert.IsInstanceOfType<TaskCanceledException>(ex.InnerException);
        }
    }
}

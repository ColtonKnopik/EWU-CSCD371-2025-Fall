using System;
using Xunit;

namespace CanHazFunny.Tests;

public class JesterTests
{
    [Fact]
    public void Jester_CanBeCreatedWithValidDependencies()
    {
        // Arrange
        var output = new MockOutput();
        var jokeService = new MockJokeService();
        // Act
        var jester = new Jester(output, jokeService);
        // Assert
        Assert.IsType<Jester>(jester);
    }

    [Fact]
    public void Jester_ThrowsArgumentNullException_WhenOutputIsNull()
    {
        // Arrange
        IOutput? output = null;
        var jokeService = new MockJokeService();
        // Act & Assert
        Assert.Throws<ArgumentNullException>(() => new Jester(output, jokeService));
    }

    [Fact]
    public void Jester_ThrowsArgumentNullException_WhenJokeServiceIsNull()
    {
        // Arrange
        var output = new MockOutput();
        IJokeService? jokeService = null;
        // Act & Assert
        Assert.Throws<ArgumentNullException>(() => new Jester(output, jokeService));
    }

    [Fact]
    public void Jester_TellJoke_CallsOutputWriteLine()
    {
        // Arrange
        var output = new MockOutput();
        var jokeService = new MockJokeService();
        var jester = new Jester(output, jokeService);
        // Act
        jester.TellJoke();
        // Assert
        Assert.True(output.WriteLineCalled);
        Assert.Equal("This is a mock joke.", output.LastWrittenLine);
    }

    [Fact]
    public void Jester_TellJoke_DoesNotContainChuckNorris()
    {
        // Arrange
        var output = new MockOutput();
        var jokeService = new ChuckNorrisMockJokeService();
        var jester = new Jester(output, jokeService);
        // Act
        jester.TellJoke();
        // Assert
        Assert.True(output.WriteLineCalled);
        Assert.DoesNotContain("Chuck Norris", output.LastWrittenLine ?? string.Empty);
    }


}

// Mock implementation of IOutput for testing
public class MockOutput : IOutput
{
    public bool WriteLineCalled { get; private set; }
    public string? LastWrittenLine { get; private set; }

    public void WriteLine(string message)
    {
        WriteLineCalled = true;
        LastWrittenLine = message;
    }
}

// Mock implementation of IJokeService for testing
public class MockJokeService : IJokeService
{
    public string GetJoke()
    {
        return "This is a mock joke.";
    }
}

public class ChuckNorrisMockJokeService : IJokeService
{
    private int callCount = 0;

    public string GetJoke()
    {
        if (callCount == 0)
        {
            callCount++;
            return "This is a Chuck Norris joke.";
        }
        else
        {
            return "This is a regular joke.";
        }
    }
}
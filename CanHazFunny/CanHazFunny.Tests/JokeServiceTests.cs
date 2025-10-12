using System;
using Xunit;
namespace CanHazFunny.Tests;

public class JokeServiceTests
{
    [Fact]
    public void JokeService_GetJoke_ReturnsNonEmptyString()
    {
        // Arrange
        var jokeService = new JokeService();
        // Act
        string joke = jokeService.GetJoke();
        // Assert
        Assert.False(string.IsNullOrWhiteSpace(joke));
    }
}

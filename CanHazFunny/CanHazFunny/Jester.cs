using System;

namespace CanHazFunny;

public class Jester
{
    private IOutput Output { get; }
    private IJokeService JokeService { get; }

    public Jester(IOutput? output, IJokeService? jokeService)
    {
        Output = output ?? throw new ArgumentNullException(nameof(output));
        JokeService = jokeService ?? throw new ArgumentNullException(nameof(jokeService));
    }

    public void TellJoke()
    {
        const int MaxAttempts = 10;
        int attempt = 0;
        string joke = string.Empty;
        bool containsChuckNorrisJoke;

        do
        {
            joke = JokeService.GetJoke() ?? string.Empty;
            containsChuckNorrisJoke = joke.Contains("Chuck Norris", StringComparison.OrdinalIgnoreCase);
            attempt++;
        } while (containsChuckNorrisJoke && attempt < MaxAttempts);

        if (containsChuckNorrisJoke)
        {
            Output.Write("No appropriate joke could be retrieved.");
        }
        else
        {
            Output.Write(joke);
        }
    }
}


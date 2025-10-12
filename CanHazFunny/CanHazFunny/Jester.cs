using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

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
            string joke;
            bool containsChuckNorrisJoke;

            do
            {
                joke = JokeService.GetJoke();
                containsChuckNorrisJoke = joke.Contains("Chuck Norris", StringComparison.OrdinalIgnoreCase);

            } while (containsChuckNorrisJoke);
            
            Output.WriteLine(joke);
        }
    }
}

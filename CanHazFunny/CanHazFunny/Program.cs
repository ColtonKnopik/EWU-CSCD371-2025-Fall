namespace CanHazFunny;

sealed class Program
{
    static void Main(string[] args)
    {
        //Feel free to use your own setup here - this is just provided as an example
        //new Jester(new SomeReallyCoolOutputClass(), new SomeJokeServiceClass()).TellJoke();
        IOutput output = new ConsoleOutput();
        IJokeService jokeService = new JokeService();
        Jester jester = new Jester(ouput, jokeService);
        jester.TellJoke();
    }
}

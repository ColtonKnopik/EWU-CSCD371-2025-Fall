using System.Net.Http;
using System.Text.Json;

namespace CanHazFunny;

public class JokeService : IJokeService
{
    private HttpClient HttpClient { get; } = new();

    public string GetJoke()
    {
        string json = HttpClient.GetStringAsync("https://geek-jokes.sameerkumar.website/api?format=json").Result;
        var joke = JsonSerializer.Deserialize<JsonElement>(json);
        return joke.GetProperty("joke").GetString()!;
    }
}

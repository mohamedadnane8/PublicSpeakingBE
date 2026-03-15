namespace MyApp.Application.DTOs;

public class RandomWordRequest
{
    public string? Language { get; set; }
    public string? Difficulty { get; set; }
    public IReadOnlyCollection<string>? ExcludedWords { get; set; }
}

public class RandomWordResponse
{
    public string Word { get; set; } = null!;
    public string Language { get; set; } = null!;
    public string Difficulty { get; set; } = null!;
}

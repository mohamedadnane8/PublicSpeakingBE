namespace MyApp.Infrastructure.Configuration;

public class DeepgramOptions
{
    public const string SectionName = "Deepgram";

    public string ApiKey { get; set; } = string.Empty;
    public string BaseUrl { get; set; } = "https://api.deepgram.com/v1";
    public string Model { get; set; } = "nova-3";
}

namespace MyApp.Infrastructure.Configuration;

public class DeepSeekOptions
{
    public const string SectionName = "DeepSeek";

    public string ApiKey { get; set; } = string.Empty;
    public string BaseUrl { get; set; } = "https://api.deepseek.com";
}

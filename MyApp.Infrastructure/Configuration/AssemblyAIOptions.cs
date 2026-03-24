namespace MyApp.Infrastructure.Configuration;

public class AssemblyAIOptions
{
    public const string SectionName = "AssemblyAI";

    public string ApiKey { get; set; } = string.Empty;
    public string BaseUrl { get; set; } = "https://api.assemblyai.com/v2";
    public int PollingIntervalMs { get; set; } = 3000;
    public int MaxPollingAttempts { get; set; } = 200;
    public int PreSignedUrlExpiryMinutes { get; set; } = 60;
}

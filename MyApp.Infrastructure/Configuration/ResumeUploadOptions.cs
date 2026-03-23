namespace MyApp.Infrastructure.Configuration;

public class ResumeUploadOptions
{
    public const string SectionName = "ResumeUpload";

    public int UploadCooldownHours { get; set; } = 24;
}

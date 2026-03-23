namespace MyApp.Infrastructure.Configuration;

public class ResumeUploadOptions
{
    public const string SectionName = "ResumeUpload";

    public int UploadCooldownHours { get; set; } = 24;
    public int QuestionsPerBatch { get; set; } = 50;
    public bool EnableSecondBatch { get; set; } = true;
}

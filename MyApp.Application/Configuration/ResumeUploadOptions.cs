namespace MyApp.Application.Configuration;

public class ResumeUploadOptions
{
    public const string SectionName = "ResumeUpload";

    public int MaxUploadsPerWeek { get; set; } = 5;
    public int QuestionsPerBatch { get; set; } = 50;
    public bool EnableSecondBatch { get; set; } = true;
}

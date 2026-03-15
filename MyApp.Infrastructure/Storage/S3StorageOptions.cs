namespace MyApp.Infrastructure.Storage;

public class S3StorageOptions
{
    public const string SectionName = "S3Storage";

    public string BucketName { get; set; } = string.Empty;
    public string Region { get; set; } = string.Empty;
}

namespace MyApp.Application.DTOs;

public class AudioUploadResultDto
{
    public string ObjectKey { get; set; } = null!;
    public string BucketName { get; set; } = null!;
    public string Region { get; set; } = null!;
    public long FileSize { get; set; }
    public string ContentType { get; set; } = null!;
}

public class AudioPlaybackUrlDto
{
    public string Url { get; set; } = null!;
    public DateTime ExpiresAt { get; set; }
}

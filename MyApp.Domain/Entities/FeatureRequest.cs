namespace MyApp.Domain.Entities;

public class FeatureRequest
{
    public Guid Id { get; private set; }
    public Guid UserId { get; private set; }
    public string Message { get; private set; } = null!;
    public string? PageUrl { get; private set; }
    public DateTime CreatedAt { get; private set; }

    public User User { get; private set; } = null!;

    private FeatureRequest() { }

    public static FeatureRequest Create(Guid userId, string message, string? pageUrl)
    {
        if (userId == Guid.Empty)
            throw new ArgumentException("UserId is required", nameof(userId));

        var normalizedMessage = message?.Trim();
        if (string.IsNullOrWhiteSpace(normalizedMessage))
            throw new ArgumentException("Message is required", nameof(message));

        if (normalizedMessage.Length > 2000)
            throw new ArgumentException("Message cannot exceed 2000 characters", nameof(message));

        var normalizedPageUrl = string.IsNullOrWhiteSpace(pageUrl) ? null : pageUrl.Trim();
        if (normalizedPageUrl?.Length > 500)
            throw new ArgumentException("Page URL cannot exceed 500 characters", nameof(pageUrl));

        return new FeatureRequest
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            Message = normalizedMessage,
            PageUrl = normalizedPageUrl,
            CreatedAt = DateTime.UtcNow
        };
    }
}

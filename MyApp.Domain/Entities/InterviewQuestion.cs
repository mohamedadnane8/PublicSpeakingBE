using MyApp.Domain.Enums;

namespace MyApp.Domain.Entities;

public class InterviewQuestion
{
    public Guid Id { get; private set; }
    public Guid UserId { get; private set; }
    public string Question { get; private set; } = null!;
    public string Category { get; private set; } = null!;
    public QuestionDifficulty Difficulty { get; private set; }
    public int ThinkingSeconds { get; private set; }
    public int AnsweringSeconds { get; private set; }
    public DateTime CreatedAt { get; private set; }

    public User User { get; private set; } = null!;

    private InterviewQuestion() { }

    public static InterviewQuestion Create(
        Guid userId,
        string question,
        string category,
        QuestionDifficulty difficulty,
        int thinkingSeconds,
        int answeringSeconds)
    {
        if (userId == Guid.Empty)
            throw new ArgumentException("UserId is required", nameof(userId));

        var normalizedQuestion = question?.Trim();
        if (string.IsNullOrWhiteSpace(normalizedQuestion))
            throw new ArgumentException("Question is required", nameof(question));

        var normalizedCategory = category?.Trim();
        if (string.IsNullOrWhiteSpace(normalizedCategory))
            throw new ArgumentException("Category is required", nameof(category));

        if (thinkingSeconds <= 0)
            throw new ArgumentException("ThinkingSeconds must be positive", nameof(thinkingSeconds));

        if (answeringSeconds <= 0)
            throw new ArgumentException("AnsweringSeconds must be positive", nameof(answeringSeconds));

        return new InterviewQuestion
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            Question = normalizedQuestion,
            Category = normalizedCategory,
            Difficulty = difficulty,
            ThinkingSeconds = thinkingSeconds,
            AnsweringSeconds = answeringSeconds,
            CreatedAt = DateTime.UtcNow
        };
    }
}

using MyApp.Domain.Entities;
using MyApp.Domain.Enums;

namespace MyApp.Application.Interfaces;

public interface IInterviewQuestionRepository
{
    Task AddRangeAsync(IEnumerable<InterviewQuestion> questions, CancellationToken cancellationToken = default);
    Task<InterviewQuestion?> GetRandomByUserIdAsync(Guid userId, QuestionDifficulty? difficulty = null, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<InterviewQuestion>> GetByUserIdAsync(Guid userId, CancellationToken cancellationToken = default);
    Task<DateTime?> GetLatestUploadDateAsync(Guid userId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<string>> GetCategoriesByUserIdAsync(Guid userId, CancellationToken cancellationToken = default);
    Task DeleteByUserIdAsync(Guid userId, CancellationToken cancellationToken = default);
    Task SaveChangesAsync(CancellationToken cancellationToken = default);
}

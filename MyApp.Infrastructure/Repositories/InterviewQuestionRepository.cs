using Microsoft.EntityFrameworkCore;
using MyApp.Application.Interfaces;
using MyApp.Domain.Entities;
using MyApp.Domain.Enums;
using MyApp.Infrastructure.Data;

namespace MyApp.Infrastructure.Repositories;

public class InterviewQuestionRepository : IInterviewQuestionRepository
{
    private readonly ApplicationDbContext _context;

    public InterviewQuestionRepository(ApplicationDbContext context)
    {
        _context = context;
    }

    public async Task AddRangeAsync(IEnumerable<InterviewQuestion> questions, CancellationToken cancellationToken = default)
    {
        await _context.InterviewQuestions.AddRangeAsync(questions, cancellationToken);
    }

    public async Task<InterviewQuestion?> GetRandomByUserIdAsync(
        Guid userId,
        QuestionDifficulty? difficulty = null,
        CancellationToken cancellationToken = default)
    {
        var query = _context.InterviewQuestions
            .AsNoTracking()
            .Where(q => q.UserId == userId);

        if (difficulty.HasValue)
        {
            query = query.Where(q => q.Difficulty == difficulty.Value);
        }

        return await query
            .OrderBy(_ => EF.Functions.Random())
            .FirstOrDefaultAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<InterviewQuestion>> GetByUserIdAsync(
        Guid userId,
        CancellationToken cancellationToken = default)
    {
        return await _context.InterviewQuestions
            .AsNoTracking()
            .Where(q => q.UserId == userId)
            .OrderByDescending(q => q.CreatedAt)
            .ToListAsync(cancellationToken);
    }

    public async Task<DateTime?> GetLatestUploadDateAsync(
        Guid userId,
        CancellationToken cancellationToken = default)
    {
        return await _context.InterviewQuestions
            .Where(q => q.UserId == userId)
            .MaxAsync(q => (DateTime?)q.CreatedAt, cancellationToken);
    }

    public async Task<IReadOnlyList<string>> GetCategoriesByUserIdAsync(
        Guid userId,
        CancellationToken cancellationToken = default)
    {
        return await _context.InterviewQuestions
            .AsNoTracking()
            .Where(q => q.UserId == userId)
            .Select(q => q.Category)
            .Distinct()
            .OrderBy(c => c)
            .ToListAsync(cancellationToken);
    }

    public async Task DeleteByUserIdAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        await _context.InterviewQuestions
            .Where(q => q.UserId == userId)
            .ExecuteDeleteAsync(cancellationToken);
    }

    public async Task SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        await _context.SaveChangesAsync(cancellationToken);
    }
}

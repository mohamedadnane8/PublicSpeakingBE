using Microsoft.EntityFrameworkCore;
using MyApp.Application.Interfaces;
using MyApp.Domain.Entities;
using MyApp.Infrastructure.Data;

namespace MyApp.Infrastructure.Repositories;

public class FeatureRequestRepository : IFeatureRequestRepository
{
    private readonly ApplicationDbContext _context;

    public FeatureRequestRepository(ApplicationDbContext context)
    {
        _context = context;
    }

    public Task<FeatureRequest> AddAsync(FeatureRequest featureRequest, CancellationToken cancellationToken = default)
    {
        return Task.FromResult(_context.Set<FeatureRequest>().Add(featureRequest).Entity);
    }

    public async Task<IReadOnlyList<FeatureRequest>> GetByUserIdAsync(
        Guid userId,
        CancellationToken cancellationToken = default)
    {
        return await _context.Set<FeatureRequest>()
            .AsNoTracking()
            .Where(x => x.UserId == userId)
            .OrderByDescending(x => x.CreatedAt)
            .ToListAsync(cancellationToken);
    }

    public async Task SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        await _context.SaveChangesAsync(cancellationToken);
    }
}

using MyApp.Domain.Entities;

namespace MyApp.Application.Interfaces;

public interface IFeatureRequestRepository
{
    Task<FeatureRequest> AddAsync(FeatureRequest featureRequest, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<FeatureRequest>> GetByUserIdAsync(Guid userId, CancellationToken cancellationToken = default);
    Task SaveChangesAsync(CancellationToken cancellationToken = default);
}

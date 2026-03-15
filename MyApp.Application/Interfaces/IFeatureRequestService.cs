using MyApp.Application.DTOs;

namespace MyApp.Application.Interfaces;

public interface IFeatureRequestService
{
    Task<FeatureRequestDto> CreateAsync(
        Guid userId,
        CreateFeatureRequestRequest request,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<FeatureRequestDto>> GetForUserAsync(
        Guid userId,
        CancellationToken cancellationToken = default);
}

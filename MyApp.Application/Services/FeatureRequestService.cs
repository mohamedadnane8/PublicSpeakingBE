using MyApp.Application.DTOs;
using MyApp.Application.Interfaces;
using MyApp.Domain.Entities;

namespace MyApp.Application.Services;

public class FeatureRequestService : IFeatureRequestService
{
    private readonly IFeatureRequestRepository _featureRequestRepository;

    public FeatureRequestService(IFeatureRequestRepository featureRequestRepository)
    {
        _featureRequestRepository = featureRequestRepository;
    }

    public async Task<FeatureRequestDto> CreateAsync(
        Guid userId,
        CreateFeatureRequestRequest request,
        CancellationToken cancellationToken = default)
    {
        var featureRequest = FeatureRequest.Create(
            userId,
            request.Message,
            request.PageUrl);

        await _featureRequestRepository.AddAsync(featureRequest, cancellationToken);
        await _featureRequestRepository.SaveChangesAsync(cancellationToken);

        return Map(featureRequest);
    }

    public async Task<IReadOnlyList<FeatureRequestDto>> GetForUserAsync(
        Guid userId,
        CancellationToken cancellationToken = default)
    {
        var featureRequests = await _featureRequestRepository.GetByUserIdAsync(userId, cancellationToken);
        return featureRequests.Select(Map).ToList();
    }

    private static FeatureRequestDto Map(FeatureRequest featureRequest)
    {
        return new FeatureRequestDto
        {
            Id = featureRequest.Id,
            UserId = featureRequest.UserId,
            Message = featureRequest.Message,
            PageUrl = featureRequest.PageUrl,
            CreatedAt = featureRequest.CreatedAt
        };
    }
}

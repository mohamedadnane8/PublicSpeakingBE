using MyApp.Application.DTOs;

namespace MyApp.Application.Interfaces;

public interface ISessionService
{
    Task<SessionDto> CreateSessionAsync(Guid userId, CreateSessionRequest request, CancellationToken cancellationToken = default);
    Task<SessionDto?> GetSessionAsync(Guid sessionId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<SessionDto>> GetUserSessionsAsync(Guid userId, CancellationToken cancellationToken = default);
    Task<bool> DeleteSessionAsync(Guid sessionId, CancellationToken cancellationToken = default);
}

using MyApp.Domain.Entities;

namespace MyApp.Application.Interfaces;

/// <summary>
/// Repository for user authentication sessions (refresh token storage)
/// </summary>
public interface IUserSessionRepository
{
    /// <summary>
    /// Get session by ID
    /// </summary>
    Task<UserSession?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>
    /// Get session by refresh token hash
    /// </summary>
    Task<UserSession?> GetByRefreshTokenHashAsync(string refreshTokenHash, CancellationToken cancellationToken = default);

    /// <summary>
    /// Get all active sessions for a user
    /// </summary>
    Task<IReadOnlyList<UserSession>> GetActiveSessionsByUserIdAsync(Guid userId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Add a new session
    /// </summary>
    Task AddAsync(UserSession session, CancellationToken cancellationToken = default);

    /// <summary>
    /// Update an existing session
    /// </summary>
    Task UpdateAsync(UserSession session, CancellationToken cancellationToken = default);

    /// <summary>
    /// Revoke all sessions for a user
    /// </summary>
    Task RevokeAllUserSessionsAsync(Guid userId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Delete expired sessions (cleanup)
    /// </summary>
    Task<int> DeleteExpiredSessionsAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Save changes
    /// </summary>
    Task SaveChangesAsync(CancellationToken cancellationToken = default);
}

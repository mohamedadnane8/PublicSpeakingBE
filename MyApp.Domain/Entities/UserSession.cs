namespace MyApp.Domain.Entities;

/// <summary>
/// User session for stateful authentication (OAuth/refresh tokens)
/// </summary>
public class UserSession
{
    public Guid Id { get; private set; }
    public Guid UserId { get; private set; }
    public User User { get; private set; } = null!;
    
    /// <summary>
    /// Hashed refresh token (never store raw tokens)
    /// </summary>
    public string RefreshTokenHash { get; private set; } = null!;
    
    /// <summary>
    /// When this session was created
    /// </summary>
    public DateTime CreatedAt { get; private set; }
    
    /// <summary>
    /// When the refresh token expires
    /// </summary>
    public DateTime ExpiresAt { get; private set; }
    
    /// <summary>
    /// When the access token was last rotated
    /// </summary>
    public DateTime? LastRotatedAt { get; private set; }
    
    /// <summary>
    /// Whether this session has been revoked
    /// </summary>
    public bool IsRevoked { get; private set; }
    
    /// <summary>
    /// When this session was revoked (if applicable)
    /// </summary>
    public DateTime? RevokedAt { get; private set; }
    
    /// <summary>
    /// IP address that created this session
    /// </summary>
    public string? IpAddress { get; private set; }
    
    /// <summary>
    /// User agent that created this session
    /// </summary>
    public string? UserAgent { get; private set; }
    
    /// <summary>
    /// Number of times token has been refreshed (rotation count)
    /// </summary>
    public int RotationCount { get; private set; }

    private UserSession() { } // EF Core

    public static UserSession Create(
        Guid userId,
        string refreshTokenHash,
        DateTime expiresAt,
        string? ipAddress = null,
        string? userAgent = null)
    {
        if (string.IsNullOrWhiteSpace(refreshTokenHash))
            throw new ArgumentException("Refresh token hash is required", nameof(refreshTokenHash));

        var now = DateTime.UtcNow;

        return new UserSession
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            RefreshTokenHash = refreshTokenHash,
            CreatedAt = now,
            ExpiresAt = expiresAt,
            IpAddress = ipAddress,
            UserAgent = userAgent,
            RotationCount = 0,
            IsRevoked = false
        };
    }

    /// <summary>
    /// Rotate the refresh token (token rotation for security)
    /// </summary>
    public void RotateToken(string newRefreshTokenHash, DateTime newExpiresAt)
    {
        if (IsRevoked)
            throw new InvalidOperationException("Cannot rotate a revoked session");

        if (IsExpired)
            throw new InvalidOperationException("Cannot rotate an expired session");

        RefreshTokenHash = newRefreshTokenHash;
        ExpiresAt = newExpiresAt;
        LastRotatedAt = DateTime.UtcNow;
        RotationCount++;
    }

    /// <summary>
    /// Revoke this session (logout)
    /// </summary>
    public void Revoke()
    {
        if (IsRevoked) return;

        IsRevoked = true;
        RevokedAt = DateTime.UtcNow;
    }

    /// <summary>
    /// Check if the session has expired
    /// </summary>
    public bool IsExpired => DateTime.UtcNow > ExpiresAt;

    /// <summary>
    /// Check if this session is active and valid
    /// </summary>
    public bool IsActive => !IsRevoked && !IsExpired;

    /// <summary>
    /// Validate a refresh token against this session
    /// </summary>
    public bool ValidateRefreshToken(string refreshTokenHash)
    {
        return IsActive && RefreshTokenHash == refreshTokenHash;
    }
}

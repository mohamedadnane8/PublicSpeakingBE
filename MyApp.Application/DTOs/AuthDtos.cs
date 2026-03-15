namespace MyApp.Application.DTOs;

/// <summary>
/// Response from /auth/me endpoint
/// </summary>
public class UserInfoResponse
{
    public Guid Id { get; set; }
    public string Email { get; set; } = null!;
    public string FirstName { get; set; } = null!;
    public string LastName { get; set; } = null!;
    public string? ProfilePictureUrl { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime LastLoginAt { get; set; }
}

/// <summary>
/// OAuth state stored temporarily during Google auth flow
/// </summary>
public class OAuthState
{
    public string State { get; set; } = null!;
    public string CodeVerifier { get; set; } = null!;
    public string? RedirectUri { get; set; }
    public DateTime ExpiresAt { get; set; }
}

/// <summary>
/// Result of successful authentication
/// </summary>
public class AuthenticationResult
{
    public string AccessToken { get; set; } = null!;
    public string RefreshToken { get; set; } = null!;
    public Guid SessionId { get; set; }
    public DateTime AccessTokenExpiresAt { get; set; }
    public DateTime RefreshTokenExpiresAt { get; set; }
    public UserInfoResponse User { get; set; } = null!;
}

/// <summary>
/// Google token exchange response (internal use)
/// </summary>
public class GoogleTokenResponse
{
    public string AccessToken { get; set; } = null!;
    public string IdToken { get; set; } = null!;
    public string? RefreshToken { get; set; }
    public int ExpiresIn { get; set; }
}

/// <summary>
/// Refresh token request (empty - tokens come from cookies)
/// </summary>
public class RefreshTokenRequest
{
    // Empty - refresh token is in HttpOnly cookie
}

/// <summary>
/// Logout request (optional)
/// </summary>
public class LogoutRequest
{
    /// <summary>
    /// If true, revoke all sessions for this user across all devices
    /// </summary>
    public bool RevokeAllSessions { get; set; }
}

/// <summary>
/// Success/error response for auth operations
/// </summary>
public class AuthOperationResponse
{
    public bool Success { get; set; }
    public string? Message { get; set; }
    public string? RedirectUri { get; set; }
}

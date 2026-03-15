using System.Security.Claims;
using MyApp.Domain.Entities;

namespace MyApp.Application.Interfaces;

/// <summary>
/// Cookie options for token storage
/// </summary>
public class TokenCookieOptions
{
    public bool HttpOnly { get; set; }
    public bool Secure { get; set; }
    public string SameSite { get; set; } = "Strict";
    public string Path { get; set; } = "/";
    public int MaxAgeMinutes { get; set; }
}

/// <summary>
/// Service for generating and validating JWT access tokens and refresh tokens
/// </summary>
public interface ITokenService
{
    // ========== Access Token Methods ==========

    /// <summary>
    /// Generate a JWT access token for a user
    /// </summary>
    /// <param name="user">The authenticated user</param>
    /// <param name="sessionId">The session ID</param>
    /// <returns>JWT string</returns>
    string GenerateAccessToken(User user, Guid sessionId);

    /// <summary>
    /// Validate a JWT access token and return the principal
    /// </summary>
    /// <param name="accessToken">The JWT to validate</param>
    /// <returns>ClaimsPrincipal if valid, null if invalid</returns>
    ClaimsPrincipal? ValidateAccessToken(string accessToken);

    /// <summary>
    /// Extract user ID from access token without full validation
    /// </summary>
    Guid? GetUserIdFromToken(string accessToken);

    /// <summary>
    /// Extract session ID from access token
    /// </summary>
    Guid? GetSessionIdFromToken(string accessToken);

    /// <summary>
    /// Get the configured access token expiration time
    /// </summary>
    TimeSpan AccessTokenLifetime { get; }

    // ========== Refresh Token Methods ==========

    /// <summary>
    /// Generate a cryptographically secure opaque refresh token
    /// </summary>
    /// <returns>Base64-encoded random token</returns>
    string GenerateRefreshToken();

    /// <summary>
    /// Hash a refresh token for secure storage
    /// </summary>
    /// <param name="refreshToken">The raw refresh token</param>
    /// <returns>SHA-256 hash as base64 string</returns>
    string HashRefreshToken(string refreshToken);

    /// <summary>
    /// Verify a refresh token against its hash
    /// </summary>
    bool VerifyRefreshToken(string refreshToken, string hash);

    /// <summary>
    /// Get the configured refresh token expiration time
    /// </summary>
    TimeSpan RefreshTokenLifetime { get; }

    // ========== Cookie Configuration ==========

    /// <summary>
    /// Get cookie options for the access token cookie
    /// </summary>
    TokenCookieOptions GetAccessTokenCookieOptions();

    /// <summary>
    /// Get cookie options for the refresh token cookie
    /// </summary>
    TokenCookieOptions GetRefreshTokenCookieOptions();

    /// <summary>
    /// Get cookie options for the session ID cookie
    /// </summary>
    TokenCookieOptions GetSessionIdCookieOptions();

    /// <summary>
    /// Get cookie options for OAuth state cookies (PKCE)
    /// </summary>
    TokenCookieOptions GetOAuthStateCookieOptions();

    /// <summary>
    /// Get cookie options for deleting/clearing cookies
    /// </summary>
    TokenCookieOptions GetDeleteCookieOptions(string path = "/");
}

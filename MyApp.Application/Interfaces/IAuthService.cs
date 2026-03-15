using MyApp.Application.DTOs;

namespace MyApp.Application.Interfaces;

/// <summary>
/// Main authentication service for OAuth 2.0 + Cookie-based auth
/// </summary>
public interface IAuthService
{
    /// <summary>
    /// Generate OAuth state and PKCE parameters for Google login
    /// </summary>
    /// <param name="redirectUri">Optional frontend redirect URI after auth</param>
    /// <returns>OAuth state containing state parameter and code verifier</returns>
    OAuthState GenerateOAuthState(string? redirectUri = null);

    /// <summary>
    /// Build the Google OAuth authorization URL
    /// </summary>
    /// <param name="state">The state parameter (CSRF protection)</param>
    /// <param name="codeChallenge">PKCE code challenge</param>
    string BuildGoogleAuthorizationUrl(string state, string codeChallenge);

    /// <summary>
    /// Handle OAuth callback from Google
    /// </summary>
    /// <param name="code">Authorization code from Google</param>
    /// <param name="state">State parameter from callback</param>
    /// <param name="storedState">Original state stored in cookie</param>
    /// <param name="codeVerifier">PKCE code verifier from cookie</param>
    /// <param name="ipAddress">Client IP address</param>
    /// <param name="userAgent">Client user agent</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Authentication result with tokens and user info</returns>
    Task<AuthenticationResult> HandleGoogleCallbackAsync(
        string code,
        string state,
        string storedState,
        string codeVerifier,
        string? ipAddress,
        string? userAgent,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Get current user from access token
    /// </summary>
    /// <param name="accessToken">JWT access token</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>User info if token is valid</returns>
    Task<UserInfoResponse?> GetCurrentUserAsync(
        string accessToken,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Refresh access token using refresh token
    /// </summary>
    /// <param name="refreshToken">The opaque refresh token</param>
    /// <param name="sessionId">Session ID</param>
    /// <param name="ipAddress">Client IP address</param>
    /// <param name="userAgent">Client user agent</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>New authentication result with rotated tokens</returns>
    Task<AuthenticationResult> RefreshTokenAsync(
        string refreshToken,
        Guid sessionId,
        string? ipAddress,
        string? userAgent,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Logout user and revoke session
    /// </summary>
    /// <param name="sessionId">Session ID to revoke</param>
    /// <param name="revokeAllSessions">If true, revoke all sessions for this user</param>
    /// <param name="cancellationToken">Cancellation token</param>
    Task LogoutAsync(
        Guid sessionId,
        bool revokeAllSessions = false,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Validate OAuth state parameter (CSRF protection)
    /// </summary>
    /// <param name="state">State from callback</param>
    /// <param name="storedState">State from cookie</param>
    /// <param name="storedExpiresAt">When the state expires</param>
    bool ValidateOAuthState(string state, string storedState, DateTime storedExpiresAt);
}

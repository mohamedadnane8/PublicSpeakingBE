using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using MyApp.Application.DTOs;
using MyApp.Application.Interfaces;
using MyApp.Domain.Exceptions;

namespace MyApp.API.Controllers;

/// <summary>
/// OAuth 2.0 Authentication Controller with HttpOnly Cookie-based auth
/// </summary>
[ApiController]
[Route("api/auth")]
public class AuthController : ControllerBase
{
    private readonly IAuthService _authService;
    private readonly ITokenService _tokenService;
    private readonly ILogger<AuthController> _logger;
    private readonly FrontendOptions _frontendOptions;

    // Cookie names
    private const string AccessTokenCookie = "access_token";
    private const string RefreshTokenCookie = "refresh_token";
    private const string SessionIdCookie = "session_id";
    private const string OAuthStateCookie = "oauth_state";
    private const string OAuthCodeVerifierCookie = "oauth_code_verifier";
    private const string OAuthRedirectUriCookie = "oauth_redirect_uri";

    public AuthController(
        IAuthService authService,
        ITokenService tokenService,
        ILogger<AuthController> logger,
        IOptions<FrontendOptions> frontendOptions)
    {
        _authService = authService;
        _tokenService = tokenService;
        _logger = logger;
        _frontendOptions = frontendOptions.Value;
    }

    /// <summary>
    /// Initiate Google OAuth login flow
    /// </summary>
    /// <param name="redirectUri">Optional: Where to redirect after successful auth</param>
    /// <returns>302 redirect to Google</returns>
    [HttpGet("google/login")]
    [ProducesResponseType(StatusCodes.Status302Found)]
    public IActionResult GoogleLogin([FromQuery] string? redirectUri = null)
    {
        // Validate redirectUri if provided (prevent open redirect)
        if (!string.IsNullOrEmpty(redirectUri) && !IsValidRedirectUri(redirectUri))
        {
            _logger.LogWarning("Invalid redirect URI attempted: {RedirectUri}", redirectUri);
            redirectUri = null;  // Fall back to default
        }

        // Generate OAuth state with PKCE
        var oauthState = _authService.GenerateOAuthState(redirectUri);

        // Compute PKCE code challenge (S256)
        var codeChallenge = ComputeCodeChallenge(oauthState.CodeVerifier);

        // Store state in HttpOnly cookie (CSRF protection)
        Response.Cookies.Append(OAuthStateCookie, oauthState.State, 
            ToCookieOptions(_tokenService.GetOAuthStateCookieOptions()));
        Response.Cookies.Append(OAuthCodeVerifierCookie, oauthState.CodeVerifier,
            ToCookieOptions(_tokenService.GetOAuthStateCookieOptions()));
        
        if (!string.IsNullOrEmpty(redirectUri))
        {
            Response.Cookies.Append(OAuthRedirectUriCookie, redirectUri,
                ToCookieOptions(_tokenService.GetOAuthStateCookieOptions()));
        }

        // Build Google authorization URL
        var authorizationUrl = _authService.BuildGoogleAuthorizationUrl(oauthState.State, codeChallenge);

        _logger.LogInformation("Initiating Google OAuth flow, state: {State}", oauthState.State[..8] + "...");

        return Redirect(authorizationUrl);
    }

    /// <summary>
    /// Handle OAuth callback from Google
    /// </summary>
    /// <param name="code">Authorization code from Google</param>
    /// <param name="state">State parameter for CSRF validation</param>
    /// <param name="error">Error from Google (if any)</param>
    /// <param name="errorDescription">Error description from Google</param>
    [HttpGet("google/callback")]
    [ProducesResponseType(StatusCodes.Status302Found)]
    [ApiExplorerSettings(IgnoreApi = true)]  // Hide from Swagger
    public async Task<IActionResult> GoogleCallback(
        [FromQuery] string? code,
        [FromQuery] string? state,
        [FromQuery] string? error,
        [FromQuery(Name = "error_description")] string? errorDescription,
        CancellationToken cancellationToken)
    {
        // Handle OAuth errors from Google
        if (!string.IsNullOrEmpty(error))
        {
            _logger.LogWarning("Google OAuth error: {Error} - {Description}", error, errorDescription);
            ClearOAuthStateCookies();
            return RedirectToFrontendError($"google_{error}", errorDescription);
        }

        // Validate required parameters
        if (string.IsNullOrEmpty(code) || string.IsNullOrEmpty(state))
        {
            _logger.LogWarning("Missing code or state in OAuth callback");
            ClearOAuthStateCookies();
            return RedirectToFrontendError("invalid_request", "Missing required parameters");
        }

        // Retrieve and validate state from cookie
        var storedState = Request.Cookies[OAuthStateCookie];
        var codeVerifier = Request.Cookies[OAuthCodeVerifierCookie];
        var redirectUri = Request.Cookies[OAuthRedirectUriCookie];

        if (string.IsNullOrEmpty(storedState) || string.IsNullOrEmpty(codeVerifier))
        {
            _logger.LogWarning("Missing OAuth state cookies - possible CSRF attack or expired session");
            ClearOAuthStateCookies();
            return RedirectToFrontendError("invalid_state", "Session expired. Please try again.");
        }

        // Validate state parameter (CSRF protection)
        var stateExpiresAt = DateTime.UtcNow.AddMinutes(10);  // Max age from cookie
        if (!_authService.ValidateOAuthState(state, storedState, stateExpiresAt))
        {
            _logger.LogWarning("Invalid OAuth state - possible CSRF attack");
            ClearOAuthStateCookies();
            return RedirectToFrontendError("invalid_state", "Invalid session state. Please try again.");
        }

        try
        {
            // Get client info for session
            var ipAddress = HttpContext.Connection.RemoteIpAddress?.ToString();
            var userAgent = Request.Headers.UserAgent.ToString();

            // Exchange code for tokens and create session
            var authResult = await _authService.HandleGoogleCallbackAsync(
                code, state, storedState, codeVerifier, ipAddress, userAgent, cancellationToken);

            // Clear OAuth state cookies
            ClearOAuthStateCookies();

            // Set authentication cookies (HttpOnly, Secure, SameSite)
            SetAuthenticationCookies(authResult);

            _logger.LogInformation("User authenticated successfully: {Email}, Session: {SessionId}",
                authResult.User.Email, authResult.SessionId);

            // Redirect to frontend with success
            return RedirectToFrontendSuccess(redirectUri);
        }
        catch (InvalidGoogleTokenException ex)
        {
            _logger.LogWarning(ex, "Google token validation failed");
            ClearOAuthStateCookies();
            return RedirectToFrontendError("token_validation_failed", ex.Message);
        }
        catch (DomainException ex)
        {
            _logger.LogWarning(ex, "Domain error during OAuth callback");
            ClearOAuthStateCookies();
            return RedirectToFrontendError("authentication_failed", ex.Message);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error during OAuth callback");
            ClearOAuthStateCookies();
            return RedirectToFrontendError("server_error", "An unexpected error occurred");
        }
    }

    /// <summary>
    /// Get current authenticated user information
    /// </summary>
    [HttpGet("me")]
    [Authorize]
    [ProducesResponseType(typeof(UserInfoResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<UserInfoResponse>> GetCurrentUser(
        CancellationToken cancellationToken)
    {
        var accessToken = Request.Cookies[AccessTokenCookie];
        
        _logger.LogDebug("GetCurrentUser called. Cookie present: {HasCookie}", 
            !string.IsNullOrEmpty(accessToken));

        if (string.IsNullOrEmpty(accessToken))
        {
            _logger.LogWarning("GetCurrentUser 401: access_token cookie is missing. " +
                "Cookies received: {Cookies}", 
                string.Join(", ", Request.Cookies.Keys));
            
            return Unauthorized(new { 
                error = "missing_token", 
                message = "Access token not found in cookies",
                debug = new {
                    cookiesReceived = Request.Cookies.Keys.ToList(),
                    hint = "Make sure your frontend sends cookies with 'credentials: include'"
                }
            });
        }

        _logger.LogDebug("GetCurrentUser: Validating token...");
        var user = await _authService.GetCurrentUserAsync(accessToken, cancellationToken);
        
        if (user == null)
        {
            _logger.LogWarning("GetCurrentUser 401: Token validation failed or user not found. " +
                "Token starts with: {TokenPrefix}", 
                accessToken[..Math.Min(20, accessToken.Length)] + "...");
            
            return Unauthorized(new { 
                error = "invalid_token", 
                message = "Invalid or expired token",
                debug = new {
                    tokenPresent = true,
                    tokenLength = accessToken.Length,
                    suggestion = "Token may be expired. Try calling /api/auth/refresh"
                }
            });
        }

        _logger.LogDebug("GetCurrentUser: User authenticated - {Email}", user.Email);
        return Ok(user);
    }

    /// <summary>
    /// Refresh access token using refresh token cookie
    /// </summary>
    [HttpPost("refresh")]
    [ProducesResponseType(typeof(AuthOperationResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(AuthOperationResponse), StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<AuthOperationResponse>> RefreshToken(
        CancellationToken cancellationToken)
    {
        var refreshToken = Request.Cookies[RefreshTokenCookie];
        var sessionIdCookie = Request.Cookies[SessionIdCookie];

        _logger.LogDebug("RefreshToken called. refresh_token present: {HasRefresh}, session_id present: {HasSession}",
            !string.IsNullOrEmpty(refreshToken), !string.IsNullOrEmpty(sessionIdCookie));

        if (string.IsNullOrEmpty(refreshToken) || string.IsNullOrEmpty(sessionIdCookie))
        {
            _logger.LogWarning("RefreshToken 401: Missing cookies. " +
                "refresh_token: {HasRefresh}, session_id: {HasSession}. " +
                "All cookies: {Cookies}",
                !string.IsNullOrEmpty(refreshToken), 
                !string.IsNullOrEmpty(sessionIdCookie),
                string.Join(", ", Request.Cookies.Keys));
            
            ClearAuthenticationCookies();
            return Unauthorized(new AuthOperationResponse
            {
                Success = false,
                Message = "Refresh token not found",
                DebugInfo = new {
                    refreshTokenPresent = !string.IsNullOrEmpty(refreshToken),
                    sessionIdPresent = !string.IsNullOrEmpty(sessionIdCookie),
                    hint = "Make sure your frontend sends cookies with 'credentials: include'"
                }
            });
        }

        if (!Guid.TryParse(sessionIdCookie, out var sessionId))
        {
            _logger.LogWarning("RefreshToken 401: Invalid session_id format: {SessionId}", 
                sessionIdCookie[..Math.Min(20, sessionIdCookie.Length)]);
            
            ClearAuthenticationCookies();
            return Unauthorized(new AuthOperationResponse
            {
                Success = false,
                Message = "Invalid session"
            });
        }

        try
        {
            var ipAddress = HttpContext.Connection.RemoteIpAddress?.ToString();
            var userAgent = Request.Headers.UserAgent.ToString();

            var authResult = await _authService.RefreshTokenAsync(
                refreshToken, sessionId, ipAddress, userAgent, cancellationToken);

            // Update cookies with new tokens
            SetAuthenticationCookies(authResult);

            _logger.LogDebug("Token refreshed for session: {SessionId}", sessionId);

            return Ok(new AuthOperationResponse
            {
                Success = true,
                Message = "Token refreshed successfully"
            });
        }
        catch (DomainException ex)
        {
            _logger.LogWarning(ex, "Token refresh failed for session: {SessionId}", sessionId);
            ClearAuthenticationCookies();
            return Unauthorized(new AuthOperationResponse
            {
                Success = false,
                Message = ex.Message
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error during token refresh");
            ClearAuthenticationCookies();
            return Unauthorized(new AuthOperationResponse
            {
                Success = false,
                Message = "Session expired. Please log in again."
            });
        }
    }

    /// <summary>
    /// Logout user and revoke session
    /// </summary>
    [HttpPost("logout")]
    [ProducesResponseType(typeof(AuthOperationResponse), StatusCodes.Status200OK)]
    public async Task<ActionResult<AuthOperationResponse>> Logout(
        [FromBody] LogoutRequest? request,
        CancellationToken cancellationToken)
    {
        var sessionIdCookie = Request.Cookies[SessionIdCookie];
        
        if (!string.IsNullOrEmpty(sessionIdCookie) && Guid.TryParse(sessionIdCookie, out var sessionId))
        {
            try
            {
                await _authService.LogoutAsync(sessionId, request?.RevokeAllSessions ?? false, cancellationToken);
                _logger.LogInformation("User logged out, session: {SessionId}, revokeAll: {RevokeAll}",
                    sessionId, request?.RevokeAllSessions ?? false);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Error during logout for session: {SessionId}", sessionId);
                // Continue to clear cookies even if DB operation fails
            }
        }

        ClearAuthenticationCookies();

        return Ok(new AuthOperationResponse
        {
            Success = true,
            Message = "Logged out successfully"
        });
    }

    // ========== Private Helper Methods ==========

    private void SetAuthenticationCookies(AuthenticationResult authResult)
    {
        Response.Cookies.Append(AccessTokenCookie, authResult.AccessToken,
            ToCookieOptions(_tokenService.GetAccessTokenCookieOptions()));
        Response.Cookies.Append(RefreshTokenCookie, authResult.RefreshToken,
            ToCookieOptions(_tokenService.GetRefreshTokenCookieOptions()));
        Response.Cookies.Append(SessionIdCookie, authResult.SessionId.ToString(),
            ToCookieOptions(_tokenService.GetSessionIdCookieOptions()));
    }

    private void ClearAuthenticationCookies()
    {
        Response.Cookies.Delete(AccessTokenCookie, ToCookieOptions(_tokenService.GetDeleteCookieOptions("/api")));
        Response.Cookies.Delete(RefreshTokenCookie, ToCookieOptions(_tokenService.GetDeleteCookieOptions("/api/auth/refresh")));
        Response.Cookies.Delete(SessionIdCookie, ToCookieOptions(_tokenService.GetDeleteCookieOptions("/")));
    }

    private void ClearOAuthStateCookies()
    {
        Response.Cookies.Delete(OAuthStateCookie, ToCookieOptions(_tokenService.GetDeleteCookieOptions("/api/auth")));
        Response.Cookies.Delete(OAuthCodeVerifierCookie, ToCookieOptions(_tokenService.GetDeleteCookieOptions("/api/auth")));
        Response.Cookies.Delete(OAuthRedirectUriCookie, ToCookieOptions(_tokenService.GetDeleteCookieOptions("/api/auth")));
    }

    private static string ComputeCodeChallenge(string codeVerifier)
    {
        using var sha256 = SHA256.Create();
        var hash = sha256.ComputeHash(Encoding.ASCII.GetBytes(codeVerifier));
        return Convert.ToBase64String(hash)
            .Replace("+", "-")
            .Replace("/", "_")
            .Replace("=", "");
    }

    private bool IsValidRedirectUri(string redirectUri)
    {
        // Only allow redirect to configured frontend URLs
        // This prevents open redirect vulnerabilities
        if (string.IsNullOrEmpty(redirectUri))
            return false;

        // Must start with allowed frontend URL
        var allowedBaseUrl = _frontendOptions.BaseUrl?.TrimEnd('/');
        if (string.IsNullOrEmpty(allowedBaseUrl))
            return false;

        // Check if it's a relative URL (starts with /)
        if (redirectUri.StartsWith("/") && !redirectUri.StartsWith("//"))
            return true;

        // Check if it's within the allowed domain
        return redirectUri.StartsWith(allowedBaseUrl, StringComparison.OrdinalIgnoreCase);
    }

    private RedirectResult RedirectToFrontendSuccess(string? customRedirectUri)
    {
        var redirectUrl = !string.IsNullOrEmpty(customRedirectUri)
            ? customRedirectUri
            : $"{_frontendOptions.BaseUrl}/auth/success";

        return Redirect(redirectUrl);
    }

    private RedirectResult RedirectToFrontendError(string error, string? message)
    {
        var encodedError = Uri.EscapeDataString(error);
        var encodedMessage = Uri.EscapeDataString(message ?? "Authentication failed");
        var redirectUrl = $"{_frontendOptions.BaseUrl}/auth/error?error={encodedError}&message={encodedMessage}";

        return Redirect(redirectUrl);
    }

    private static CookieOptions ToCookieOptions(TokenCookieOptions options)
    {
        return new CookieOptions
        {
            HttpOnly = options.HttpOnly,
            Secure = options.Secure,
            SameSite = Enum.Parse<SameSiteMode>(options.SameSite),
            Path = options.Path,
            MaxAge = options.MaxAgeMinutes > 0 ? TimeSpan.FromMinutes(options.MaxAgeMinutes) : TimeSpan.Zero,
            IsEssential = true
        };
    }
}

/// <summary>
/// Configuration options for frontend application
/// </summary>
public class FrontendOptions
{
    public string BaseUrl { get; set; } = null!;
}

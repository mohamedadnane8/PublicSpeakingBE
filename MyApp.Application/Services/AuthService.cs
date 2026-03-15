using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Configuration;
using MyApp.Application.DTOs;
using MyApp.Application.Interfaces;
using MyApp.Domain.Entities;
using MyApp.Domain.Exceptions;

namespace MyApp.Application.Services;

public class AuthService : IAuthService
{
    private readonly IUserRepository _userRepository;
    private readonly IUserSessionRepository _sessionRepository;
    private readonly IGoogleTokenValidator _googleTokenValidator;
    private readonly ITokenService _tokenService;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly string _googleClientId;
    private readonly string _googleClientSecret;
    private readonly string _googleRedirectUri;

    public AuthService(
        IUserRepository userRepository,
        IUserSessionRepository sessionRepository,
        IGoogleTokenValidator googleTokenValidator,
        ITokenService tokenService,
        IHttpClientFactory httpClientFactory,
        IConfiguration configuration)
    {
        _userRepository = userRepository;
        _sessionRepository = sessionRepository;
        _googleTokenValidator = googleTokenValidator;
        _tokenService = tokenService;
        _httpClientFactory = httpClientFactory;

        var googleSettings = configuration.GetSection("GoogleOAuth");
        _googleClientId = googleSettings["ClientId"]!;
        _googleClientSecret = googleSettings["ClientSecret"]!;
        _googleRedirectUri = googleSettings["RedirectUri"]!;
    }

    public OAuthState GenerateOAuthState(string? redirectUri = null)
    {
        // Generate state parameter (CSRF protection)
        var stateBytes = new byte[32];
        using (var rng = RandomNumberGenerator.Create())
        {
            rng.GetBytes(stateBytes);
        }
        var state = Convert.ToBase64String(stateBytes)
            .Replace("+", "-")
            .Replace("/", "_")
            .Replace("=", "");

        // Generate PKCE code verifier
        var codeVerifierBytes = new byte[32];
        using (var rng = RandomNumberGenerator.Create())
        {
            rng.GetBytes(codeVerifierBytes);
        }
        var codeVerifier = Convert.ToBase64String(codeVerifierBytes)
            .Replace("+", "-")
            .Replace("/", "_")
            .Replace("=", "");

        return new OAuthState
        {
            State = state,
            CodeVerifier = codeVerifier,
            RedirectUri = redirectUri,
            ExpiresAt = DateTime.UtcNow.AddMinutes(10)
        };
    }

    public string BuildGoogleAuthorizationUrl(string state, string codeChallenge)
    {
        var scope = Uri.EscapeDataString("openid email profile");
        var encodedState = Uri.EscapeDataString(state);
        var encodedRedirectUri = Uri.EscapeDataString(_googleRedirectUri);
        var encodedCodeChallenge = Uri.EscapeDataString(codeChallenge);

        return $"https://accounts.google.com/o/oauth2/v2/auth?" +
               $"client_id={_googleClientId}" +
               $"&redirect_uri={encodedRedirectUri}" +
               $"&response_type=code" +
               $"&scope={scope}" +
               $"&state={encodedState}" +
               $"&code_challenge={encodedCodeChallenge}" +
               $"&code_challenge_method=S256" +
               $"&prompt=consent" +  // Always show consent screen for refresh token
               $"&access_type=offline";  // Request refresh token
    }

    public bool ValidateOAuthState(string state, string storedState, DateTime storedExpiresAt)
    {
        if (string.IsNullOrEmpty(state) || string.IsNullOrEmpty(storedState))
            return false;

        if (DateTime.UtcNow > storedExpiresAt)
            return false;

        // Constant-time comparison to prevent timing attacks
        return CryptographicOperations.FixedTimeEquals(
            Encoding.UTF8.GetBytes(state),
            Encoding.UTF8.GetBytes(storedState));
    }

    public async Task<AuthenticationResult> HandleGoogleCallbackAsync(
        string code,
        string state,
        string storedState,
        string codeVerifier,
        string? ipAddress,
        string? userAgent,
        CancellationToken cancellationToken = default)
    {
        // Note: State expiration should be checked by the caller using ValidateOAuthState
        
        // Step 1: Exchange authorization code for tokens
        var googleTokens = await ExchangeCodeForTokensAsync(code, codeVerifier, cancellationToken);

        // Step 2: Validate the ID token and get user info
        var googleUserInfo = await _googleTokenValidator.ValidateAsync(googleTokens.IdToken, cancellationToken);

        if (!googleUserInfo.EmailVerified)
        {
            throw new InvalidGoogleTokenException("Email not verified with Google");
        }

        // Step 3: Safely extract name fields from Google profile
        // Google may not provide all name fields depending on user's privacy settings
        var (email, firstName, lastName, googleId, profilePicture) = ExtractGoogleProfileSafely(googleUserInfo);

        // Step 4: Find or create user
        var user = await _userRepository.GetByGoogleIdAsync(googleId, cancellationToken)
            ?? await _userRepository.GetByEmailAsync(email, cancellationToken);

        if (user == null)
        {
            // Create new user from OAuth data
            // CreateFromOAuth handles null/empty lastName gracefully
            user = User.CreateFromOAuth(
                email,
                firstName,
                lastName,  // May be null
                googleId,
                profilePicture);

            await _userRepository.AddAsync(user, cancellationToken);
        }
        else
        {
            // Update existing user profile
            // Only update fields that have values from Google
            user.UpdateProfile(
                firstName,
                lastName,
                profilePicture);
        }

        // Step 5: Record login
        user.RecordLogin();
        await _userRepository.SaveChangesAsync(cancellationToken);

        // Step 6: Create session with refresh token
        var refreshToken = _tokenService.GenerateRefreshToken();
        var refreshTokenHash = _tokenService.HashRefreshToken(refreshToken);
        var sessionExpiresAt = DateTime.UtcNow.Add(_tokenService.RefreshTokenLifetime);

        var session = UserSession.Create(
            user.Id,
            refreshTokenHash,
            sessionExpiresAt,
            ipAddress,
            userAgent);

        await _sessionRepository.AddAsync(session, cancellationToken);
        await _sessionRepository.SaveChangesAsync(cancellationToken);

        // Step 7: Generate access token
        var accessToken = _tokenService.GenerateAccessToken(user, session.Id);

        return new AuthenticationResult
        {
            AccessToken = accessToken,
            RefreshToken = refreshToken,
            SessionId = session.Id,
            AccessTokenExpiresAt = DateTime.UtcNow.Add(_tokenService.AccessTokenLifetime),
            RefreshTokenExpiresAt = sessionExpiresAt,
            User = MapToUserInfoResponse(user)
        };
    }

    /// <summary>
    /// Safely extracts Google profile data with appropriate fallbacks.
    /// Handles cases where Google may not provide certain fields.
    /// </summary>
    private static (string email, string firstName, string? lastName, string googleId, string? profilePicture) 
        ExtractGoogleProfileSafely(GoogleUserInfo googleUserInfo)
    {
        // Email is always required
        var email = googleUserInfo.Email;
        
        // Google may not provide separate first/last name fields
        // Use the best available name data
        var firstName = !string.IsNullOrWhiteSpace(googleUserInfo.FirstName) 
            ? googleUserInfo.FirstName 
            : googleUserInfo.GetBestAvailableName();  // Fallback to full name or "User"
        
        // Last name is optional - may be null
        var lastName = googleUserInfo.LastName;
        
        // Google ID is always required
        var googleId = googleUserInfo.GoogleId;
        
        // Profile picture is optional
        var profilePicture = googleUserInfo.ProfilePictureUrl;

        return (email, firstName, lastName, googleId, profilePicture);
    }

    public async Task<UserInfoResponse?> GetCurrentUserAsync(
        string accessToken,
        CancellationToken cancellationToken = default)
    {
        // Validate token
        var principal = _tokenService.ValidateAccessToken(accessToken);
        if (principal == null)
            return null;

        // Get user ID from token
        var userId = _tokenService.GetUserIdFromToken(accessToken);
        if (!userId.HasValue)
            return null;

        // Get user from database
        var user = await _userRepository.GetByIdAsync(userId.Value, cancellationToken);
        if (user == null)
            return null;

        return MapToUserInfoResponse(user);
    }

    public async Task<AuthenticationResult> RefreshTokenAsync(
        string refreshToken,
        Guid sessionId,
        string? ipAddress,
        string? userAgent,
        CancellationToken cancellationToken = default)
    {
        // Step 1: Get session from database
        var session = await _sessionRepository.GetByIdAsync(sessionId, cancellationToken);
        if (session == null)
        {
            throw new DomainException("Invalid session");
        }

        // Step 2: Verify refresh token
        if (!session.ValidateRefreshToken(_tokenService.HashRefreshToken(refreshToken)))
        {
            // Potential token reuse attack - revoke all sessions for this user
            await _sessionRepository.RevokeAllUserSessionsAsync(session.UserId, cancellationToken);
            throw new DomainException("Invalid refresh token. All sessions have been revoked for security.");
        }

        // Step 3: Rotate refresh token (security best practice)
        var newRefreshToken = _tokenService.GenerateRefreshToken();
        var newRefreshTokenHash = _tokenService.HashRefreshToken(newRefreshToken);
        var newExpiresAt = DateTime.UtcNow.Add(_tokenService.RefreshTokenLifetime);

        session.RotateToken(newRefreshTokenHash, newExpiresAt);
        await _sessionRepository.UpdateAsync(session, cancellationToken);
        await _sessionRepository.SaveChangesAsync(cancellationToken);

        // Step 4: Generate new access token
        var accessToken = _tokenService.GenerateAccessToken(session.User, session.Id);

        return new AuthenticationResult
        {
            AccessToken = accessToken,
            RefreshToken = newRefreshToken,
            SessionId = session.Id,
            AccessTokenExpiresAt = DateTime.UtcNow.Add(_tokenService.AccessTokenLifetime),
            RefreshTokenExpiresAt = newExpiresAt,
            User = MapToUserInfoResponse(session.User)
        };
    }

    public async Task LogoutAsync(
        Guid sessionId,
        bool revokeAllSessions = false,
        CancellationToken cancellationToken = default)
    {
        if (revokeAllSessions)
        {
            // Get user from session first
            var session = await _sessionRepository.GetByIdAsync(sessionId, cancellationToken);
            if (session != null)
            {
                await _sessionRepository.RevokeAllUserSessionsAsync(session.UserId, cancellationToken);
            }
        }
        else
        {
            // Revoke just this session
            var session = await _sessionRepository.GetByIdAsync(sessionId, cancellationToken);
            if (session != null)
            {
                session.Revoke();
                await _sessionRepository.UpdateAsync(session, cancellationToken);
            }
        }

        await _sessionRepository.SaveChangesAsync(cancellationToken);
    }

    // ========== Private Helpers ==========

    private async Task<GoogleTokenResponse> ExchangeCodeForTokensAsync(
        string code,
        string codeVerifier,
        CancellationToken cancellationToken)
    {
        var httpClient = _httpClientFactory.CreateClient();

        var requestBody = new Dictionary<string, string>
        {
            ["code"] = code,
            ["client_id"] = _googleClientId,
            ["client_secret"] = _googleClientSecret,
            ["redirect_uri"] = _googleRedirectUri,
            ["grant_type"] = "authorization_code",
            ["code_verifier"] = codeVerifier
        };

        var response = await httpClient.PostAsync(
            "https://oauth2.googleapis.com/token",
            new FormUrlEncodedContent(requestBody),
            cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            var errorContent = await response.Content.ReadAsStringAsync(cancellationToken);
            throw new InvalidGoogleTokenException($"Token exchange failed: {errorContent}");
        }

        var responseContent = await response.Content.ReadAsStringAsync(cancellationToken);
        var tokenResponse = JsonSerializer.Deserialize<GoogleTokenJsonResponse>(responseContent);

        if (tokenResponse?.id_token == null)
        {
            throw new InvalidGoogleTokenException("Invalid response from Google token endpoint");
        }

        return new GoogleTokenResponse
        {
            AccessToken = tokenResponse.access_token!,
            IdToken = tokenResponse.id_token,
            RefreshToken = tokenResponse.refresh_token,
            ExpiresIn = tokenResponse.expires_in
        };
    }

    private static UserInfoResponse MapToUserInfoResponse(User user)
    {
        return new UserInfoResponse
        {
            Id = user.Id,
            Email = user.Email,
            FirstName = user.FirstName,
            LastName = user.LastName,
            DisplayName = user.GetDisplayName(),  // Combines first + last name
            ProfilePictureUrl = user.ProfilePictureUrl,
            CreatedAt = user.CreatedAt,
            LastLoginAt = user.LastLoginAt
        };
    }

    // JSON model for Google token response
    private class GoogleTokenJsonResponse
    {
        public string? access_token { get; set; }
        public string? id_token { get; set; }
        public string? refresh_token { get; set; }
        public int expires_in { get; set; }
        public string? token_type { get; set; }
    }
}

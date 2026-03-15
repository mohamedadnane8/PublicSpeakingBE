using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.IdentityModel.Tokens;
using MyApp.Application.Interfaces;
using MyApp.Domain.Entities;

namespace MyApp.Infrastructure.Authentication;

public class TokenService : ITokenService
{
    private readonly IConfiguration _configuration;
    private readonly ILogger<TokenService> _logger;
    private readonly string _secretKey;
    private readonly string _issuer;
    private readonly string _audience;
    private readonly int _accessTokenExpiryMinutes;
    private readonly int _refreshTokenExpiryDays;
    private readonly bool _isProduction;
    private readonly bool _cookieSecure;
    private readonly string _cookieSameSite;

    public TokenService(IConfiguration configuration, ILogger<TokenService> logger)
    {
        _configuration = configuration;
        _logger = logger;
        
        var jwtSettings = configuration.GetSection("Jwt");
        _secretKey = jwtSettings["SecretKey"]!;
        _issuer = jwtSettings["Issuer"]!;
        _audience = jwtSettings["Audience"]!;
        _accessTokenExpiryMinutes = int.Parse(jwtSettings["AccessTokenExpiryMinutes"] ?? "15");
        _refreshTokenExpiryDays = int.Parse(jwtSettings["RefreshTokenExpiryDays"] ?? "7");
        _isProduction = !string.Equals(
            configuration["ASPNETCORE_ENVIRONMENT"], 
            "Development", 
            StringComparison.OrdinalIgnoreCase);

        // Cookie policy can be overridden per environment via configuration.
        // Defaults remain secure: SameSite=Strict and Secure in production only.
        _cookieSecure = bool.TryParse(configuration["AuthCookies:Secure"], out var secureOverride)
            ? secureOverride
            : _isProduction;

        var configuredSameSite = configuration["AuthCookies:SameSite"]?.Trim();
        _cookieSameSite = NormalizeSameSite(configuredSameSite, logger);

        // Browsers require Secure=true when SameSite=None.
        if (_cookieSameSite == SameSiteMode.None.ToString() && !_cookieSecure)
        {
            _logger.LogWarning("AuthCookies:SameSite=None requires Secure cookies. Forcing Secure=true.");
            _cookieSecure = true;
        }

        _logger.LogInformation(
            "Auth cookie policy: SameSite={SameSite}, Secure={Secure}, Environment={Environment}",
            _cookieSameSite,
            _cookieSecure,
            configuration["ASPNETCORE_ENVIRONMENT"] ?? "Unknown");
    }

    public TimeSpan AccessTokenLifetime => TimeSpan.FromMinutes(_accessTokenExpiryMinutes);
    public TimeSpan RefreshTokenLifetime => TimeSpan.FromDays(_refreshTokenExpiryDays);

    public string GenerateAccessToken(User user, Guid sessionId)
    {
        var securityKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_secretKey));
        var credentials = new SigningCredentials(securityKey, SecurityAlgorithms.HmacSha256);

        var claims = new List<Claim>
        {
            new Claim(JwtRegisteredClaimNames.Sub, user.Id.ToString()),
            new Claim(JwtRegisteredClaimNames.Email, user.Email),
            new Claim(JwtRegisteredClaimNames.GivenName, user.FirstName),
            new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString()),
            new Claim("session_id", sessionId.ToString()),
            new Claim("type", "access")
        };

        // Only add family name claim if user has a last name
        if (!string.IsNullOrWhiteSpace(user.LastName))
        {
            claims.Add(new Claim(JwtRegisteredClaimNames.FamilyName, user.LastName));
        }

        var token = new JwtSecurityToken(
            issuer: _issuer,
            audience: _audience,
            claims: claims,
            expires: DateTime.UtcNow.AddMinutes(_accessTokenExpiryMinutes),
            signingCredentials: credentials
        );

        return new JwtSecurityTokenHandler().WriteToken(token);
    }

    public ClaimsPrincipal? ValidateAccessToken(string accessToken)
    {
        try
        {
            var tokenHandler = new JwtSecurityTokenHandler();
            var validationParameters = new TokenValidationParameters
            {
                ValidateIssuer = true,
                ValidateAudience = true,
                ValidateLifetime = true,
                ValidateIssuerSigningKey = true,
                ValidIssuer = _issuer,
                ValidAudience = _audience,
                IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_secretKey)),
                ClockSkew = TimeSpan.FromMinutes(1)
            };

            var principal = tokenHandler.ValidateToken(accessToken, validationParameters, out var validatedToken);
            
            var jwtToken = validatedToken as JwtSecurityToken;
            _logger.LogDebug("Token validation: Token parsed successfully. " +
                "Issuer: {Issuer}, Audience: {Audience}, Expires: {Expires}",
                jwtToken?.Issuer,
                jwtToken?.Audiences.FirstOrDefault(),
                jwtToken?.ValidTo);
            
            // Additional validation: ensure it's an access token
            var tokenType = principal.FindFirst("type")?.Value;
            if (tokenType != "access")
            {
                _logger.LogWarning("Token validation FAILED: Token type is '{TokenType}', expected 'access'", 
                    tokenType);
                return null;
            }

            _logger.LogDebug("Token validation SUCCESS for user {UserId}", 
                principal.FindFirst(JwtRegisteredClaimNames.Sub)?.Value);

            return principal;
        }
        catch (SecurityTokenExpiredException ex)
        {
            _logger.LogWarning("Token validation FAILED: Token has expired at {Expiry}", 
                ex.Expires);
            return null;
        }
        catch (SecurityTokenInvalidIssuerException ex)
        {
            _logger.LogWarning("Token validation FAILED: Invalid issuer. Expected: {Expected}, Got: {Actual}",
                _issuer, ex.Message);
            return null;
        }
        catch (SecurityTokenInvalidAudienceException ex)
        {
            _logger.LogWarning("Token validation FAILED: Invalid audience. Expected: {Expected}, Got: {Actual}",
                _audience, ex.Message);
            return null;
        }
        catch (SecurityTokenInvalidSignatureException)
        {
            _logger.LogWarning("Token validation FAILED: Invalid signature. " +
                "This usually means the JWT secret has changed or the token was tampered with.");
            return null;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Token validation FAILED: Unexpected error - {ErrorType}", 
                ex.GetType().Name);
            return null;
        }
    }

    public Guid? GetUserIdFromToken(string accessToken)
    {
        try
        {
            var handler = new JwtSecurityTokenHandler();
            var token = handler.ReadJwtToken(accessToken);
            var subClaim = token.Claims.FirstOrDefault(c => c.Type == JwtRegisteredClaimNames.Sub)?.Value;
            return subClaim != null && Guid.TryParse(subClaim, out var userId) ? userId : null;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to extract user ID from token");
            return null;
        }
    }

    public Guid? GetSessionIdFromToken(string accessToken)
    {
        try
        {
            var handler = new JwtSecurityTokenHandler();
            var token = handler.ReadJwtToken(accessToken);
            var sessionClaim = token.Claims.FirstOrDefault(c => c.Type == "session_id")?.Value;
            return sessionClaim != null && Guid.TryParse(sessionClaim, out var sessionId) ? sessionId : null;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to extract session ID from token");
            return null;
        }
    }

    public string GenerateRefreshToken()
    {
        // Generate 32 bytes of cryptographically secure random data
        var randomBytes = new byte[32];
        using (var rng = RandomNumberGenerator.Create())
        {
            rng.GetBytes(randomBytes);
        }
        return Convert.ToBase64String(randomBytes)
            .Replace("+", "-")
            .Replace("/", "_")
            .Replace("=", "");
    }

    public string HashRefreshToken(string refreshToken)
    {
        using var sha256 = SHA256.Create();
        var hashBytes = sha256.ComputeHash(Encoding.UTF8.GetBytes(refreshToken));
        return Convert.ToBase64String(hashBytes);
    }

    public bool VerifyRefreshToken(string refreshToken, string hash)
    {
        var computedHash = HashRefreshToken(refreshToken);
        return CryptographicOperations.FixedTimeEquals(
            Encoding.UTF8.GetBytes(computedHash),
            Encoding.UTF8.GetBytes(hash));
    }

    // ========== Cookie Options ==========

    public TokenCookieOptions GetAccessTokenCookieOptions()
    {
        return new TokenCookieOptions
        {
            HttpOnly = true,
            Secure = _cookieSecure,
            SameSite = _cookieSameSite,
            Path = "/api",  // Only sent to API routes
            MaxAgeMinutes = _accessTokenExpiryMinutes,
        };
    }

    public TokenCookieOptions GetRefreshTokenCookieOptions()
    {
        return new TokenCookieOptions
        {
            HttpOnly = true,
            Secure = _cookieSecure,
            SameSite = _cookieSameSite,
            Path = "/api/auth/refresh",  // Only sent to refresh endpoint
            MaxAgeMinutes = _refreshTokenExpiryDays * 24 * 60,
        };
    }

    public TokenCookieOptions GetSessionIdCookieOptions()
    {
        return new TokenCookieOptions
        {
            HttpOnly = true,
            Secure = _cookieSecure,
            SameSite = _cookieSameSite,
            Path = "/",
            MaxAgeMinutes = _refreshTokenExpiryDays * 24 * 60,
        };
    }

    public TokenCookieOptions GetOAuthStateCookieOptions()
    {
        return new TokenCookieOptions
        {
            HttpOnly = true,
            Secure = _cookieSecure,
            SameSite = "Lax",  // Lax for OAuth redirects
            Path = "/api/auth",
            MaxAgeMinutes = 10,
        };
    }

    public TokenCookieOptions GetDeleteCookieOptions(string path = "/")
    {
        return new TokenCookieOptions
        {
            HttpOnly = true,
            Secure = _cookieSecure,
            SameSite = _cookieSameSite,
            Path = path,
            MaxAgeMinutes = 0  // Expire immediately
        };
    }

    private static string NormalizeSameSite(string? configuredSameSite, ILogger logger)
    {
        if (string.IsNullOrWhiteSpace(configuredSameSite))
        {
            return SameSiteMode.Strict.ToString();
        }

        if (Enum.TryParse<SameSiteMode>(configuredSameSite, true, out var mode))
        {
            return mode.ToString();
        }

        logger.LogWarning(
            "Invalid AuthCookies:SameSite value '{Value}'. Falling back to Strict.",
            configuredSameSite);
        return SameSiteMode.Strict.ToString();
    }
}

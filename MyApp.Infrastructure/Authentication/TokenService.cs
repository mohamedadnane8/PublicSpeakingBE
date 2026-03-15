using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Configuration;
using Microsoft.IdentityModel.Tokens;
using MyApp.Application.Interfaces;
using MyApp.Domain.Entities;

namespace MyApp.Infrastructure.Authentication;

public class TokenService : ITokenService
{
    private readonly IConfiguration _configuration;
    private readonly string _secretKey;
    private readonly string _issuer;
    private readonly string _audience;
    private readonly int _accessTokenExpiryMinutes;
    private readonly int _refreshTokenExpiryDays;
    private readonly bool _isProduction;

    public TokenService(IConfiguration configuration)
    {
        _configuration = configuration;
        
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

            var principal = tokenHandler.ValidateToken(accessToken, validationParameters, out _);
            
            // Additional validation: ensure it's an access token
            var tokenType = principal.FindFirst("type")?.Value;
            if (tokenType != "access")
                return null;

            return principal;
        }
        catch
        {
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
        catch
        {
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
        catch
        {
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
            Secure = _isProduction,  // Only HTTPS in production
            SameSite = "Strict",
            Path = "/api",  // Only sent to API routes
            MaxAgeMinutes = _accessTokenExpiryMinutes,
        };
    }

    public TokenCookieOptions GetRefreshTokenCookieOptions()
    {
        return new TokenCookieOptions
        {
            HttpOnly = true,
            Secure = _isProduction,
            SameSite = "Strict",
            Path = "/api/auth/refresh",  // Only sent to refresh endpoint
            MaxAgeMinutes = _refreshTokenExpiryDays * 24 * 60,
        };
    }

    public TokenCookieOptions GetSessionIdCookieOptions()
    {
        return new TokenCookieOptions
        {
            HttpOnly = true,
            Secure = _isProduction,
            SameSite = "Strict",
            Path = "/",
            MaxAgeMinutes = _refreshTokenExpiryDays * 24 * 60,
        };
    }

    public TokenCookieOptions GetOAuthStateCookieOptions()
    {
        return new TokenCookieOptions
        {
            HttpOnly = true,
            Secure = _isProduction,
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
            Secure = _isProduction,
            SameSite = "Strict",
            Path = path,
            MaxAgeMinutes = 0  // Expire immediately
        };
    }
}

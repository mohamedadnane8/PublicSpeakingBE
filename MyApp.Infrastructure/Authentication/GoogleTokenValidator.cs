using Google.Apis.Auth;
using MyApp.Application.DTOs;
using MyApp.Application.Interfaces;
using MyApp.Domain.Exceptions;

namespace MyApp.Infrastructure.Authentication;

public class GoogleTokenValidator : IGoogleTokenValidator
{
    public async Task<GoogleUserInfo> ValidateAsync(string idToken, CancellationToken cancellationToken = default)
    {
        try
        {
            var payload = await GoogleJsonWebSignature.ValidateAsync(idToken);

            if (string.IsNullOrEmpty(payload.Email))
            {
                throw new InvalidGoogleTokenException("Email not found in token");
            }

            if (string.IsNullOrEmpty(payload.Subject))
            {
                throw new InvalidGoogleTokenException("Google ID not found in token");
            }

            return new GoogleUserInfo
            {
                Email = payload.Email,
                FirstName = payload.GivenName ?? string.Empty,
                LastName = payload.FamilyName ?? string.Empty,
                GoogleId = payload.Subject,
                ProfilePictureUrl = payload.Picture,
                EmailVerified = payload.EmailVerified
            };
        }
        catch (InvalidJwtException ex)
        {
            throw new InvalidGoogleTokenException($"Token validation failed: {ex.Message}");
        }
    }
}

using Google.Apis.Auth;
using MyApp.Application.DTOs;
using MyApp.Application.Interfaces;
using MyApp.Domain.Exceptions;

namespace MyApp.Infrastructure.Authentication;

/// <summary>
/// Validates Google ID tokens and extracts user information.
/// Handles cases where Google may not provide all profile fields.
/// </summary>
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

            // Safely extract all fields with null handling
            return new GoogleUserInfo
            {
                Email = payload.Email,
                
                // Given name (first name) - may be null
                FirstName = SafeExtract(payload.GivenName),
                
                // Family name (last name) - may be null
                LastName = SafeExtract(payload.FamilyName),
                
                // Full name - may be null
                FullName = SafeExtract(payload.Name),
                
                GoogleId = payload.Subject,
                ProfilePictureUrl = SafeExtract(payload.Picture),
                EmailVerified = payload.EmailVerified
            };
        }
        catch (InvalidJwtException ex)
        {
            throw new InvalidGoogleTokenException($"Token validation failed: {ex.Message}");
        }
    }

    /// <summary>
    /// Safely extracts a string value, returning null if empty/whitespace.
    /// </summary>
    private static string? SafeExtract(string? value)
    {
        return string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    }
}

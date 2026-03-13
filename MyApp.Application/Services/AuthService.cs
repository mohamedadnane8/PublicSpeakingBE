using MyApp.Application.DTOs;
using MyApp.Application.Interfaces;
using MyApp.Domain.Entities;
using MyApp.Domain.Exceptions;

namespace MyApp.Application.Services;

public class AuthService : IAuthService
{
    private readonly IUserRepository _userRepository;
    private readonly IGoogleTokenValidator _googleTokenValidator;
    private readonly IJwtTokenService _jwtTokenService;

    public AuthService(
        IUserRepository userRepository,
        IGoogleTokenValidator googleTokenValidator,
        IJwtTokenService jwtTokenService)
    {
        _userRepository = userRepository;
        _googleTokenValidator = googleTokenValidator;
        _jwtTokenService = jwtTokenService;
    }

    public async Task<AuthResponse> GoogleLoginAsync(string googleIdToken, CancellationToken cancellationToken = default)
    {
        // Step 1: Validate the Google token
        var googleUserInfo = await _googleTokenValidator.ValidateAsync(googleIdToken, cancellationToken);

        // Step 2: Check if email is verified
        if (!googleUserInfo.EmailVerified)
        {
            throw new InvalidGoogleTokenException("Email not verified with Google");
        }

        // Step 3: Find existing user or create new one
        var user = await _userRepository.GetByEmailAsync(googleUserInfo.Email, cancellationToken);

        if (user == null)
        {
            // Create new user
            user = User.Create(
                googleUserInfo.Email,
                googleUserInfo.FirstName,
                googleUserInfo.LastName,
                googleUserInfo.GoogleId,
                googleUserInfo.ProfilePictureUrl);

            await _userRepository.AddAsync(user, cancellationToken);
        }
        else
        {
            // Update existing user's profile info
            user.UpdateProfile(
                googleUserInfo.FirstName,
                googleUserInfo.LastName,
                googleUserInfo.ProfilePictureUrl);
        }

        // Step 4: Record login time
        user.RecordLogin();
        await _userRepository.SaveChangesAsync(cancellationToken);

        // Step 5: Generate JWT token
        var jwtToken = _jwtTokenService.GenerateToken(user);

        // Step 6: Build response
        return new AuthResponse
        {
            Token = jwtToken,
            ExpiresAt = DateTime.UtcNow.AddHours(24),
            User = MapToUserDto(user)
        };
    }

    private static UserDto MapToUserDto(User user)
    {
        return new UserDto
        {
            Id = user.Id,
            Email = user.Email,
            FirstName = user.FirstName,
            LastName = user.LastName,
            ProfilePictureUrl = user.ProfilePictureUrl,
            CreatedAt = user.CreatedAt,
            LastLoginAt = user.LastLoginAt
        };
    }
}

using MyApp.Application.DTOs;

namespace MyApp.Application.Interfaces;

public interface IAuthService
{
    Task<AuthResponse> GoogleLoginAsync(string googleIdToken, CancellationToken cancellationToken = default);
}

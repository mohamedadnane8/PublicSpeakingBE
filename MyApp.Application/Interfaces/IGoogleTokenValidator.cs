using MyApp.Application.DTOs;

namespace MyApp.Application.Interfaces;

public interface IGoogleTokenValidator
{
    Task<GoogleUserInfo> ValidateAsync(string idToken, CancellationToken cancellationToken = default);
}

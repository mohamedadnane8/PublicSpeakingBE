using MyApp.Domain.Entities;

namespace MyApp.Application.Interfaces;

public interface IJwtTokenService
{
    string GenerateToken(User user);
}

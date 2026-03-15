using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using MyApp.Application.Interfaces;
using MyApp.Domain.Entities;
using MyApp.Infrastructure.Authentication;
using MyApp.Infrastructure.Data;
using MyApp.Infrastructure.Repositories;

namespace MyApp.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        // Database
        services.AddDbContext<ApplicationDbContext>(options =>
            options.UseNpgsql(
                configuration.GetConnectionString("DefaultConnection"),
                b => b.MigrationsAssembly("MyApp.Infrastructure")));

        // Repositories
        services.AddScoped<IUserRepository, UserRepository>();
        services.AddScoped<ISessionRepository, SessionRepository>();
        services.AddScoped<IUserSessionRepository, UserSessionRepository>();

        // Authentication services
        services.AddScoped<IGoogleTokenValidator, GoogleTokenValidator>();
        services.AddScoped<ITokenService, TokenService>();
        // Note: IJwtTokenService is replaced by ITokenService
        // Remove registration for IJwtTokenService if not used elsewhere
        services.AddScoped<IJwtTokenService, JwtTokenServiceAdapter>();

        return services;
    }
}

/// <summary>
/// Adapter to maintain backward compatibility with IJwtTokenService
/// </summary>
public class JwtTokenServiceAdapter : IJwtTokenService
{
    private readonly ITokenService _tokenService;

    public JwtTokenServiceAdapter(ITokenService tokenService)
    {
        _tokenService = tokenService;
    }

    public string GenerateToken(User user)
    {
        // Generate a token without session ID for backward compatibility
        // This is a simplified version - in production, migrate all usages to ITokenService
        throw new NotImplementedException(
            "Use ITokenService.GenerateAccessToken with session ID instead. " +
            "This method is deprecated for security reasons.");
    }
}

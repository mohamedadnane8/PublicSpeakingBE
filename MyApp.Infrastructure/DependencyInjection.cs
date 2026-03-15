using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Amazon;
using Amazon.Runtime;
using Amazon.S3;
using MyApp.Application.Interfaces;
using MyApp.Domain.Entities;
using MyApp.Infrastructure.Authentication;
using MyApp.Infrastructure.Data;
using MyApp.Infrastructure.Repositories;
using MyApp.Infrastructure.Storage;

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
                b => b.MigrationsAssembly("MyApp.Infrastructure"))
            .ConfigureWarnings(warnings => 
                warnings.Ignore(Microsoft.EntityFrameworkCore.Diagnostics.RelationalEventId.PendingModelChangesWarning)));

        // Repositories
        services.AddScoped<IUserRepository, UserRepository>();
        services.AddScoped<ISessionRepository, SessionRepository>();
        services.AddScoped<IUserSessionRepository, UserSessionRepository>();
        services.AddScoped<IFeatureRequestRepository, FeatureRequestRepository>();

        // S3 Storage
        services.Configure<S3StorageOptions>(configuration.GetSection(S3StorageOptions.SectionName));

        services.AddSingleton<IAmazonS3>(serviceProvider =>
        {
            var options = serviceProvider.GetRequiredService<IOptions<S3StorageOptions>>().Value;
            if (string.IsNullOrWhiteSpace(options.Region))
            {
                throw new InvalidOperationException("S3Storage:Region is required.");
            }

            var region = RegionEndpoint.GetBySystemName(options.Region);
            var hasStaticCredentials =
                !string.IsNullOrWhiteSpace(options.AccessKeyId) &&
                !string.IsNullOrWhiteSpace(options.SecretAccessKey);

            if (hasStaticCredentials)
            {
                AWSCredentials credentials;
                if (string.IsNullOrWhiteSpace(options.SessionToken))
                {
                    credentials = new BasicAWSCredentials(options.AccessKeyId!, options.SecretAccessKey!);
                }
                else
                {
                    credentials = new SessionAWSCredentials(options.AccessKeyId!, options.SecretAccessKey!, options.SessionToken!);
                }

                return new AmazonS3Client(credentials, region);
            }

            return new AmazonS3Client(region);
        });
        services.AddScoped<IS3StorageService, S3StorageService>();

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

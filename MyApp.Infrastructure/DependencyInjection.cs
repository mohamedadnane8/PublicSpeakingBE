using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Amazon;
using Amazon.Runtime;
using Amazon.S3;
using MyApp.Application.Interfaces;
using MyApp.Infrastructure.Authentication;
using MyApp.Infrastructure.Configuration;
using MyApp.Infrastructure.Data;
using MyApp.Infrastructure.Repositories;
using MyApp.Infrastructure.Services;
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

        // Resume parsing
        services.AddScoped<IResumeParserService, ResumeParserService>();

        // Interview question repository
        services.AddScoped<IInterviewQuestionRepository, InterviewQuestionRepository>();

        // DeepSeek AI
        services.Configure<DeepSeekOptions>(configuration.GetSection(DeepSeekOptions.SectionName));
        services.AddHttpClient<IDeepSeekService, DeepSeekService>(client =>
        {
            client.Timeout = TimeSpan.FromMinutes(5);
        });

        // Resume upload rate limiting
        services.Configure<ResumeUploadOptions>(configuration.GetSection(ResumeUploadOptions.SectionName));

        // Speech Analysis (reuses DeepSeek config)
        services.AddHttpClient<ISpeechAnalysisService, SpeechAnalysisService>(client =>
        {
            client.Timeout = TimeSpan.FromMinutes(3);
        });

        // AssemblyAI Transcription
        services.Configure<AssemblyAIOptions>(configuration.GetSection(AssemblyAIOptions.SectionName));
        services.AddHttpClient<ITranscriptionService, AssemblyAITranscriptionService>(client =>
        {
            client.Timeout = TimeSpan.FromMinutes(15);
        });

        return services;
    }
}

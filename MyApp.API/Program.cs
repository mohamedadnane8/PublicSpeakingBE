using System.Text;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using MyApp.API.Controllers;
using MyApp.API.Middleware;
using MyApp.Application.Interfaces;
using MyApp.Application.Services;
using MyApp.Infrastructure;
using MyApp.Infrastructure.Data;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container
builder.Services.AddControllers();

// Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

// Configure JWT Authentication (for cookie validation)
var jwtSettings = builder.Configuration.GetSection("Jwt");
var secretKey = jwtSettings["SecretKey"]!;

builder.Services.AddAuthentication(options =>
{
    options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
    options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
})
.AddJwtBearer(options =>
{
    options.TokenValidationParameters = new TokenValidationParameters
    {
        ValidateIssuer = true,
        ValidateAudience = true,
        ValidateLifetime = true,
        ValidateIssuerSigningKey = true,
        ValidIssuer = jwtSettings["Issuer"],
        ValidAudience = jwtSettings["Audience"],
        IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(secretKey)),
        ClockSkew = TimeSpan.Zero
    };

    // Configure JWT Bearer to read from cookie instead of Authorization header
    options.Events = new JwtBearerEvents
    {
        OnMessageReceived = context =>
        {
            // Try to get the token from the access_token cookie
            if (context.Request.Cookies.TryGetValue("access_token", out var token))
            {
                context.Token = token;
                
                var logger = context.HttpContext.RequestServices
                    .GetRequiredService<ILogger<Program>>();
                logger.LogDebug("JWT: Token read from cookie (length: {Length})", token.Length);
            }
            else
            {
                var logger = context.HttpContext.RequestServices
                    .GetRequiredService<ILogger<Program>>();
                logger.LogDebug("JWT: No access_token cookie found. " +
                    "Cookies present: {Cookies}", 
                    string.Join(", ", context.Request.Cookies.Keys));
            }
            
            return Task.CompletedTask;
        },
        
        OnAuthenticationFailed = context =>
        {
            var logger = context.HttpContext.RequestServices
                .GetRequiredService<ILogger<Program>>();
            
            logger.LogError(context.Exception, 
                "JWT Authentication FAILED: {ErrorType} - {Message}. " +
                "Token present: {HasToken}",
                context.Exception.GetType().Name,
                context.Exception.Message,
                !string.IsNullOrEmpty(context.Request.Cookies["access_token"]));
            
            return Task.CompletedTask;
        },
        
        OnTokenValidated = context =>
        {
            var logger = context.HttpContext.RequestServices
                .GetRequiredService<ILogger<Program>>();
            
            var userId = context.Principal?.FindFirst("sub")?.Value ?? "unknown";
            logger.LogDebug("JWT: Token validated successfully for user {UserId}", userId);
            
            return Task.CompletedTask;
        },
        
        OnChallenge = context =>
        {
            var logger = context.HttpContext.RequestServices
                .GetRequiredService<ILogger<Program>>();
            
            logger.LogWarning("JWT Challenge triggered. Error: {Error}. " +
                "Description: {Description}. " +
                "Path: {Path}",
                context.Error,
                context.ErrorDescription,
                context.Request.Path);
            
            return Task.CompletedTask;
        }
    };
});

// Add Authorization
builder.Services.AddAuthorization();

// Configure Frontend Options
builder.Services.Configure<FrontendOptions>(options =>
{
    options.BaseUrl = builder.Configuration["Frontend:BaseUrl"]!;
});

// Add HttpClient for Google token exchange
builder.Services.AddHttpClient();

// Add Infrastructure services (DB, Repositories, External services)
builder.Services.AddInfrastructure(builder.Configuration);

// Add Application services
builder.Services.AddScoped<IAuthService, AuthService>();
builder.Services.AddScoped<ISessionService, SessionService>();
builder.Services.AddScoped<IResumeService, ResumeService>();
builder.Services.AddScoped<IWordService, WordService>();
builder.Services.AddScoped<IFeatureRequestService, FeatureRequestService>();
builder.Services.AddSingleton<IBehavioralQuestionService, BehavioralQuestionService>();

// Configure CORS as a service (must be before Build)
var allowedOrigins = new List<string>();
var configuredFrontend = builder.Configuration["Frontend:BaseUrl"];
if (!string.IsNullOrEmpty(configuredFrontend))
{
    allowedOrigins.Add(configuredFrontend);
}
allowedOrigins.Add("http://localhost:3000");
allowedOrigins.Add("https://localhost:3000");

builder.Services.AddCors(options =>
{
    options.AddDefaultPolicy(policy =>
    {
        policy.WithOrigins(allowedOrigins.ToArray())
              .AllowCredentials()
              .AllowAnyHeader()
              .AllowAnyMethod();
    });
});

var app = builder.Build();

var logger = app.Services.GetRequiredService<ILogger<Program>>();
logger.LogInformation("CORS configured with origins: {Origins}", string.Join(", ", allowedOrigins));

// Configure the HTTP request pipeline
app.UseSwagger();
app.UseSwaggerUI(options =>
{
    options.SwaggerEndpoint("/swagger/v1/swagger.json", "PublicSpeaking API v1");
    options.RoutePrefix = "swagger";
    options.DocumentTitle = "PublicSpeaking API Documentation";
});

app.UseHttpsRedirection();

// Custom exception handling middleware
app.UseMiddleware<ExceptionHandlingMiddleware>();

// CORS must be before auth
app.UseCors();

app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();

app.Run();

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
builder.Services.AddScoped<IWordService, WordService>();
builder.Services.AddScoped<IFeatureRequestService, FeatureRequestService>();

var app = builder.Build();

// Configure the HTTP request pipeline
// Enable Swagger in all environments
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

// Add CORS middleware before authentication
// Configure allowed origins - supports multiple origins for local development
var allowedOrigins = new List<string>();

// Add configured frontend URL
var configuredFrontend = builder.Configuration["Frontend:BaseUrl"];
if (!string.IsNullOrEmpty(configuredFrontend))
{
    allowedOrigins.Add(configuredFrontend);
}

// Always allow localhost for development (remove in production if needed)
allowedOrigins.Add("http://localhost:3000");
allowedOrigins.Add("https://localhost:3000");

var logger = app.Services.GetRequiredService<ILogger<Program>>();
logger.LogInformation("CORS configured with origins: {Origins}", string.Join(", ", allowedOrigins));

app.UseCors(options =>
{
    options.WithOrigins(allowedOrigins.ToArray())
           .AllowCredentials()  // Required for cookies
           .AllowAnyHeader()
           .AllowAnyMethod();
});

app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();

app.Run();

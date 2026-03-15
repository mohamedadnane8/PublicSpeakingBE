using System.Text;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using MyApp.API.Controllers;
using MyApp.API.Middleware;
using MyApp.Application.Interfaces;
using MyApp.Application.Services;
using MyApp.Infrastructure;

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
            if (context.Request.Cookies.ContainsKey("access_token"))
            {
                context.Token = context.Request.Cookies["access_token"];
            }
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

var app = builder.Build();

// Configure the HTTP request pipeline
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

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

app.UseCors(options =>
{
    var frontendUrl = builder.Configuration["Frontend:BaseUrl"]!;
    options.WithOrigins(frontendUrl)
           .AllowCredentials()  // Required for cookies
           .AllowAnyHeader()
           .AllowAnyMethod();
});

app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();

app.Run();

# MyApp - Clean Architecture .NET Web API

A production-ready .NET Web API built with Clean Architecture, featuring Google authentication.

## Project Structure

```
MyApp/
├── MyApp.Domain/           # Core business logic, entities, exceptions
├── MyApp.Application/      # Application services, DTOs, interfaces
├── MyApp.Infrastructure/   # EF Core, repositories, external services
└── MyApp.API/              # Controllers, middleware, DI configuration
```

### Why Each Project Exists

| Project | Purpose | Dependencies |
|---------|---------|--------------|
| **MyApp.Domain** | Contains core business entities and domain-specific exceptions. Has zero external dependencies. | None |
| **MyApp.Application** | Orchestrates use cases, defines interfaces (contracts) that infrastructure must implement, validates input, maps data. | MyApp.Domain |
| **MyApp.Infrastructure** | Implements interfaces defined in Application. Handles database access, external API calls (Google), JWT generation. | MyApp.Application |
| **MyApp.API** | HTTP layer - controllers, middleware, request/response handling, authentication setup, Swagger. | MyApp.Application, MyApp.Infrastructure |

## Prerequisites

- .NET 10.0 SDK or later
- SQL Server (LocalDB or full instance)

## Getting Started

### 1. Clone and Build

```bash
# Build the solution
dotnet build

# Run the API
cd MyApp.API
dotnet run
```

### 2. Database Setup

```bash
# Create and apply migrations
cd MyApp.Infrastructure
dotnet ef migrations add InitialCreate --startup-project ../MyApp.API
dotnet ef database update --startup-project ../MyApp.API
```

Or configure automatic migrations in `Program.cs` (not recommended for production).

### 3. Configure JWT Secret

Update `appsettings.json` (or use user-secrets for production):

```json
"Jwt": {
  "SecretKey": "your-super-secret-key-at-least-32-characters-long-for-jwt-signing",
  "Issuer": "MyApp",
  "Audience": "MyAppUsers",
  "ExpiryHours": "24"
}
```

**Important:** Use a strong, random secret key in production (at least 32 characters).

## API Endpoints

### POST /api/auth/google

Authenticates a user using a Google ID token.

**Request:**
```json
{
  "idToken": "eyJhbGciOiJSUzI1NiIs..."
}
```

**Response:**
```json
{
  "token": "eyJhbGciOiJIUzI1NiIs...",
  "expiresAt": "2024-01-20T12:00:00Z",
  "user": {
    "id": "550e8400-e29b-41d4-a716-446655440000",
    "email": "user@example.com",
    "firstName": "John",
    "lastName": "Doe",
    "profilePictureUrl": "https://lh3.googleusercontent.com/...",
    "createdAt": "2024-01-01T00:00:00Z",
    "lastLoginAt": "2024-01-19T12:00:00Z"
  }
}
```

## Required NuGet Packages

### MyApp.Domain
- (None - pure entities)

### MyApp.Application
- Microsoft.Extensions.DependencyInjection.Abstractions

### MyApp.Infrastructure
- Microsoft.EntityFrameworkCore.SqlServer
- Microsoft.EntityFrameworkCore.Design
- Microsoft.EntityFrameworkCore.Tools
- Google.Apis.Auth
- Microsoft.AspNetCore.Authentication.JwtBearer

### MyApp.API
- Swashbuckle.AspNetCore
- Microsoft.EntityFrameworkCore.Design (for migrations)

## Architecture Decisions

### What We Included

1. **Clean Architecture** - Clear separation of concerns with Domain at the center
2. **Repository Pattern** - Abstracts data access, makes testing easier
3. **Application Services** - Business logic lives here, not in controllers
4. **JWT Authentication** - Stateless, scalable authentication
5. **Google Token Validation** - Secure OAuth flow
6. **Exception Handling Middleware** - Consistent error responses
7. **Thin Controllers** - Controllers only handle HTTP concerns

### What We Excluded (Intentionally)

1. **MediatR** - Overkill for simple CRUD, adds complexity
2. **CQRS** - No need to split reads/writes for this use case
3. **Domain Events** - Adds complexity without clear benefit here
4. **FluentValidation** - Manual validation is sufficient for this scope
5. **AutoMapper** - Simple mapping functions work fine

## Authentication Flow

```
┌──────────────┐     ┌──────────────┐     ┌─────────────────┐
│    Client    │────▶│  POST /api   │────▶│  Google Token   │
│   (Browser)  │     │  /auth/google│     │   Validator     │
└──────────────┘     └──────────────┘     └─────────────────┘
                                                   │
                                                   ▼
┌──────────────┐     ┌──────────────┐     ┌─────────────────┐
│   Return     │◀────│  Generate    │◀────│  User Service   │
│ JWT + User   │     │  JWT Token   │     │ (find/create)   │
└──────────────┘     └──────────────┘     └─────────────────┘
```

## Testing the API

1. Get a Google ID token from your frontend (after Google Sign-In)
2. Send POST request:

```bash
curl -X POST http://localhost:5000/api/auth/google \
  -H "Content-Type: application/json" \
  -d '{"idToken": "YOUR_GOOGLE_ID_TOKEN"}'
```

## Layer Responsibilities

### Domain Layer
- `User` entity with factory method and business methods
- Domain exceptions for business rule violations
- **No dependencies on other layers**

### Application Layer
- `IAuthService` - Orchestrates the login flow
- `IGoogleTokenValidator` - Interface for Google token validation
- `IJwtTokenService` - Interface for JWT generation
- `IUserRepository` - Repository interface
- DTOs for request/response
- **No infrastructure code**

### Infrastructure Layer
- `ApplicationDbContext` - EF Core DbContext
- `UserRepository` - Repository implementation
- `GoogleTokenValidator` - Validates Google ID tokens
- `JwtTokenService` - Creates JWT tokens
- Extension method for DI registration

### API Layer
- `AuthController` - HTTP endpoint
- `ExceptionHandlingMiddleware` - Global error handling
- `Program.cs` - Service configuration and middleware pipeline

## Development Commands

```bash
# Create new migration
dotnet ef migrations add MigrationName --project MyApp.Infrastructure --startup-project MyApp.API

# Update database
dotnet ef database update --project MyApp.Infrastructure --startup-project MyApp.API

# Run tests (when added)
dotnet test

# Publish
dotnet publish -c Release
```

## License

MIT

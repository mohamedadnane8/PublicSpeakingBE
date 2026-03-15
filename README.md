# MyApp - Clean Architecture .NET Web API

A production-ready .NET Web API built with Clean Architecture, featuring **secure OAuth 2.0 with HttpOnly cookie-based authentication**.

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
- PostgreSQL (or SQL Server with minor config changes)
- Google OAuth 2.0 credentials

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

### 3. Configure Secrets

Update `appsettings.Development.json` (or use user-secrets for production):

```json
{
  "Jwt": {
    "Issuer": "MyApp",
    "Audience": "MyAppUsers",
    "AccessTokenExpiryMinutes": 15,
    "RefreshTokenExpiryDays": 7
  },
  "GoogleOAuth": {
    "ClientId": "your-client-id.apps.googleusercontent.com",
    "RedirectUri": "http://localhost:5000/api/auth/google/callback"
  },
  "Frontend": {
    "BaseUrl": "http://localhost:3000"
  }
}
```

**Important:** 
- Use a strong, random JWT secret key (at least 32 bytes)
- Never commit real secrets to git - use user-secrets or environment variables

### 4. Google Cloud Console Setup

1. Go to [Google Cloud Console](https://console.cloud.google.com/)
2. Create a new project or select existing
3. Enable **Google Identity Toolkit API**
4. Go to **Credentials** → **Create Credentials** → **OAuth client ID**
5. Configure consent screen with scopes: `openid`, `email`, `profile`
6. Create OAuth client ID:
   - Application type: **Web application**
   - Authorized redirect URIs:
     - `http://localhost:5000/api/auth/google/callback` (development)
     - `https://api.yourapp.com/api/auth/google/callback` (production)

---

## Authentication Flow

This implementation uses **OAuth 2.0 Authorization Code flow with PKCE** and **HttpOnly cookie-based authentication**.

### Why This Approach?

| Feature | Old Approach (Token in Body) | New Approach (HttpOnly Cookies) |
|---------|------------------------------|--------------------------------|
| **XSS Protection** | ❌ Vulnerable | ✅ Protected |
| **CSRF Protection** | ⚠️ Manual | ✅ SameSite + State |
| **Token Storage** | localStorage/sessionStorage | HttpOnly cookies |
| **Session Management** | Stateless | Stateful (revocable) |
| **Refresh Strategy** | Manual | Automatic rotation |

### Login Flow

```
1. User clicks "Login with Google"
   ↓
2. Frontend: window.location.href = '/api/auth/google/login'
   ↓
3. Backend generates PKCE + state, redirects to Google
   ↓
4. User authenticates with Google
   ↓
5. Google redirects to /api/auth/google/callback?code=xxx&state=yyy
   ↓
6. Backend validates, creates session, sets HttpOnly cookies
   ↓
7. Backend redirects to frontend /auth/success
   ↓
8. Frontend calls GET /api/auth/me → User is authenticated!
```

### Security Features

- ✅ **PKCE** - Protects against authorization code interception
- ✅ **State Parameter** - CSRF protection
- ✅ **HttpOnly Cookies** - JavaScript cannot access tokens (XSS protection)
- ✅ **SameSite=Strict** - CSRF protection for cross-site requests
- ✅ **Secure Flag** - HTTPS only in production
- ✅ **Refresh Token Rotation** - New refresh token on every use
- ✅ **Short-lived Access Tokens** - 15 minutes by default

---

## API Endpoints

### Authentication

| Method | Endpoint | Description | Auth Required |
|--------|----------|-------------|---------------|
| `GET` | `/api/auth/google/login` | Start OAuth flow | No |
| `GET` | `/api/auth/google/callback` | OAuth callback (Google → Backend) | No |
| `GET` | `/api/auth/me` | Get current user | Yes |
| `POST` | `/api/auth/refresh` | Refresh access token | Cookie |
| `POST` | `/api/auth/logout` | Logout and revoke session | Cookie |

### Example: Get Current User

```bash
curl http://localhost:5000/api/auth/me \
  -H "Accept: application/json" \
  --cookie "access_token=YOUR_JWT_COOKIE"
```

**Response:**
```json
{
  "id": "550e8400-e29b-41d4-a716-446655440000",
  "email": "user@example.com",
  "firstName": "John",
  "lastName": "Doe",
  "profilePictureUrl": "https://lh3.googleusercontent.com/...",
  "createdAt": "2024-01-01T00:00:00Z",
  "lastLoginAt": "2024-01-19T12:00:00Z"
}
```

---

## Frontend Integration

See [OAUTH_IMPLEMENTATION.md](OAUTH_IMPLEMENTATION.md) for complete frontend integration guide including:
- TypeScript/JavaScript examples
- React hooks
- Token refresh logic
- Error handling

### Quick Example

```typescript
// Login (triggers browser redirect)
function loginWithGoogle() {
  window.location.href = '/api/auth/google/login?redirectUri=/dashboard';
}

// Get current user
async function getCurrentUser() {
  const response = await fetch('/api/auth/me', {
    credentials: 'include'  // IMPORTANT: Sends cookies
  });
  return response.json();
}

// Make authenticated request
async function fetchWithAuth(url, options = {}) {
  const response = await fetch(url, {
    ...options,
    credentials: 'include'  // IMPORTANT: Sends cookies
  });
  
  if (response.status === 401) {
    // Try refresh
    const refreshed = await fetch('/api/auth/refresh', {
      method: 'POST',
      credentials: 'include'
    });
    
    if (refreshed.ok) {
      // Retry original request
      return fetch(url, { ...options, credentials: 'include' });
    }
  }
  
  return response;
}
```

---

## Required NuGet Packages

### MyApp.Domain
- (None - pure entities)

### MyApp.Application
- Microsoft.Extensions.DependencyInjection.Abstractions
- Microsoft.Extensions.Configuration.Abstractions
- Microsoft.Extensions.Http

### MyApp.Infrastructure
- Npgsql.EntityFrameworkCore.PostgreSQL
- Microsoft.EntityFrameworkCore.Design
- Microsoft.EntityFrameworkCore.Tools
- Google.Apis.Auth
- Microsoft.AspNetCore.Authentication.JwtBearer

### MyApp.API
- Swashbuckle.AspNetCore
- Microsoft.EntityFrameworkCore.Design (for migrations)

---

## Architecture Decisions

### What We Included

1. **Clean Architecture** - Clear separation of concerns with Domain at the center
2. **Repository Pattern** - Abstracts data access, makes testing easier
3. **Application Services** - Business logic lives here, not in controllers
4. **Stateful Sessions** - Database-backed sessions with refresh token rotation
5. **OAuth 2.0 + PKCE** - Modern, secure authentication flow
6. **HttpOnly Cookies** - XSS protection for authentication tokens
7. **Exception Handling Middleware** - Consistent error responses
8. **Thin Controllers** - Controllers only handle HTTP concerns

### What We Excluded (Intentionally)

1. **MediatR** - Overkill for simple CRUD, adds complexity
2. **CQRS** - No need to split reads/writes for this use case
3. **Domain Events** - Adds complexity without clear benefit here
4. **FluentValidation** - Manual validation is sufficient for this scope
5. **AutoMapper** - Simple mapping functions work fine

---

## Layer Responsibilities

### Domain Layer
- `User`, `UserSession`, `Session` entities
- Domain exceptions for business rule violations
- **No dependencies on other layers**

### Application Layer
- `IAuthService` - Orchestrates OAuth flow
- `ITokenService` - JWT and refresh token management
- `IGoogleTokenValidator` - Google token validation
- Repository interfaces
- DTOs for request/response
- **No infrastructure code**

### Infrastructure Layer
- `ApplicationDbContext` - EF Core DbContext
- `UserRepository`, `UserSessionRepository` - Repository implementations
- `GoogleTokenValidator` - Validates Google tokens
- `TokenService` - JWT and cookie configuration
- Extension method for DI registration

### API Layer
- `AuthController` - OAuth endpoints
- `SessionsController` - Session management
- `ExceptionHandlingMiddleware` - Global error handling
- `Program.cs` - Service configuration and middleware pipeline

---

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

---

## Security Checklist

Before deploying to production:

- [ ] Use HTTPS only (cookies require Secure flag)
- [ ] Set strong JWT secret (32+ bytes, random)
- [ ] Configure CORS properly (don't use `*`)
- [ ] Set up session cleanup job (remove expired sessions)
- [ ] Enable audit logging for auth events
- [ ] Configure rate limiting on auth endpoints
- [ ] Review Google OAuth consent screen settings
- [ ] Add monitoring for suspicious activity

---

## Documentation

- [OAUTH_IMPLEMENTATION.md](OAUTH_IMPLEMENTATION.md) - Detailed OAuth implementation guide
- [Google OAuth 2.0 Documentation](https://developers.google.com/identity/protocols/oauth2)

---

## License

MIT

# OAuth 2.0 Implementation Guide

This document describes the secure, cookie-based OAuth 2.0 implementation with Google.

## Table of Contents

1. [Architecture Overview](#architecture-overview)
2. [Security Features](#security-features)
3. [Flow Diagram](#flow-diagram)
4. [API Endpoints](#api-endpoints)
5. [Cookie Configuration](#cookie-configuration)
6. [Frontend Integration](#frontend-integration)
7. [Configuration](#configuration)
8. [Security Considerations](#security-considerations)

---

## Architecture Overview

This implementation uses a **stateful, server-side session** with **HttpOnly cookies** for token storage. This approach provides superior security compared to storing tokens in `localStorage` or `sessionStorage`.

### Key Components

```
┌─────────────┐      ┌─────────────┐      ┌─────────────┐      ┌─────────────┐
│   Browser   │      │   Frontend  │      │   Backend   │      │    Google   │
│   Cookies   │◄────►│   (SPA)     │◄────►│   (.NET)    │◄────►│    OAuth    │
└─────────────┘      └─────────────┘      └─────────────┘      └─────────────┘

- HttpOnly cookies    - No token storage   - Session DB        - PKCE flow
- Secure/Strict       - Redirect handling  - Token rotation    - State param
```

### State Management

| Storage | What | Why |
|---------|------|-----|
| **HttpOnly Cookie** | Access Token, Refresh Token, Session ID | JavaScript cannot read → XSS protection |
| **Database** | Refresh token hashes, session metadata | Server-side validation, revocation |
| **Memory** | Access token validation | Stateless JWT verification |

---

## Security Features

### 1. PKCE (Proof Key for Code Exchange)
- Protects against authorization code interception attacks
- Code verifier never leaves the browser (stored in HttpOnly cookie during flow)

### 2. State Parameter (CSRF Protection)
- Cryptographically random state parameter
- Stored in HttpOnly cookie and validated on callback
- Prevents cross-site request forgery attacks

### 3. HttpOnly Cookies
- Tokens are NOT accessible to JavaScript
- Prevents XSS attacks from stealing tokens
- Automatic browser handling

### 4. SameSite=Strict
- Cookies only sent to same-origin requests
- Prevents CSRF via cross-site POST requests

### 5. Secure Flag
- Cookies only sent over HTTPS in production
- Prevents token theft via network sniffing

### 6. Refresh Token Rotation
- New refresh token issued on every refresh
- Old refresh token invalidated
- Detects token reuse (potential theft)

### 7. Short-Lived Access Tokens
- 15-minute expiration by default
- Limits window of compromise

---

## Flow Diagram

### Login Flow

```
1. User clicks "Login with Google"
   
   Frontend: window.location.href = '/api/auth/google/login'

2. Backend generates PKCE parameters
   
   ├─ Generates state (CSRF protection)
   ├─ Generates code_verifier (PKCE)
   ├─ Stores in HttpOnly cookies
   └─ Redirects to Google

3. Google authenticates user
   
   User enters credentials, consents to permissions

4. Google redirects to callback
   
   GET /api/auth/google/callback?code=xxx&state=yyy

5. Backend validates and exchanges
   
   ├─ Validates state parameter
   ├─ Exchanges code for tokens (with code_verifier)
   ├─ Validates ID token
   ├─ Creates/updates user in DB
   ├─ Creates session with refresh token
   ├─ Sets HttpOnly cookies
   └─ Redirects to frontend

6. Frontend loads authenticated
   
   ├─ Calls GET /api/auth/me
   ├─ Receives user info
   └─ Updates UI state
```

### Token Refresh Flow

```
1. API returns 401 (token expired)

2. Frontend calls POST /api/auth/refresh
   
   Browser automatically sends refresh_token cookie

3. Backend validates and rotates
   
   ├─ Validates refresh token against DB hash
   ├─ Generates new access token
   ├─ Generates new refresh token
   ├─ Updates session in DB
   └─ Sets new cookies

4. Frontend retries original request
```

---

## API Endpoints

### `GET /api/auth/google/login`

Initiates the OAuth flow.

**Query Parameters:**
| Name | Type | Required | Description |
|------|------|----------|-------------|
| `redirectUri` | string | No | Where to redirect after auth (must be validated) |

**Response:**
- `302 Redirect` to Google OAuth authorization URL
- Sets `oauth_state`, `oauth_code_verifier` cookies

---

### `GET /api/auth/google/callback`

Handles Google OAuth callback. **Called by Google, not your frontend.**

**Query Parameters:**
| Name | Type | Required | Description |
|------|------|----------|-------------|
| `code` | string | Yes | Authorization code |
| `state` | string | Yes | State parameter |
| `error` | string | No | Error from Google |

**Response:**
- `302 Redirect` to frontend `/auth/success` or `/auth/error`
- Sets `access_token`, `refresh_token`, `session_id` cookies

---

### `GET /api/auth/me`

Get current authenticated user.

**Headers:**
```
Cookie: access_token=xxx
```

**Response (200):**
```json
{
  "id": "uuid",
  "email": "user@example.com",
  "firstName": "John",
  "lastName": "Doe",
  "profilePictureUrl": "https://...",
  "createdAt": "2024-01-15T10:30:00Z",
  "lastLoginAt": "2024-01-15T10:30:00Z"
}
```

**Response (401):**
```json
{
  "error": "invalid_token",
  "message": "Token expired or invalid"
}
```

---

### `POST /api/auth/refresh`

Refresh access token using refresh token cookie.

**Headers:**
```
Cookie: refresh_token=xxx; session_id=xxx
```

**Response (200):**
```json
{
  "success": true,
  "message": "Token refreshed successfully"
}
```

**Response (401):**
```json
{
  "success": false,
  "message": "Session expired. Please log in again."
}
```

---

### `POST /api/auth/logout`

Logout and revoke session.

**Headers:**
```
Cookie: session_id=xxx
```

**Request Body:**
```json
{
  "revokeAllSessions": false  // Set true to logout all devices
}
```

**Response (200):**
```json
{
  "success": true,
  "message": "Logged out successfully"
}
```

---

## Cookie Configuration

| Cookie | HttpOnly | Secure | SameSite | Path | Lifetime | Purpose |
|--------|----------|--------|----------|------|----------|---------|
| `access_token` | ✅ | Production | Strict | `/api` | 15 min | API authentication |
| `refresh_token` | ✅ | Production | Strict | `/api/auth/refresh` | 7 days | Token refresh only |
| `session_id` | ✅ | Production | Strict | `/` | 7 days | Session identification |
| `oauth_state` | ✅ | Production | Lax | `/api/auth` | 10 min | CSRF protection |
| `oauth_code_verifier` | ✅ | Production | Lax | `/api/auth` | 10 min | PKCE |

---

## Frontend Integration

### TypeScript Example

```typescript
// auth.service.ts
const API_URL = process.env.REACT_APP_API_URL;

class AuthService {
  /**
   * Initiate login - causes browser navigation
   */
  loginWithGoogle(returnUrl: string = '/'): void {
    const encodedReturnUrl = encodeURIComponent(returnUrl);
    window.location.href = `${API_URL}/auth/google/login?redirectUri=${encodedReturnUrl}`;
  }

  /**
   * Get current user - call on app load
   */
  async getCurrentUser(): Promise<User | null> {
    try {
      const response = await fetch(`${API_URL}/auth/me`, {
        method: 'GET',
        credentials: 'include',  // CRITICAL: sends cookies
        headers: { 'Accept': 'application/json' }
      });

      if (response.status === 401) {
        // Try to refresh
        const refreshed = await this.refreshToken();
        if (!refreshed) return null;
        
        // Retry
        return this.getCurrentUser();
      }

      if (!response.ok) throw new Error('Failed to get user');
      return await response.json();
    } catch (error) {
      console.error('Auth error:', error);
      return null;
    }
  }

  /**
   * Refresh access token
   */
  async refreshToken(): Promise<boolean> {
    try {
      const response = await fetch(`${API_URL}/auth/refresh`, {
        method: 'POST',
        credentials: 'include',  // Sends refresh_token cookie
      });
      return response.ok;
    } catch (error) {
      console.error('Refresh error:', error);
      return false;
    }
  }

  /**
   * Logout
   */
  async logout(revokeAll: boolean = false): Promise<void> {
    try {
      await fetch(`${API_URL}/auth/logout`, {
        method: 'POST',
        credentials: 'include',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify({ revokeAllSessions: revokeAll })
      });
    } finally {
      // Clear any local state
      window.location.href = '/';
    }
  }

  /**
   * Make authenticated API request with automatic retry
   */
  async fetchWithAuth(url: string, options: RequestInit = {}): Promise<Response> {
    const response = await fetch(url, {
      ...options,
      credentials: 'include',  // Always include cookies
      headers: {
        ...options.headers,
        'Accept': 'application/json'
      }
    });

    // If token expired, try to refresh and retry
    if (response.status === 401) {
      const refreshed = await this.refreshToken();
      if (refreshed) {
        // Retry original request
        return fetch(url, {
          ...options,
          credentials: 'include',
          headers: {
            ...options.headers,
            'Accept': 'application/json'
          }
        });
      }
    }

    return response;
  }
}

// React Hook Example
function useAuth() {
  const [user, setUser] = useState<User | null>(null);
  const [loading, setLoading] = useState(true);

  useEffect(() => {
    // Check auth on mount
    authService.getCurrentUser().then(user => {
      setUser(user);
      setLoading(false);
    });
  }, []);

  const login = () => authService.loginWithGoogle(window.location.pathname);
  const logout = () => authService.logout();

  return { user, loading, login, logout };
}
```

### Auth Success Page

Create `/auth/success` page in your frontend:

```typescript
// pages/auth/success.tsx
import { useEffect } from 'react';
import { useNavigate, useSearchParams } from 'react-router-dom';

export default function AuthSuccess() {
  const navigate = useNavigate();
  const [searchParams] = useSearchParams();

  useEffect(() => {
    // Get the redirect URI from query params (if any)
    const redirectUri = searchParams.get('redirect') || '/';
    
    // Redirect to original page
    navigate(redirectUri, { replace: true });
  }, []);

  return <div>Logging you in...</div>;
}
```

### Auth Error Page

Create `/auth/error` page:

```typescript
// pages/auth/error.tsx
import { useSearchParams } from 'react-router-dom';

export default function AuthError() {
  const [searchParams] = useSearchParams();
  const error = searchParams.get('error');
  const message = searchParams.get('message');

  return (
    <div>
      <h1>Authentication Failed</h1>
      <p>Error: {error}</p>
      <p>{message}</p>
      <button onClick={() => window.location.href = '/'}>Try Again</button>
    </div>
  );
}
```

---

## Configuration

### appsettings.json

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
    "RedirectUri": "https://api.yourapp.com/api/auth/google/callback"
  },
  "Frontend": {
    "BaseUrl": "https://your-frontend.com"
  }
}
```

### Google Cloud Console Setup

1. Go to [Google Cloud Console](https://console.cloud.google.com/)
2. Create a new project or select existing
3. Enable **Google+ API** (or **Google Identity Toolkit API**)
4. Go to **Credentials** → **Create Credentials** → **OAuth client ID**
5. Configure consent screen:
   - Add scopes: `openid`, `email`, `profile`
   - Add test users if in testing mode
6. Create OAuth client ID:
   - Application type: **Web application**
   - Authorized redirect URIs:
     - `https://api.yourapp.com/api/auth/google/callback`
     - `http://localhost:5000/api/auth/google/callback` (for dev)

---

## Security Considerations

### Production Checklist

- [ ] Use HTTPS only (cookies have `Secure` flag)
- [ ] Set strong JWT secret (32+ bytes)
- [ ] Configure CORS properly
- [ ] Rotate JWT secret periodically
- [ ] Monitor for suspicious token usage
- [ ] Implement rate limiting on auth endpoints
- [ ] Set up session cleanup job (remove expired sessions)
- [ ] Enable audit logging for auth events

### Common Vulnerabilities Prevented

| Attack | Prevention |
|--------|------------|
| **XSS** | HttpOnly cookies (JS can't read tokens) |
| **CSRF** | SameSite=Strict, state parameter |
| **Token Theft** | Short-lived access tokens, rotation |
| **Code Interception** | PKCE code verifier |
| **Open Redirect** | Redirect URI validation |
| **Replay Attack** | JWT jti claim, token rotation |

### Token Reuse Detection

If a refresh token is used twice (indicating potential theft), the system:
1. Revokes ALL sessions for that user
2. Requires re-authentication
3. Logs the security event

---

## Database Migration

Create migration for UserSession entity:

```bash
cd MyApp.Infrastructure
dotnet ef migrations add AddUserSessions --startup-project ../MyApp.API
dotnet ef database update --startup-project ../MyApp.API
```

---

## Testing

### Manual Testing Checklist

1. **Login Flow**
   - [ ] Click login → redirected to Google
   - [ ] Complete Google auth → redirected back
   - [ ] Check cookies are set (DevTools → Application → Cookies)
   - [ ] Call `/api/auth/me` → returns user info

2. **Token Refresh**
   - [ ] Wait for access token to expire (or modify expiry time)
   - [ ] Make API call → should auto-refresh
   - [ ] Check new cookies are set

3. **Logout**
   - [ ] Call logout → cookies cleared
   - [ ] Call `/api/auth/me` → returns 401

4. **Security**
   - [ ] Try CSRF attack (different origin) → should fail
   - [ ] Try XSS (steal cookie via JS) → should fail (HttpOnly)
   - [ ] Try token reuse → should revoke all sessions

---

## Troubleshooting

### Common Issues

**Issue: Cookies not being set**
- Check `Secure` flag - must use HTTPS in production
- Check `SameSite` - may need `Lax` for OAuth redirects
- Check domain - must match between API and frontend

**Issue: 401 after login**
- Check cookie is being sent (DevTools → Network)
- Check `credentials: 'include'` in fetch requests
- Check CORS allows credentials

**Issue: CORS errors**
- Ensure `Access-Control-Allow-Credentials: true`
- Ensure `Access-Control-Allow-Origin` is not `*`
- Must specify exact origin when using credentials

---

## Migration from Old Flow

If upgrading from the old token-in-body flow:

1. **Backend Changes** (Done)
   - ✅ New endpoints added
   - ✅ Cookie-based auth implemented
   - ✅ Session management added

2. **Frontend Changes** (Required)
   - Remove `Authorization` header handling
   - Add `credentials: 'include'` to all requests
   - Update login to use `window.location.href`
   - Add `/auth/success` and `/auth/error` pages
   - Remove token from localStorage/sessionStorage

3. **Gradual Migration** (Optional)
   - Support both auth methods temporarily
   - Deprecate old endpoint with warning
   - Remove old endpoint after all clients updated

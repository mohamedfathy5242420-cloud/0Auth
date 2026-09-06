# OAuth 2.0 Demo Application with Google Login

## Project Structure
OAuthDemo.Api/
- Controllers/ - API controllers for authentication and profile
- Services/ - GoogleOAuthService, JwtTokenService, configuration classes
- Data/ - ApplicationDbContext for Entity Framework Core
- Entities/ - User entity
- Configuration/ - GoogleOAuthSettings, JwtSettings
- appsettings.json - Application configuration

## What is OAuth 2.0? (1/11)

OAuth 2.0 is the industry-standard protocol for authorization. It enables a third-party application to obtain limited access to an HTTP service, either on behalf of a resource owner by user credentials, or by allowing the third-party application to obtain access on its own behalf.

**Key points:**
- OAuth 2.0 delegates authorization, not authentication
- It provides authorization flows for web applications, desktop applications, mobile phones, and consoles
- The most common flow for web applications is the Authorization Code Flow
- OAuth 2.0 tokens are credentials used to access resources, not identity tokens

## What is OpenID Connect? (2/11)

OpenID Connect (OIDC) is a simple identity layer built on top of OAuth 2.0. It allows clients to verify the identity of an end-user based on the authentication performed by an authorization server.

**Key points:**
- OIDC adds ID token (JWT) to the OAuth 2.0 flow
- The ID token contains verified user identity information
- OIDC defines three token types: access_token, ID token, and refresh_token
- It enables "Login with Google", "Login with Facebook", etc.
- OIDC is backward-compatible with OAuth 2.0

## Why Google Login Uses OAuth 2.0 + OpenID Connect (3/11)

Google's authentication uses OAuth 2.0 + OpenID Connect because:

- **OAuth 2.0** handles the authorization flow - getting user consent to access their data
- **OpenID Connect** adds the identity layer - verifying who the user is
- Together, they provide a standardized way to:
  - Redirect users to Google's login page
  - Receive an authorization code
  - Exchange the code for verified tokens
  - Get user profile information (name, email, picture)
  - Authenticate users without handling passwords

Google's implementation strictly separates:
- **Authorization** (OAuth 2.0): Granting access to resources
- **Authentication** (OIDC): Verifying user identity

## OAuth 2.0 Flow Diagram (4/11)

```
Client                                                    Google Authorization Server
  |                                                         |
  | GET /api/auth/google-login                              |
  |--------------------------------------------------------->|
  |                                                     | Generate state CSRF token
  |                                                     | Redirect to consent screen
  |                                                         |
  | <-- 302 Redirect with auth URL -------------------------||
  |                                                         |
  | User logs in at Google, grants permission              |
  |                                                         |
  | GET /api/auth/google-callback?code=AUTH_CODE&state=STATE|
  |--------------------------------------------------------->|
  |                                                         |
  |                                                     | Verify state token (CSRF protection)
  |                                                     | Exchange code for tokens
  |                                                         |
  | <-- 302 with access_token, id_token ---------------------|
  |                                                         |
  | Read user identity from ID token                       |
  | Create/update local user in SQL Server                |
  | Generate application JWT                               |
  |--------------------------------------------------------->|
  |                                                         |
  | GET /api/auth/me (Authorization: Bearer <our-jwt>)     |
  |--------------------------------------------------------->|
```

## Request and Response Flow Explained (5/11)

### 1. GET /api/auth/google-login
**Client → Backend:**
- Frontend calls this endpoint to initiate Google login
- Backend generates a random `state` parameter for CSRF protection
- Backend redirects user to Google's authorization endpoint

**Response:**
- JSON: `{"AuthorizationUrl": "https://accounts.google.com/o/oauth2/v2/auth?..."}`
- Frontend redirects user's browser to this URL

### 2. Google Authorization Endpoint
**User → Google:**
- User sees Google's consent screen
- User enters Google credentials and grants permissions
- Google redirects back to our `redirect_uri` with `code` and `state` parameters

**Backend → User:**
- The browser automatically follows the redirect
- Our `/api/auth/google-callback` endpoint receives the code and state

### 3. GET /api/auth/google-callback
**Backend processing:**
1. Validate the `state` parameter against the one we generated (CSRF protection)
2. Exchange the `authorization_code` for tokens at Google's token endpoint
   - POST to `https://oauth2.googleapis.com/token`
   - Include: `code`, `client_id`, `client_secret`, `redirect_uri`, `grant_type=authorization_code`
3. Google responds with:
   - `access_token`: For accessing Google APIs
   - `id_token`: JWT containing verified user identity (OpenID Connect)
   - `expires_in`: Token expiration time (seconds)
   - `refresh_token`: Optional, to get new tokens when expired
4. Verify and read the `id_token` to get user information
5. Create or update user in local SQL Server database
6. Generate our application's JWT token
7. Return JWT + user info to client

### 4. GET /api/auth/me (Protected)
**Client → Backend:**
- Frontend sends: `Authorization: Bearer <our_application_jwt>`
- Backend validates the JWT token
- Returns the currently logged-in user information

**Response:**
- JSON with user: `Id`, `Email`, `Name`, `GoogleId`, `ProfilePictureUrl`, `CreatedAt`, `LastLoginAt`

### 5. GET /api/profile (Protected)
**Client → Backend:**
- Frontend sends: `Authorization: Bearer <our_application_jwt>`
- Backend validates the JWT token
- Returns protected profile information

**Response:**
- JSON: `{"Message": "Access granted with valid JWT token.", "UserId": "...", "Email": "..."}`

## Key Differences (6/11)

```
Authorization Code  ≠  Access Token  ≠  ID Token  ≠  Application JWT
```

- **Authorization Code**: Short-lived code received from Google after user consent. Exchanged for tokens server-side. Never exposed to frontend.
- **Access Token**: Google's token for accessing Google APIs (people API, profile API). Short-lived (typically 1 hour). NOT returned to client in our app.
- **ID Token**: Google's JWT containing verified user identity (sub, email, name, picture). Signed by Google. Used only server-side to get user info.
- **Application JWT**: OUR application's JWT token generated AFTER Google authentication. Used to protect our APIs. Contains our claims (user ID, email, issuer, audience). Short-lived (60 minutes by default).

**Summary:** Google handles OAuth 2.0 + OIDC. We use only the ID token server-side to identify the user, then generate OUR JWT for API protection.

## What Each Component Handles (7/11)

| Component | Handles |
|-----------|---------|
| **Browser/Frontend** | Redirect user to Google, display consent screen, send authorization code back to backend, send JWT token in Authorization header |
| **Our Backend** | Generate OAuth state CSRF token, exchange authorization code for tokens, verify ID token, create/update user in DB, generate application JWT, validate JWT on protected endpoints |
| **Google Authorization Server** | Authenticate user, obtain consent, generate authorization code, issue access_token + ID token + refresh_token, validate client credentials |
| **Google Resource Server** | Access tokens protect Google APIs (people, profile, email). Return user profile data when accessed with valid access token |

## Creating Google OAuth Credentials (8/11)

### Step-by-step from Google Cloud Console:

1. Go to [Google Cloud Console](https://console.cloud.google.com/)
2. Select your project or create a new one
3. Navigate to **APIs & Services → Credentials**
4. Click **Create credentials → OAuth client ID**
5. For "Application type", select **Web application**
6. Enter a name (e.g., "OAuthDemo")
7. **Authorized redirect URIs**: Add `https://localhost:XXXX/api/auth/google-callback`
   - Replace `XXXX` with your port number (use the port from your launchSettings.json)
   - Important: The redirect URI must exactly match what's configured in Google Cloud Console
8. Click Create
9. Note the **Client ID** and **Client Secret**
10. Add these to `appsettings.json` under `GoogleOAuth` section, or environment variables

**Important security notes:**
- Keep `ClientSecret` secure - never expose it in frontend code
- Use environment variables or user secrets for production
- The redirect URI must match exactly (including protocol, host, and path)
- Restrict authorized origins if needed

## Configuring Google Redirect URI (9/11)

The Google OAuth redirect URI must be configured in **two places**:

1. **Google Cloud Console**:
   - Go to Credentials → OAuth 2.0 Client IDs
   - Add under "Authorized redirect URIs":
     `https://localhost:XXXX/api/auth/google-callback`
   - Must include `https://` protocol
   - Must match the port your application runs on

2. **appsettings.json** (in our application):
   ```json
   "GoogleOAuth": {
     "ClientId": "YOUR_GOOGLE_CLIENT_ID",
     "ClientSecret": "YOUR_GOOGLE_CLIENT_SECRET",
     "RedirectUri": "https://localhost:XXXX/api/auth/google-callback"
   }
   ```

**Why it matters:**
- The redirect URI is the endpoint where Google sends the user after they authorize
- If the URIs don't match exactly, Google will reject the request with `redirect_uri_mismatch` error
- In production, use `https://your-production-domain.com/api/auth/google-callback`

## Commands (10/11)

### dotnet restore
```bash
dotnet restore
```
- Restores all NuGet packages referenced in the project
- Required after adding new packages or cloning the project

### Creating EF Core Migration
```bash
# Add a new migration (describes changes to the database)
dotnet ef migrations add InitialCreate

# Or from the package manager console:
Add-Migration InitialCreate
```
- Creates a new migration file based on the DbContext configuration
- The `InitialCreate` migration sets up the `Users` table

### Updating the Database
```bash
# Apply pending migrations to the database
dotnet ef database update

# Or from the package manager console:
Update-Database
```
- Creates the database and applies all migrations
- By default, uses the connection string from `appsettings.json`
- For local development, creates `OAuthDemo` database on `(localdb)\MSSQLLocalDB`

### Running the Application
```bash
dotnet run
```
- Builds and starts the ASP.NET Core Web API
- By default, runs on `https://localhost:7288` and `http://localhost:5273`
- Swagger UI available at `/swagger`

### Running with Specific Profile
```bash
dotnet run --environment Development
# or
ASPNETCORE_ENVIRONMENT=Development dotnet run
```

## Swagger Configuration and JWT Testing (11/11)

### Swagger/OpenAPI Configuration

The application uses Swashbuckle.AspNetCore (v6.6.2) for API documentation:

**Swagger UI:** Available at `https://localhost:7288/swagger` (or your port)

**Configuration in Program.cs:**
- Adds Swagger generator with JWT security scheme
- Defines `Bearer` security scheme in the header
- Allows Swagger UI to show authorization dialog

**Security Definition:**
```json
{
  "type": "apiKey",
  "name": "Authorization",
  "in": "header",
  "scheme": "Bearer"
}
```

### Testing Protected Endpoints Using JWT

**1. Obtain a JWT token:**
- Call `GET /api/auth/google-login` to get the Google authorization URL
- Redirect user to the URL
- User logs in with Google
- Call `GET /api/auth/google-callback?code=AUTH_CODE&state=STATE`
- Receive JWT token in response: `{"token": "...", "user": {...}}`

**2. Use the JWT token:**
- Copy the token from the authentication response
- Send requests with: `Authorization: Bearer <jwt_token>`

**3. Test the endpoints:**

**GET /api/auth/me:**
- Protected endpoint
- Returns the currently logged-in application user
- Requires valid JWT in Authorization header

**GET /api/profile:**
- Protected endpoint
- Demonstrates `Authorization: Bearer <jwt>` header usage
- Returns: `{"Message": "Access granted with valid JWT token.", "UserId": "...", "Email": "..."}`

**If the token is invalid/expired:**
- Returns 401 Unauthorized
- Swagger UI will show authentication error

### JWT Token Lifetime

- Our application JWT expires in 60 minutes (configurable via `Jwt:ExpirationMinutes` in appsettings.json)
- After expiration, the user must re-authenticate with Google
- The token contains claims: `NameIdentifier` (user ID), `Email`, ` Issuer`, `Audience`

### Important Security Notes

- **Never expose Google ClientSecret** to the frontend
- **Never expose Google's access token** to the client
- Our application uses its own JWT for API protection, NOT the Google access token
- OAuth state parameter validates against CSRF attacks
- User identity MUST come from Google's ID token, never from user input
- Store secrets using environment variables or user-secrets, not in source code
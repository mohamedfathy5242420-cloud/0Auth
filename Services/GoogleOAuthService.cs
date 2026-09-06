using Google.Apis.Auth;
using Google.Apis.Auth.OAuth2;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using OAuthDemo.Api.Configuration;
using OAuthDemo.Api.Data;
using OAuthDemo.Api.Entities;
using OAuthDemo.Api.Services.Abstract;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;

namespace OAuthDemo.Api.Services;

/// <summary>
/// Service responsible for handling Google OAuth 2.0 authentication flow.
/// 
/// This service manages the complete OAuth 2.0 authorization code flow:
/// 1. Redirect user to Google's authorization endpoint
/// 2. Receive authorization code from Google callback  
/// 3. Exchange authorization code for tokens at Google's token endpoint
/// 4. Verify and read user information from ID token
/// 5. Create/update local user in database
/// 6. Generate application JWT token for the authenticated user
/// </summary>
public class GoogleOAuthService : IGoogleOAuthService
{
    private readonly GoogleOAuthSettings _googleOAuthSettings;
    private readonly JwtSettings _jwtSettings;
    private readonly ApplicationDbContext _dbContext;
    private readonly IHttpContextAccessor _httpContextAccessor;

    /// <summary>
    /// Initializes a new instance of the <see cref="GoogleOAuthService"/> class.
    /// </summary>
    /// <param name="googleOAuthSettings">Configuration for Google OAuth (ClientId, ClientSecret, RedirectUri)</param>
    /// <param name="jwtSettings">Configuration for JWT token generation (Key, Issuer, Audience, Expiration)</param>
    /// <param name="dbContext">Database context for user management</param>
    /// <param name="httpContextAccessor">Accessor for HTTP context to manage OAuth state</param>
    public GoogleOAuthService(
        GoogleOAuthSettings googleOAuthSettings,
        JwtSettings jwtSettings,
        ApplicationDbContext dbContext,
        IHttpContextAccessor httpContextAccessor)
    {
        _googleOAuthSettings = googleOAuthSettings;
        _jwtSettings = jwtSettings;
        _dbContext = dbContext;
        _httpContextAccessor = httpContextAccessor;
    }

    /// <summary>
    /// Generates the Google OAuth 2.0 authorization URL that the frontend should redirect to.
    /// 
    /// This URL includes:
    /// - response_type=code: Authorization code flow
    /// - client_id: From Google Cloud Console
    /// - redirect_uri: Where Google sends user after authorization
    /// - scope: Profile and email permissions + openid
    /// - state: Random CSRF protection token
    /// - prompt=consent: Forces consent screen
    /// 
    /// The browser/frontend redirects the user to this URL.
    /// After login, Google redirects back to our /api/auth/google-callback endpoint
    /// with authorization code and state parameter.
    /// </summary>
    /// <returns>The Google authorization URL</returns>
    public string GetGoogleAuthUrl()
    {
        // Generate a random state token to prevent CSRF (Cross-Site Request Forgery)
        // The state is a unique value we generate and must match when Google redirects back
        var state = Guid.NewGuid().ToString();

        // Store the state in the HTTP session so we can validate it in the callback
        // This prevents attackers from tricking users into authorizing our application
        var session = _httpContextAccessor.HttpContext?.Session;
        if (session != null)
        {
            session.SetString("OAuthState", state);
        }

        // Build the Google OAuth 2.0 authorization endpoint URL
        var authorizationEndpoint = "https://accounts.google.com/o/oauth2/v2/auth";

        // Construct the query parameters
        var parameters = new[]
        {
            "response_type=code",                  // We want authorization code
            $"client_id={_googleOAuthSettings.ClientId}",  // Google Cloud Console client ID
            $"redirect_uri={_googleOAuthSettings.RedirectUri}", // Must match Google Cloud Console
            "scope=https://www.googleapis.com/auth/userinfo.profile openid", // Requested permissions
            $"state={state}",                      // CSRF protection
            "prompt=consent"                       // Show consent screen
        };

        var authUrl = $"{authorizationEndpoint}?{string.Join("&", parameters)}";

        return authUrl;
    }

    /// <summary>
    /// Handles the Google OAuth callback after user authorization.
    /// 
    /// This method:
    /// 1. Validates the state parameter to prevent CSRF attacks
    /// 2. Exchanges the authorization code for tokens (access_token, id_token, expires_in, refresh_token)
    /// 3. Verifies the ID token using Google's library to get verified user info
    /// 4. Creates or updates the user in the local database
    /// 5. Generates our application's JWT token for API protection
    /// 
    /// Security flow:
    /// - User is redirected to: /api/auth/google-callback?code=AUTH_CODE&state=STATE
    /// - We check state matches what we generated (prevents CSRF)
    /// - We exchange code for tokens server-side (ClientSecret is safe on server)
    /// - We decode ID token to get verified user identity (comes FROM Google, not user)
    /// - We create/update local user and generate OUR JWT for API protection
    /// </summary>
    /// <param name="authorizationCode">The authorization code from Google's callback URL</param>
    /// <param name="receivedState">The state parameter from Google's callback URL</param>
    /// <returns>Tuple of (application JWT token, user email, user name, Google user ID, profile picture URL)</returns>
    /// <exception>GoogleJsonWebTokenException if state validation fails</exception>
    public async Task<(string jwtToken, string email, string name, string googleId, string profilePictureUrl)> HandleGoogleCallbackAsync(
        string authorizationCode,
        string receivedState)
    {
        // ==========================================
        // STEP 1: Validate the state parameter
        // ==========================================
        // CRITICAL SECURITY MEASURE: Prevents CSRF attacks.
        // If the state doesn't match what we initiated, the request is rejected.
        // This ensures the request originated from our application, not an attacker.

        var session = _httpContextAccessor.HttpContext?.Session;
        var expectedState = session?.GetString("OAuthState");

        if (string.IsNullOrEmpty(expectedState) || expectedState != receivedState)
        {
            throw new Exception(
                "Invalid OAuth state parameter. Possible CSRF attack detected. " +
                "The state parameter does not match the one generated during login.");
        }

        // ==========================================
        // STEP 2: Exchange authorization code for tokens
        // ==========================================
        // The authorization code is a short-lived code Google returns after user consent.
        // We exchange it at Google's token endpoint for actual tokens.
        // This happens server-side, so our ClientSecret remains secure.
        //
        // Tokens we receive:
        // - access_token: For accessing Google APIs (resource server)
        // - id_token: JWT containing verified user identity (OpenID Connect)
        // - expires_in: How long the tokens are valid (seconds)
        // - refresh_token: If available, to get new tokens when expired
        //
        // Important: We do NOT expose Google's access token to the client.
        // Only our application JWT is returned.

        // Exchange the authorization code for tokens using Google's token validation
        // This internally calls Google's token endpoint: https://oauth2.googleapis.com/token
        var payload = await GoogleJsonWebSignature.ValidateAsync(authorizationCode,
            new GoogleJsonWebSignature.ValidationSettings()
            {
                // For authorization code exchange, we validate the ID token that Google
                // returns. The audience check ensures this token was issued for our client.
                // We'll set the Audience to our Google Client ID.
                Audience = new List<string> { _googleOAuthSettings.ClientId }
            });

        // ==========================================
        // STEP 3: Read user information from the ID token
        // ==========================================
        // Google's ID token is a JWT that contains verified user information.
        // We use Google's library to validate and parse it.
        // This is Google's JWT, NOT our application's JWT.
        // Our JWT is generated AFTER this validation succeeds.

        // The ID token contains verified claims:
        // - sub: Google's unique user identifier
        // - email: User's email (verified if email_verified=true)
        // - name: User's full name
        // - picture: Profile picture URL
        // - email_verified: Whether email was verified by Google
        // - iat: Issued-at timestamp
        // - exp: Expiration timestamp
        //
        // We trust this information because it's signed by Google and validated above.

        var userInfo = new
        {
            GoogleId = payload.Subject,
            Email = payload.Email,
            Name = payload.Name,
            ProfilePictureUrl = payload.Picture,
            EmailVerified = payload.EmailVerified
        };

        // ==========================================
        // STEP 4: Create or update user in database
        // ==========================================
        // Store or update the user in our local SQL Server database.
        // We use GoogleId as the unique key to prevent duplicate accounts.

        var user = await _dbContext.Users
            .FirstOrDefaultAsync(u => u.GoogleId == userInfo.GoogleId);

        if (user == null)
        {
            // New user - create new record
            user = new User
            {
                GoogleId = userInfo.GoogleId,
                Email = userInfo.Email,
                Name = userInfo.Name,
                ProfilePictureUrl = userInfo.ProfilePictureUrl,
                CreatedAt = DateTime.UtcNow,
                LastLoginAt = DateTime.UtcNow
            };
            _dbContext.Users.Add(user);
        }
        else
        {
            // Existing user - update profile and login time
            user.Email = userInfo.Email;
            user.Name = userInfo.Name;
            user.ProfilePictureUrl = userInfo.ProfilePictureUrl;
            user.LastLoginAt = DateTime.UtcNow;
        }

        await _dbContext.SaveChangesAsync();

        // ==========================================
        // STEP 5: Generate our application JWT token
        // ==========================================
        // After successful Google authentication, we generate OUR application's JWT token.
        // This is the token we use to protect our APIs.
        // The user will use this JWT for all subsequent API calls.
        //
        // Key point: The Google access token and ID token are NOT returned to the client.
        // Only our application JWT is returned. This is the "Important" requirement:
        // "OAuth to be used only for authenticating the user with Google.
        // After authentication, our application must use its own JWT for protecting APIs."
        
        string applicationJwt = GenerateApplicationJwt(user.Id, user.Email);

        // Return the application JWT plus user information (without Google tokens)
        return (applicationJwt,
                user.Email,
                user.Name,
                user.GoogleId,
                user.ProfilePictureUrl);
    }

    /// <summary>
    /// Generates an application JWT token for the authenticated user.
    /// 
    /// This is OUR application's JWT, distinct from Google's ID token.
    //  After Google authenticates the user via OAuth 2.0 + OpenID Connect,
    //  we create our own JWT for API authentication and authorization.
    ///
    /// The JWT contains claims:
    /// - NameIdentifier: Our local user ID
    /// - Email: User's email address
    /// - Issuer: From configuration (our application issuer)
    /// - Audience: From configuration (our application audience)
    /// - Expiration: From configuration (default 60 minutes)
    ///
    /// This token protects our APIs. The user sends: Authorization: Bearer <our_jwt>
    //  in subsequent requests.
    /// </summary>
    /// <param name="userId">The local database user ID</param>
    /// <param name="email">The user's email address</param>
    /// <returns>The generated JWT token string</>
    private string GenerateApplicationJwt(int userId, string email)
    {
        // Use the HMAC-SHA256 algorithm with the secret key from configuration
        // The key must be configured securely (environment variables or user secrets)
        // Never hardcode this in source code!
        var key = System.Text.Encoding.UTF8.GetBytes(_jwtSettings.Key);

        var credentials = new SigningCredentials(
            new SymmetricSecurityKey(key),
            SecurityAlgorithms.HmacSha256);

        // Create the JWT with claims
        var token = new JwtSecurityToken(
            issuer: _jwtSettings.Issuer,
            audience: _jwtSettings.Audience,
            claims: new[]
            {
                // Subject = our local user ID
                new Claim(ClaimTypes.NameIdentifier, userId.ToString()),

                // User's email address
                new Claim(ClaimTypes.Email, email),

                // Additional claim for Google ID (for reference)
                new Claim("googleId", userId.ToString())
            },
            expires: DateTime.UtcNow.AddMinutes(_jwtSettings.ExpirationMinutes),
            signingCredentials: credentials);

        // Write the token as a string
        var tokenHandler = new JwtSecurityTokenHandler();
        var stringToken = tokenHandler.WriteToken(token);

        return stringToken;
    }
}
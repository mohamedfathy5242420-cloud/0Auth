using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using OAuthDemo.Api.Configuration;
using OAuthDemo.Api.Data;
using OAuthDemo.Api.Entities;
using OAuthDemo.Api.Services;
using OAuthDemo.Api.Services.Abstract;

namespace OAuthDemo.Api.Controllers;

/// <summary>
/// Authentication controller handling Google OAuth 2.0 login flow and user management.
/// 
/// Endpoints:
/// - GET /api/auth/google-login: Redirects user to Google's authorization page
/// - GET /api/auth/google-callback: Handles Google's redirect back with authorization code
/// - GET /api/auth/me: Returns the currently logged-in user (protected by our JWT)
/// </summary>
[ApiController]
[Route("api/auth")]
public class AuthController : ControllerBase
{
    private readonly IGoogleOAuthService _googleOAuthService;
    private readonly JwtTokenService _jwtTokenService;

    /// <summary>
    /// Initializes a new instance of the <see cref="AuthController"/> class.
    /// </summary>
    /// <param name="googleOAuthService">Service for handling Google OAuth flow</param>
    /// <param name="jwtTokenService">Service for generating and validating application JWT tokens</param>
    public AuthController(IGoogleOAuthService googleOAuthService, JwtTokenService jwtTokenService)
    {
        _googleOAuthService = googleOAuthService;
        _jwtTokenService = jwtTokenService;
    }

    /// <summary>
    /// Initiates the Google OAuth 2.0 login flow.
    /// 
    /// The user is redirected to Google's authorization endpoint where they can
    /// sign in and grant permissions. After authorization, Google redirects back
    /// to /api/auth/google-callback with an authorization code.
    ///
    /// OAuth 2.0 Authorization Code Flow:
    /// 1. Frontend calls this endpoint
    /// 2. Backend generates a random state parameter for CSRF protection
    /// 3. Backend redirects user to Google's authorization URL
    /// 4. User logs in and grants permission
    /// 5. Google redirects back to /api/auth/google-callback?code=AUTH_CODE&state=STATE
    /// </summary>
    /// <returns>Google authorization URL - frontend should redirect user to this</returns>
    [HttpGet("google-login")]
    public IActionResult GoogleLogin()
    {
        // Generate the Google authorization URL with CSRF protection (state parameter)
        var authUrl = _googleOAuthService.GetGoogleAuthUrl();

        // Return the URL to the frontend
        // The frontend should redirect the user's browser to this URL
        return Ok(new { AuthorizationUrl = authUrl });
    }

    /// <summary>
    /// Handles the Google OAuth 2.0 callback after user authorization.
    /// 
    /// This endpoint receives the authorization code from Google after the user
    /// logs in and grants permission. The backend then:
    /// 1. Validates the state parameter (CSRF protection)
    /// 2. Exchanges the authorization code for tokens (access_token, id_token, etc.)
    /// 3. Verifies the ID token and retrieves user information
    /// 4. Creates or updates the user in the local database
    /// 5. Generates our application's JWT token for API protection
    /// 
    /// The flow is:
    /// - User redirected from Google to: /api/auth/google-callback?code=AUTH_CODE&state=STATE
    /// - State validated against what was generated in GoogleLogin()
 /// - Authorization code exchanged for Google ID token
 /// - User info extracted from verified ID token
 /// - Local user created/updated in SQL Server
 /// - Our application JWT generated and returned
 /// </summary>
    /// <param name="authorizationCode">The authorization code from Google's callback URL</param>
    /// <param name="state">The state parameter from Google's callback URL</param>
    /// <returns>Application JWT token and user information</>
    [HttpGet("google-callback")]
    public async Task<IActionResult> GoogleCallback(
        [FromQuery] string code,
        [FromQuery] string state)
    {
        if (string.IsNullOrEmpty(code))
        {
            return BadRequest(new { Error = "Authorization code is missing." });
        }

        if (string.IsNullOrEmpty(state))
        {
            return BadRequest(new { Error = "State parameter is missing." });
        }

        // Handle the OAuth callback - exchange code for tokens, create user, generate JWT
        var result = await _googleOAuthService.HandleGoogleCallbackAsync(code, state);

        // Return success with the application JWT and user info
        // The client can use this JWT to authenticate subsequent API requests
        return Ok(new
        {
            Message = "Google OAuth authentication successful.",
            Token = result.jwtToken,
            User = new
            {
                result.email,
                result.name,
                result.googleId,
                result.profilePictureUrl
            }
        });
    }

    /// <summary>
    /// Returns the currently logged-in application user.
    /// 
    /// This endpoint is protected by our application's JWT token.
    /// The token must be sent in the Authorization: Bearer <jwt> header.
    /// 
    /// After Google OAuth authenticates the user, we generate our own JWT.
    /// This endpoint uses that JWT to identify the user in our system.
    /// </summary>
    /// <returns>The current application user information</>
    [HttpGet("me")]
    [Authorize]
    public IActionResult Me()
    {
        // User is authenticated via our JWT token
        // The [Authorize] attribute validates the JWT and populates User identity
        
        // Get the user ID from the JWT claim
        var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        var emailClaim = User.FindFirst(ClaimTypes.Email)?.Value;

        if (string.IsNullOrEmpty(userIdClaim) || string.IsNullOrEmpty(emailClaim))
        {
            return Unauthorized(new { Error = "User claims not found in token." });
        }

        var userId = int.Parse(userIdClaim);

        // Retrieve the user from the database
        var user = _jwtTokenService.GetUserById(userId);

        if (user == null)
        {
            return NotFound(new { Error = "User not found in database." });
        }

        return Ok(new
        {
            Id = user.Id,
            Email = user.Email,
            Name = user.Name,
            GoogleId = user.GoogleId,
            ProfilePictureUrl = user.ProfilePictureUrl,
            CreatedAt = user.CreatedAt,
            LastLoginAt = user.LastLoginAt
        });
    }
}
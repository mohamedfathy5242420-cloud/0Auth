using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace OAuthDemo.Api.Controllers;

/// <summary>
/// Profile controller demonstrating bearer token authentication.
/// 
/// This endpoint requires a valid JWT token sent via:
/// Authorization: Bearer <jwt>
///
/// After Google OAuth authentication, our application generates its own JWT token.
/// This token is used to protect API endpoints, not the Google access token.
/// </summary>
[ApiController]
[Route("api/profile")]
public class ProfileController : ControllerBase
{
    /// <summary>
    /// Returns protected profile information.
    /// 
    /// This endpoint demonstrates how to use our application's JWT for authentication.
    /// The client must send: Authorization: Bearer <our_application_jwt>
    /// 
    /// This is NOT the Google access token or ID token - it's our application's JWT
    /// that was generated after successful Google OAuth authentication.
    /// </summary>
    [HttpGet]
    [Authorize]
    public IActionResult GetProfile()
    {
        // User is authenticated via our JWT token
        // The [Authorize] attribute validates the token and sets User identity
        
        var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        var emailClaim = User.FindFirst(ClaimTypes.Email)?.Value;

        if (string.IsNullOrEmpty(userIdClaim) || string.IsNullOrEmpty(emailClaim))
        {
            return Unauthorized(new { Error = "User claims not found in token." });
        }

        return Ok(new
        {
            Message = "Access granted with valid JWT token.",
            UserId = userIdClaim,
            Email = emailClaim,
            AuthenticatedAt = DateTime.UtcNow
        });
    }
}
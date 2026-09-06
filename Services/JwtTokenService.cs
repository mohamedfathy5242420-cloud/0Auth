using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using OAuthDemo.Api.Configuration;
using OAuthDemo.Api.Data;
using OAuthDemo.Api.Entities;

namespace OAuthDemo.Api.Services;

/// <summary>
/// Service responsible for generating and validating application JWT tokens.
/// </summary>
public class JwtTokenService
{
    private readonly JwtSettings _jwtSettings;
    private readonly ApplicationDbContext _dbContext;

    /// <summary>
    /// Initializes a new instance of the <see cref="JwtTokenService"/> class.
    /// </summary>
    /// <param name="jwtSettings">JWT configuration (Key, Issuer, Audience, Expiration)</param>
    /// <param name="dbContext">Database context for user lookups</param>
    public JwtTokenService(JwtSettings jwtSettings, ApplicationDbContext dbContext)
    {
        _jwtSettings = jwtSettings;
        _dbContext = dbContext;
    }

    /// <summary>
    /// Generates a JWT token for the specified user.
    /// </summary>
    /// <param name="userId">The local user ID from the database</param>
    /// <param name="email">The user's email address</param>
    /// <returns>The generated JWT token string</returns>
    public string GenerateToken(int userId, string email)
    {
        var key = Encoding.UTF8.GetBytes(_jwtSettings.Key);

        var credentials = new SigningCredentials(
            new SymmetricSecurityKey(key),
            SecurityAlgorithms.HmacSha256);

        var token = new JwtSecurityToken(
            issuer: _jwtSettings.Issuer,
            audience: _jwtSettings.Audience,
            claims: new[]
            {
                new Claim(ClaimTypes.NameIdentifier, userId.ToString()),
                new Claim(ClaimTypes.Email, email),
                new Claim("createdAt", DateTime.UtcNow.ToString("o"))
            },
            expires: DateTime.UtcNow.AddMinutes(_jwtSettings.ExpirationMinutes),
            signingCredentials: credentials);

        var tokenHandler = new JwtSecurityTokenHandler();
        return tokenHandler.WriteToken(token);
    }

    /// <summary>
    /// Validates a JWT token and returns the user ID if valid.
    /// </summary>
    /// <param name="token">The JWT token string to validate</param>
    /// <returns>The user ID if valid, 0 if invalid</returns>
    public int? ValidateToken(string token)
    {
        if (string.IsNullOrEmpty(token))
        {
            return null;
        }

        var key = Encoding.UTF8.GetBytes(_jwtSettings.Key);

        var tokenHandler = new JwtSecurityTokenHandler();

        try
        {
            tokenHandler.ValidateToken(token,
                new TokenValidationParameters
                {
                    ValidateIssuerSigningKey = true,
                    IssuerSigningKey = new SymmetricSecurityKey(key),
                    ValidIssuer = _jwtSettings.Issuer,
                    ValidAudience = _jwtSettings.Audience,
                    ValidateLifetime = true,
                    // When validating lifetime, we allow some time skew for clock differences
                    ClockSkew = TimeSpan.Zero
                },
                out SecurityToken validatedToken);

            var jwtToken = validatedToken as JwtSecurityToken;
            if (jwtToken == null)
            {
                return null;
            }

            // Return the user ID from the NameIdentifier claim
            var userId = jwtToken.Claims.FirstOrDefault(c => c.Type == ClaimTypes.NameIdentifier)?.Value;
            if (int.TryParse(userId, out int parsedUserId))
            {
                return parsedUserId;
            }

            return null;
        }
        catch
        {
            return null;
        }
    }

    /// <summary>
    /// Gets the user from the database by their ID.
    /// </summary>
    /// <param name="userId">The user ID to look up</param>
    /// <returns>The user entity, or null if not found</>
    public User? GetUserById(int userId)
    {
        return _dbContext.Users.Find(userId);
    }
}
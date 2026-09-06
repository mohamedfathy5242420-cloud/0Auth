namespace OAuthDemo.Api.Configuration;

/// <summary>
/// Configuration settings for JWT (JSON Web Token) generation and validation.
/// </summary>
public class JwtSettings
{
    /// <summary>
    /// The secret key used to sign the JWT token.
    /// Must be at least 16, 24, or 32 bytes long for HMAC-SHA256/384/512.
    /// Store this value in environment variables or user secrets, NOT in source code.
    /// </summary>
    public string Key { get; set; } = string.Empty;

    /// <summary>
    /// The issuer of the JWT token. Typically the application's URL or identifier.
    /// </summary>
    public string Issuer { get; set; } = string.Empty;

    /// <summary>
    /// The audience (client) for the JWT token. The recipients that process the token.
    /// </summary>
    public string Audience { get; set; } = string.Empty;

    /// <summary>
    /// The expiration time for the JWT token in minutes.
    /// After this time, the token is invalid and a new login is required.
    /// </summary>
    public int ExpirationMinutes { get; set; } = 60;
}
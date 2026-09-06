namespace OAuthDemo.Api.Services.Abstract;

/// <summary>
/// Interface for Google OAuth 2.0 authentication service.
/// </summary>
public interface IGoogleOAuthService
{
    /// <summary>
    /// Generates the Google OAuth 2.0 authorization URL.
    /// The frontend should redirect the user to this URL.
    /// </summary>
    /// <returns>Google authorization URL</returns>
    string GetGoogleAuthUrl();

    /// <summary>
    /// Handles the Google OAuth callback after user authorization.
    /// Validates the state parameter, exchanges the authorization code for tokens,
    /// retrieves user information, creates/updates the local user, and generates
    /// our application's JWT token.
    /// </summary>
    /// <param name="authorizationCode">The authorization code from Google's callback</param>
    /// <param name="receivedState">The state parameter from Google's callback</param>
    /// <returns>Tuple containing (application JWT, user email, user name, Google ID, profile picture URL)</returns>
    Task<(string jwtToken, string email, string name, string googleId, string profilePictureUrl)> HandleGoogleCallbackAsync(
        string authorizationCode,
        string receivedState);
}
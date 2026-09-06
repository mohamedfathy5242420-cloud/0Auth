namespace OAuthDemo.Api.Configuration;

/// <summary>
/// Configuration settings for Google OAuth 2.0 authentication.
/// </summary>
public class GoogleOAuthSettings
{
    /// <summary>
    /// The Google OAuth client ID obtained from Google Cloud Console.
    /// This identifies the application to Google's authorization server.
    /// </summary>
    public string ClientId { get; set; } = string.Empty;

    /// <summary>
    /// The Google OAuth client secret obtained from Google Cloud Console.
    /// This is used to authenticate the application with Google's token endpoint.
    /// Never expose this in client-side code or frontend.
    /// </summary>
    public string ClientSecret { get; set; } = string.Empty;

    /// <summary>
    /// The redirect URI where Google will send the user after authorization.
    /// Must exactly match one of the authorized redirect URIs configured in Google Cloud Console.
    /// </summary>
    public string RedirectUri { get; set; } = string.Empty;
}
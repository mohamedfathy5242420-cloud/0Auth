namespace OAuthDemo.Api.Entities;

/// <summary>
/// Represents a user in the application database.
/// This user is created/updated when they authenticate via Google OAuth 2.0.
/// After authentication, the application uses its own JWT for authentication,
/// not the Google access token directly.
/// </summary>
public class User
{
    /// <summary>
    /// Primary key for the user.
    /// </summary>
    public int Id { get; set; }

    /// <summary>
    /// The user's email address. Unique identifier for the application user.
    /// Comes from Google's ID token during OAuth login.
    /// </summary>
    public string Email { get; set; } = string.Empty;

    /// <summary>
    /// The user's full name. Comes from Google's profile information.
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// The Google user ID. Unique identifier from Google's OAuth system.
    /// Used to identify the user across sessions and prevent duplicate accounts.
    /// </summary>
    public string GoogleId { get; set; } = string.Empty;

    /// <summary>
    /// URL to the user's profile picture from Google.
    /// </summary>
    public string ProfilePictureUrl { get; set; } = string.Empty;

    /// <summary>
    /// The date and time when the user account was first created.
    /// Set automatically when the user first logs in via Google.
    /// </summary>
    public DateTime CreatedAt { get; set; }

    /// <summary>
    /// The date and time of the user's last login.
    /// Updated every successful Google OAuth login.
    /// </summary>
    public DateTime LastLoginAt { get; set; }
}
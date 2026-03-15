namespace MyApp.Domain.Entities;

/// <summary>
/// User entity representing an authenticated user.
/// Supports both regular registration and OAuth providers.
/// </summary>
public class User
{
    public Guid Id { get; private set; }
    public string Email { get; private set; } = null!;
    public string FirstName { get; private set; } = null!;
    
    /// <summary>
    /// Last name is optional to accommodate OAuth providers 
    /// that may not provide this field (e.g., some Google accounts)
    /// </summary>
    public string? LastName { get; private set; }
    
    public string GoogleId { get; private set; } = null!;
    public string? ProfilePictureUrl { get; private set; }
    public DateTime CreatedAt { get; private set; }
    public DateTime LastLoginAt { get; private set; }

    // EF Core requires a parameterless constructor
    private User() { }

    /// <summary>
    /// Creates a new user with OAuth credentials.
    /// </summary>
    /// <param name="email">Required. User's email address.</param>
    /// <param name="firstName">Required. User's first/given name.</param>
    /// <param name="lastName">Optional. User's last/family name.</param>
    /// <param name="googleId">Required. Google's unique identifier for this user.</param>
    /// <param name="profilePictureUrl">Optional. URL to user's profile picture.</param>
    /// <exception cref="ArgumentException">Thrown when required fields are null or whitespace.</exception>
    public static User CreateFromOAuth(
        string email,
        string firstName,
        string? lastName,
        string googleId,
        string? profilePictureUrl)
    {
        if (string.IsNullOrWhiteSpace(email))
            throw new ArgumentException("Email is required", nameof(email));
        
        if (string.IsNullOrWhiteSpace(firstName))
            throw new ArgumentException("First name is required", nameof(firstName));
        
        if (string.IsNullOrWhiteSpace(googleId))
            throw new ArgumentException("Google ID is required", nameof(googleId));

        var now = DateTime.UtcNow;
        
        return new User
        {
            Id = Guid.NewGuid(),
            Email = email.ToLowerInvariant(),
            FirstName = firstName.Trim(),
            LastName = string.IsNullOrWhiteSpace(lastName) ? null : lastName.Trim(),
            GoogleId = googleId,
            ProfilePictureUrl = profilePictureUrl,
            CreatedAt = now,
            LastLoginAt = now
        };
    }

    /// <summary>
    /// Updates the user's profile information.
    /// Only updates fields that are provided (non-null and non-whitespace).
    /// </summary>
    public void UpdateProfile(string? firstName, string? lastName, string? profilePictureUrl)
    {
        if (!string.IsNullOrWhiteSpace(firstName))
            FirstName = firstName.Trim();
        
        // Allow setting LastName to null if explicitly provided as empty
        // This handles cases where OAuth user removes their last name
        if (lastName != null)
            LastName = string.IsNullOrWhiteSpace(lastName) ? null : lastName.Trim();
        
        ProfilePictureUrl = profilePictureUrl;
    }

    /// <summary>
    /// Records a user login event by updating the LastLoginAt timestamp.
    /// </summary>
    public void RecordLogin()
    {
        LastLoginAt = DateTime.UtcNow;
    }

    /// <summary>
    /// Gets the user's display name (first + last name if available).
    /// </summary>
    public string GetDisplayName()
    {
        return string.IsNullOrWhiteSpace(LastName) 
            ? FirstName 
            : $"{FirstName} {LastName}";
    }
}

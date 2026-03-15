namespace MyApp.Application.DTOs;

/// <summary>
/// Represents user information retrieved from Google OAuth.
/// Note: Not all fields are guaranteed to be present depending on 
/// user's privacy settings and OAuth scope.
/// </summary>
public class GoogleUserInfo
{
    /// <summary>User's email address (always present with 'email' scope)</summary>
    public string Email { get; set; } = null!;
    
    /// <summary>User's given/first name (may be null if not provided)</summary>
    public string? FirstName { get; set; }
    
    /// <summary>User's family/last name (may be null if not provided)</summary>
    public string? LastName { get; set; }
    
    /// <summary>User's full name (may be null)</summary>
    public string? FullName { get; set; }
    
    /// <summary>Google's unique identifier for this user (always present)</summary>
    public string GoogleId { get; set; } = null!;
    
    /// <summary>URL to user's profile picture (may be null)</summary>
    public string? ProfilePictureUrl { get; set; }
    
    /// <summary>Whether the user's email is verified</summary>
    public bool EmailVerified { get; set; }
    
    /// <summary>
    /// Gets the best available display name from the available fields.
    /// Falls back through: FullName -> FirstName -> "User"
    /// </summary>
    public string GetBestAvailableName()
    {
        if (!string.IsNullOrWhiteSpace(FullName))
            return FullName.Trim();
        
        if (!string.IsNullOrWhiteSpace(FirstName))
            return FirstName.Trim();
        
        // Ultimate fallback - should rarely happen
        return "User";
    }
}

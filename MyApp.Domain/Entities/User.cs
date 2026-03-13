namespace MyApp.Domain.Entities;

public class User
{
    public Guid Id { get; private set; }
    public string Email { get; private set; } = null!;
    public string FirstName { get; private set; } = null!;
    public string LastName { get; private set; } = null!;
    public string GoogleId { get; private set; } = null!;
    public string? ProfilePictureUrl { get; private set; }
    public DateTime CreatedAt { get; private set; }
    public DateTime LastLoginAt { get; private set; }

    // EF Core requires a parameterless constructor
    private User() { }

    public static User Create(
        string email,
        string firstName,
        string lastName,
        string googleId,
        string? profilePictureUrl)
    {
        if (string.IsNullOrWhiteSpace(email))
            throw new ArgumentException("Email is required", nameof(email));
        
        if (string.IsNullOrWhiteSpace(firstName))
            throw new ArgumentException("First name is required", nameof(firstName));
        
        if (string.IsNullOrWhiteSpace(lastName))
            throw new ArgumentException("Last name is required", nameof(lastName));
        
        if (string.IsNullOrWhiteSpace(googleId))
            throw new ArgumentException("Google ID is required", nameof(googleId));

        var now = DateTime.UtcNow;
        
        return new User
        {
            Id = Guid.NewGuid(),
            Email = email.ToLowerInvariant(),
            FirstName = firstName,
            LastName = lastName,
            GoogleId = googleId,
            ProfilePictureUrl = profilePictureUrl,
            CreatedAt = now,
            LastLoginAt = now
        };
    }

    public void UpdateProfile(string firstName, string lastName, string? profilePictureUrl)
    {
        if (!string.IsNullOrWhiteSpace(firstName))
            FirstName = firstName;
        
        if (!string.IsNullOrWhiteSpace(lastName))
            LastName = lastName;
        
        ProfilePictureUrl = profilePictureUrl;
    }

    public void RecordLogin()
    {
        LastLoginAt = DateTime.UtcNow;
    }
}

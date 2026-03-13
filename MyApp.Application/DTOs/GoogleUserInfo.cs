namespace MyApp.Application.DTOs;

public class GoogleUserInfo
{
    public string Email { get; set; } = null!;
    public string FirstName { get; set; } = null!;
    public string LastName { get; set; } = null!;
    public string GoogleId { get; set; } = null!;
    public string? ProfilePictureUrl { get; set; }
    public bool EmailVerified { get; set; }
}

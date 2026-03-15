using System.ComponentModel.DataAnnotations;

namespace MyApp.Application.DTOs;

public class CreateFeatureRequestRequest
{
    [Required]
    [MinLength(3)]
    [MaxLength(2000)]
    public string Message { get; set; } = null!;

    [MaxLength(500)]
    public string? PageUrl { get; set; }
}

public class FeatureRequestDto
{
    public Guid Id { get; set; }
    public Guid UserId { get; set; }
    public string Message { get; set; } = null!;
    public string? PageUrl { get; set; }
    public DateTime CreatedAt { get; set; }
}

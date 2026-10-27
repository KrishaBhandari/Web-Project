using System.ComponentModel.DataAnnotations;

namespace WattWise.Models;

/// <summary>
/// Represents a registered consumer who owns one or more electricity connections.
/// </summary>
public class User
{
    [Key]
    public int UserId { get; set; }

    [Required(ErrorMessage = "Full name is required.")]
    [StringLength(100, MinimumLength = 2, ErrorMessage = "Name must be between 2 and 100 characters.")]
    [Display(Name = "Full Name")]
    public string FullName { get; set; } = string.Empty;

    [Required(ErrorMessage = "Email address is required.")]
    [EmailAddress(ErrorMessage = "Enter a valid email address.")]
    [StringLength(150)]
    public string Email { get; set; } = string.Empty;

    [Required(ErrorMessage = "Phone number is required.")]
    [Phone(ErrorMessage = "Enter a valid phone number.")]
    [StringLength(15)]
    [Display(Name = "Phone Number")]
    public string PhoneNumber { get; set; } = string.Empty;

    [Required(ErrorMessage = "Password is required.")]
    [MinLength(8, ErrorMessage = "Password must be at least 8 characters.")]
    public string PasswordHash { get; set; } = string.Empty;   // Stores BCrypt hash, never plaintext

    [StringLength(200)]
    public string? Address { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public bool IsActive { get; set; } = true;

    // Navigation property
    public ICollection<Connection> Connections { get; set; } = new List<Connection>();
}

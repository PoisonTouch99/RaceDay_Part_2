using System.ComponentModel.DataAnnotations;
using RaceDay.Api.Models;

namespace RaceDay.Api.DTOs;

public class RegisterRequest
{
    /// <example>Thabo Mokoena</example>
    [Required, MaxLength(100)]
    public string FullName { get; set; } = string.Empty;

    /// <example>thabo.mokoena@example.com</example>
    [Required, EmailAddress, MaxLength(150)]
    public string Email { get; set; } = string.Empty;

    /// <example>Passw0rd!</example>
    [Required, MinLength(8), MaxLength(100)]
    public string Password { get; set; } = string.Empty;

    /// <example>0731112222</example>
    [MaxLength(20)]
    public string? Phone { get; set; }

    /// <summary>Must be either "Organiser" or "Participant".</summary>
    /// <example>Participant</example>
    [Required]
    [RegularExpression("^(Organiser|Participant)$",
        ErrorMessage = "Role must be either 'Organiser' or 'Participant'.")]
    public string Role { get; set; } = string.Empty;
}

public class LoginRequest
{
    /// <example>thabo.mokoena@example.com</example>
    [Required, EmailAddress]
    public string Email { get; set; } = string.Empty;

    /// <example>Passw0rd!</example>
    [Required]
    public string Password { get; set; } = string.Empty;
}

/// <summary>User details returned to clients. Never includes the password hash.</summary>
public class UserResponse
{
    public int UserId { get; set; }
    public string FullName { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string Role { get; set; } = string.Empty;
    public string? Phone { get; set; }
    public DateTime CreatedAt { get; set; }

    public static UserResponse From(User u) => new()
    {
        UserId = u.UserId,
        FullName = u.FullName,
        Email = u.Email,
        Role = u.Role,
        Phone = u.Phone,
        CreatedAt = u.CreatedAt
    };
}

public class UpdateProfileRequest
{
    [Required, MaxLength(100)]
    public string FullName { get; set; } = string.Empty;

    [MaxLength(20)]
    public string? Phone { get; set; }
}

public class ChangePasswordRequest
{
    [Required]
    public string CurrentPassword { get; set; } = string.Empty;

    [Required, MinLength(8), MaxLength(100)]
    public string NewPassword { get; set; } = string.Empty;
}

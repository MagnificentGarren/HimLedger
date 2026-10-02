using System.ComponentModel.DataAnnotations;

namespace HimLedger.Application.DTOs;

public sealed record LoginDto
{
    [Required, EmailAddress, MaxLength(256)]
    public required string Email { get; init; }

    [Required]
    public required string Password { get; init; }
}

public sealed record AuthResponseDto(
    int UserId,
    string FirstName,
    string LastName,
    string Email,
    string Role,
    string Token
);

public sealed record RegisterUserDto
{
    [Required, MaxLength(100)]
    public required string FirstName { get; init; }

    [Required, MaxLength(100)]
    public required string LastName { get; init; }

    [Required, EmailAddress, MaxLength(256)]
    public required string Email { get; init; }

    [Required, MinLength(8), MaxLength(128)]
    public required string Password { get; init; }

    [Range(1, int.MaxValue)]
    public int RoleId { get; init; }

    [Range(1, int.MaxValue)]
    public int? DepartmentId { get; init; }
}
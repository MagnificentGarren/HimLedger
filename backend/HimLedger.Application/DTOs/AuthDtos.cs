namespace HimLedger.Application.DTOs;

public record LoginDto(string Email, string Password);

public record AuthResponseDto(
    int UserId,
    string FirstName,
    string LastName,
    string Email,
    string Role,
    string Token
);

public record RegisterUserDto(
    string FirstName,
    string LastName,
    string Email,
    string Password,
    int RoleId,
    int? DepartmentId
);
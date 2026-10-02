using System.ComponentModel.DataAnnotations;

namespace HimLedger.Application.DTOs;

public sealed record CreateExpenseDto
{
    [Range(1, int.MaxValue)]
    public int CategoryId { get; init; }

    [Range(1, int.MaxValue)]
    public int DepartmentId { get; init; }

    [Required, MaxLength(200)]
    public required string Title { get; init; }

    [MaxLength(2000)]
    public string? Description { get; init; }

    [Range(typeof(decimal), "0.01", "79228162514264337593543950335", ParseLimitsInInvariantCulture = true)]
    public decimal Amount { get; init; }

    public DateTime ExpenseDate { get; init; }

    [MaxLength(2048)]
    public string? ReceiptUrl { get; init; }
}

public sealed record ExpenseResponseDto(
    int ExpenseId,
    int UserId,
    string UserFullName,
    int CategoryId,
    string CategoryName,
    int DepartmentId,
    string DepartmentName,
    string Title,
    string? Description,
    decimal Amount,
    DateTime ExpenseDate,
    string? ReceiptUrl,
    string Status,
    DateTime CreatedAt
);

public sealed record UpdateExpenseStatusDto
{
    [Required, MaxLength(20)]
    public required string Status { get; init; }

    [MaxLength(2000)]
    public string? Comments { get; init; }
}
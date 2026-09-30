namespace HimLedger.Application.DTOs;

public record CreateExpenseDto(
    int CategoryId,
    int DepartmentId,
    string Title,
    string? Description,
    decimal Amount,
    DateTime ExpenseDate,
    string? ReceiptUrl
);

public record ExpenseResponseDto(
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

public record UpdateExpenseStatusDto(
    string Status,
    string? Comments
);
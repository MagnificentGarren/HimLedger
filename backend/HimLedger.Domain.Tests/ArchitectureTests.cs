using System.ComponentModel.DataAnnotations;
using HimLedger.Application.DTOs;
using HimLedger.Domain.Entities;
using Xunit;

namespace HimLedger.Domain.Tests;

public class ArchitectureTests
{
    [Fact]
    public void Domain_does_not_reference_other_HimLedger_projects()
    {
        var projectReferences = typeof(Expense).Assembly
            .GetReferencedAssemblies()
            .Where(assembly => assembly.Name?.StartsWith("HimLedger.", StringComparison.Ordinal) == true);

        Assert.Empty(projectReferences);
    }

    [Fact]
    public void New_expenses_start_as_drafts_with_a_UTC_creation_time()
    {
        var before = DateTime.UtcNow;
        var expense = new Expense();
        var after = DateTime.UtcNow;

        Assert.Equal(ClaimStatuses.Draft, expense.Status);
        Assert.InRange(expense.CreatedAt, before, after);
        Assert.Equal(DateTimeKind.Utc, expense.CreatedAt.Kind);
    }

    [Theory]
    [InlineData(ClaimStatuses.Draft, ClaimStatuses.Submitted, true)]
    [InlineData(ClaimStatuses.Submitted, ClaimStatuses.PendingApproval, true)]
    [InlineData(ClaimStatuses.PendingApproval, ClaimStatuses.Approved, true)]
    [InlineData(ClaimStatuses.PendingApproval, ClaimStatuses.Rejected, true)]
    [InlineData(ClaimStatuses.PendingApproval, ClaimStatuses.ChangesRequested, true)]
    [InlineData(ClaimStatuses.Approved, ClaimStatuses.Reimbursed, true)]
    [InlineData(ClaimStatuses.ChangesRequested, ClaimStatuses.Resubmitted, true)]
    [InlineData(ClaimStatuses.Resubmitted, ClaimStatuses.PendingApproval, true)]
    [InlineData(ClaimStatuses.Draft, ClaimStatuses.Approved, false)]
    [InlineData(ClaimStatuses.Rejected, ClaimStatuses.Resubmitted, false)]
    [InlineData(ClaimStatuses.Reimbursed, ClaimStatuses.Approved, false)]
    public void Claim_status_transitions_match_the_lifecycle(
        string from,
        string to,
        bool expected)
    {
        Assert.Equal(expected, ClaimStatuses.CanTransition(from, to));
    }

    [Fact]
    public void Expense_requests_reject_non_positive_amounts()
    {
        var request = new CreateExpenseDto
        {
            CategoryId = 1,
            DepartmentId = 1,
            Title = "Taxi fare",
            Amount = 0,
            ExpenseDate = DateTime.UtcNow
        };
        var validationResults = new List<ValidationResult>();

        var isValid = Validator.TryValidateObject(
            request,
            new ValidationContext(request),
            validationResults,
            validateAllProperties: true);

        Assert.False(isValid);
        Assert.Contains(validationResults, result => result.MemberNames.Contains(nameof(CreateExpenseDto.Amount)));
    }

    [Fact]
    public void Registration_requests_reject_short_passwords()
    {
        var request = new RegisterUserDto
        {
            FirstName = "Ari",
            LastName = "Lee",
            Email = "ari@example.com",
            Password = "short",
            RoleId = 1
        };
        var validationResults = new List<ValidationResult>();

        var isValid = Validator.TryValidateObject(
            request,
            new ValidationContext(request),
            validationResults,
            validateAllProperties: true);

        Assert.False(isValid);
        Assert.Contains(validationResults, result => result.MemberNames.Contains(nameof(RegisterUserDto.Password)));
    }
}

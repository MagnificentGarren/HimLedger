using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HimLedger.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class Phase4ClaimLifecycleAndPhase5FinancialControls : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "CK_Expenses_Status",
                table: "Expenses");

            migrationBuilder.DropCheckConstraint(
                name: "CK_ApprovalLogs_Action",
                table: "ApprovalLogs");

            migrationBuilder.AlterColumn<string>(
                name: "Status",
                table: "Expenses",
                type: "nvarchar(30)",
                maxLength: 30,
                nullable: false,
                defaultValue: "Draft",
                oldClrType: typeof(string),
                oldType: "nvarchar(20)",
                oldMaxLength: 20,
                oldDefaultValue: "Pending");

            migrationBuilder.AlterColumn<string>(
                name: "Action",
                table: "ApprovalLogs",
                type: "nvarchar(40)",
                maxLength: 40,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(20)",
                oldMaxLength: 20);

            migrationBuilder.Sql(
                "UPDATE Expenses SET Status = 'Pending Approval' WHERE Status = 'Pending';");

            migrationBuilder.CreateTable(
                name: "ApprovalDelegations",
                columns: table => new
                {
                    ApprovalDelegationId = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    DepartmentId = table.Column<int>(type: "int", nullable: false),
                    DelegatorUserId = table.Column<int>(type: "int", nullable: false),
                    DelegateUserId = table.Column<int>(type: "int", nullable: false),
                    StartsAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    EndsAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false, defaultValueSql: "SYSUTCDATETIME()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ApprovalDelegations", x => x.ApprovalDelegationId);
                    table.CheckConstraint("CK_ApprovalDelegations_DateRange", "StartsAt < EndsAt");
                    table.ForeignKey(
                        name: "FK_ApprovalDelegations_Departments_DepartmentId",
                        column: x => x.DepartmentId,
                        principalTable: "Departments",
                        principalColumn: "DepartmentId");
                    table.ForeignKey(
                        name: "FK_ApprovalDelegations_Users_DelegateUserId",
                        column: x => x.DelegateUserId,
                        principalTable: "Users",
                        principalColumn: "UserId");
                    table.ForeignKey(
                        name: "FK_ApprovalDelegations_Users_DelegatorUserId",
                        column: x => x.DelegatorUserId,
                        principalTable: "Users",
                        principalColumn: "UserId");
                });

            migrationBuilder.CreateTable(
                name: "ClaimStatusHistory",
                columns: table => new
                {
                    ClaimStatusHistoryId = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ExpenseId = table.Column<int>(type: "int", nullable: false),
                    ActorUserId = table.Column<int>(type: "int", nullable: false),
                    FromStatus = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    ToStatus = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    Decision = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false),
                    Notes = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    OccurredAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false, defaultValueSql: "SYSUTCDATETIME()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ClaimStatusHistory", x => x.ClaimStatusHistoryId);
                    table.ForeignKey(
                        name: "FK_ClaimStatusHistory_Expenses_ExpenseId",
                        column: x => x.ExpenseId,
                        principalTable: "Expenses",
                        principalColumn: "ExpenseId");
                    table.ForeignKey(
                        name: "FK_ClaimStatusHistory_Users_ActorUserId",
                        column: x => x.ActorUserId,
                        principalTable: "Users",
                        principalColumn: "UserId");
                });

            migrationBuilder.CreateTable(
                name: "ExpenseAttachments",
                columns: table => new
                {
                    ExpenseAttachmentId = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ExpenseId = table.Column<int>(type: "int", nullable: false),
                    UploadedByUserId = table.Column<int>(type: "int", nullable: false),
                    BlobName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    FileName = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: false),
                    ContentType = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    SizeBytes = table.Column<long>(type: "bigint", nullable: false),
                    UploadedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false, defaultValueSql: "SYSUTCDATETIME()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ExpenseAttachments", x => x.ExpenseAttachmentId);
                    table.ForeignKey(
                        name: "FK_ExpenseAttachments_Expenses_ExpenseId",
                        column: x => x.ExpenseId,
                        principalTable: "Expenses",
                        principalColumn: "ExpenseId");
                    table.ForeignKey(
                        name: "FK_ExpenseAttachments_Users_UploadedByUserId",
                        column: x => x.UploadedByUserId,
                        principalTable: "Users",
                        principalColumn: "UserId");
                });

            migrationBuilder.CreateTable(
                name: "NotificationOutboxMessages",
                columns: table => new
                {
                    NotificationOutboxMessageId = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    IdempotencyKey = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    EventType = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Payload = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    DispatchedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    LockedUntil = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    LockId = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    NextAttemptAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    Attempts = table.Column<int>(type: "int", nullable: false),
                    LastError = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_NotificationOutboxMessages", x => x.NotificationOutboxMessageId);
                });

            migrationBuilder.AddCheckConstraint(
                name: "CK_Expenses_Status",
                table: "Expenses",
                sql: "Status IN ('Draft', 'Submitted', 'Pending Approval', 'Approved', 'Rejected', 'Changes Requested', 'Resubmitted', 'Reimbursed')");

            migrationBuilder.AddCheckConstraint(
                name: "CK_ApprovalLogs_Action",
                table: "ApprovalLogs",
                sql: "Action IN ('Approved', 'Rejected', 'Changes Requested', 'Reimbursed')");

            migrationBuilder.CreateIndex(
                name: "IX_ApprovalDelegations_DelegateUserId",
                table: "ApprovalDelegations",
                column: "DelegateUserId");

            migrationBuilder.CreateIndex(
                name: "IX_ApprovalDelegations_DelegatorUserId",
                table: "ApprovalDelegations",
                column: "DelegatorUserId");

            migrationBuilder.CreateIndex(
                name: "IX_ApprovalDelegations_DepartmentId_DelegateUserId_StartsAt_EndsAt",
                table: "ApprovalDelegations",
                columns: new[] { "DepartmentId", "DelegateUserId", "StartsAt", "EndsAt" });

            migrationBuilder.CreateIndex(
                name: "IX_ClaimStatusHistory_ActorUserId",
                table: "ClaimStatusHistory",
                column: "ActorUserId");

            migrationBuilder.CreateIndex(
                name: "IX_ClaimStatusHistory_ExpenseId_OccurredAt",
                table: "ClaimStatusHistory",
                columns: new[] { "ExpenseId", "OccurredAt" });

            migrationBuilder.CreateIndex(
                name: "IX_ExpenseAttachments_BlobName",
                table: "ExpenseAttachments",
                column: "BlobName",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ExpenseAttachments_ExpenseId",
                table: "ExpenseAttachments",
                column: "ExpenseId");

            migrationBuilder.CreateIndex(
                name: "IX_ExpenseAttachments_UploadedByUserId",
                table: "ExpenseAttachments",
                column: "UploadedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_NotificationOutboxMessages_DispatchedAt_NextAttemptAt_LockedUntil",
                table: "NotificationOutboxMessages",
                columns: new[] { "DispatchedAt", "NextAttemptAt", "LockedUntil" });

            migrationBuilder.CreateIndex(
                name: "IX_NotificationOutboxMessages_IdempotencyKey",
                table: "NotificationOutboxMessages",
                column: "IdempotencyKey",
                unique: true);

            migrationBuilder.Sql(
                """
                INSERT INTO ClaimStatusHistory
                    (ExpenseId, ActorUserId, FromStatus, ToStatus, Decision, Notes, OccurredAt)
                SELECT ExpenseId, ReviewedByUserId, 'Pending Approval', Action, Action, Comments,
                       TODATETIMEOFFSET(ActionedAt, '+00:00')
                FROM ApprovalLogs;

                INSERT INTO ClaimStatusHistory
                    (ExpenseId, ActorUserId, FromStatus, ToStatus, Decision, Notes, OccurredAt)
                SELECT expense.ExpenseId, expense.UserId, '', expense.Status, 'Imported',
                       'Current claim state imported during lifecycle migration.',
                       TODATETIMEOFFSET(expense.CreatedAt, '+00:00')
                FROM Expenses AS expense
                WHERE NOT EXISTS (
                    SELECT 1 FROM ApprovalLogs AS approval WHERE approval.ExpenseId = expense.ExpenseId);
                """);
            migrationBuilder.Sql(
                """
                CREATE TRIGGER dbo.TR_ClaimStatusHistory_AppendOnly
                ON dbo.ClaimStatusHistory
                INSTEAD OF UPDATE, DELETE
                AS
                BEGIN
                    SET NOCOUNT ON;
                    THROW 51000, 'ClaimStatusHistory is append-only.', 1;
                END;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ApprovalDelegations");

            migrationBuilder.DropTable(
                name: "ClaimStatusHistory");

            migrationBuilder.DropTable(
                name: "ExpenseAttachments");

            migrationBuilder.DropTable(
                name: "NotificationOutboxMessages");

            migrationBuilder.DropCheckConstraint(
                name: "CK_Expenses_Status",
                table: "Expenses");

            migrationBuilder.DropCheckConstraint(
                name: "CK_ApprovalLogs_Action",
                table: "ApprovalLogs");

            migrationBuilder.Sql(
                """
                UPDATE Expenses
                SET Status = CASE
                    WHEN Status = 'Reimbursed' THEN 'Approved'
                    WHEN Status IN ('Approved', 'Rejected') THEN Status
                    ELSE 'Pending'
                END;
                UPDATE ApprovalLogs SET Action = 'Rejected' WHERE Action = 'Changes Requested';
                """);

            migrationBuilder.AlterColumn<string>(
                name: "Status",
                table: "Expenses",
                type: "nvarchar(20)",
                maxLength: 20,
                nullable: false,
                defaultValue: "Pending",
                oldClrType: typeof(string),
                oldType: "nvarchar(30)",
                oldMaxLength: 30,
                oldDefaultValue: "Draft");

            migrationBuilder.AlterColumn<string>(
                name: "Action",
                table: "ApprovalLogs",
                type: "nvarchar(20)",
                maxLength: 20,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(40)",
                oldMaxLength: 40);

            migrationBuilder.AddCheckConstraint(
                name: "CK_Expenses_Status",
                table: "Expenses",
                sql: "Status IN ('Pending', 'Approved', 'Rejected')");

            migrationBuilder.AddCheckConstraint(
                name: "CK_ApprovalLogs_Action",
                table: "ApprovalLogs",
                sql: "Action IN ('Approved', 'Rejected')");
        }
    }
}

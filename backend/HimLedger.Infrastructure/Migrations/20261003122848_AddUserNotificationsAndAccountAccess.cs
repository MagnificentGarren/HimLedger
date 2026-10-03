using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HimLedger.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddUserNotificationsAndAccountAccess : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "IsActive",
                table: "Users",
                type: "bit",
                nullable: false,
                defaultValue: true);

            migrationBuilder.CreateTable(
                name: "UserAccessAuditLogs",
                columns: table => new
                {
                    UserAccessAuditLogId = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    UserId = table.Column<int>(type: "int", nullable: false),
                    ActorUserId = table.Column<int>(type: "int", nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    OccurredAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UserAccessAuditLogs", x => x.UserAccessAuditLogId);
                    table.ForeignKey(
                        name: "FK_UserAccessAuditLogs_Users_ActorUserId",
                        column: x => x.ActorUserId,
                        principalTable: "Users",
                        principalColumn: "UserId");
                    table.ForeignKey(
                        name: "FK_UserAccessAuditLogs_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "UserId");
                });

            migrationBuilder.CreateTable(
                name: "UserNotifications",
                columns: table => new
                {
                    UserNotificationId = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    UserId = table.Column<int>(type: "int", nullable: false),
                    ExpenseId = table.Column<int>(type: "int", nullable: true),
                    Title = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: false),
                    Message = table.Column<string>(type: "nvarchar(700)", maxLength: 700, nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    ReadAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UserNotifications", x => x.UserNotificationId);
                    table.ForeignKey(
                        name: "FK_UserNotifications_Expenses_ExpenseId",
                        column: x => x.ExpenseId,
                        principalTable: "Expenses",
                        principalColumn: "ExpenseId");
                    table.ForeignKey(
                        name: "FK_UserNotifications_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "UserId");
                });

            migrationBuilder.UpdateData(
                table: "Users",
                keyColumn: "UserId",
                keyValue: 1,
                column: "IsActive",
                value: true);

            migrationBuilder.CreateIndex(
                name: "IX_UserAccessAuditLogs_ActorUserId",
                table: "UserAccessAuditLogs",
                column: "ActorUserId");

            migrationBuilder.CreateIndex(
                name: "IX_UserAccessAuditLogs_OccurredAt_UserAccessAuditLogId",
                table: "UserAccessAuditLogs",
                columns: new[] { "OccurredAt", "UserAccessAuditLogId" });

            migrationBuilder.CreateIndex(
                name: "IX_UserAccessAuditLogs_UserId",
                table: "UserAccessAuditLogs",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_UserNotifications_ExpenseId",
                table: "UserNotifications",
                column: "ExpenseId");

            migrationBuilder.CreateIndex(
                name: "IX_UserNotifications_UserId_ReadAt_CreatedAt",
                table: "UserNotifications",
                columns: new[] { "UserId", "ReadAt", "CreatedAt" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "UserAccessAuditLogs");

            migrationBuilder.DropTable(
                name: "UserNotifications");

            migrationBuilder.DropColumn(
                name: "IsActive",
                table: "Users");
        }
    }
}

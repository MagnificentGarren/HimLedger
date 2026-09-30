using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HimLedger.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddSeedAdminUser : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.InsertData(
                table: "Users",
                columns: new[] { "UserId", "CreatedAt", "DepartmentId", "Email", "FirstName", "LastName", "PasswordHash", "RoleId" },
                values: new object[] { 1, new DateTime(2026, 9, 30, 0, 0, 0, 0, DateTimeKind.Utc), null, "admin@himledger.com", "System", "Admin", "AQAAAAIAAYagAAAAEJJi7CfPSj7HKXIcvz9OSqCxUwHjDeC6nNIKFxaWYkrAyfRZ/xKutn8njKBkkQWIZQ==", 1 });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DeleteData(
                table: "Users",
                keyColumn: "UserId",
                keyValue: 1);
        }
    }
}

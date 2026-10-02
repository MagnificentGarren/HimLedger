using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HimLedger.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class DisableSeedAdminPassword : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.UpdateData(
                table: "Users",
                keyColumn: "UserId",
                keyValue: 1,
                column: "PasswordHash",
                value: "");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Never restore the known default password when rolling back this security change.
            migrationBuilder.UpdateData(
                table: "Users",
                keyColumn: "UserId",
                keyValue: 1,
                column: "PasswordHash",
                value: "");
        }
    }
}

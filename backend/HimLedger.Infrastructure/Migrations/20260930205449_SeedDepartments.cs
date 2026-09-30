using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace HimLedger.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class SeedDepartments : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                IF NOT EXISTS (SELECT 1 FROM Departments WHERE Code = 'LOG')
                    INSERT INTO Departments (Name, Code) VALUES ('Logistics & Operations', 'LOG');
                IF NOT EXISTS (SELECT 1 FROM Departments WHERE Code = 'FIN')
                    INSERT INTO Departments (Name, Code) VALUES ('Finance & Accounting', 'FIN');
                IF NOT EXISTS (SELECT 1 FROM Departments WHERE Code = 'IT')
                    INSERT INTO Departments (Name, Code) VALUES ('Software & IT', 'IT');
                IF NOT EXISTS (SELECT 1 FROM Departments WHERE Code = 'HR')
                    INSERT INTO Departments (Name, Code) VALUES ('Human Resources (HR)', 'HR');
                IF NOT EXISTS (SELECT 1 FROM Departments WHERE Code = 'MKT')
                    INSERT INTO Departments (Name, Code) VALUES ('Sales & Marketing', 'MKT');
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                DELETE FROM Departments
                WHERE (Code = 'LOG' AND Name = 'Logistics & Operations')
                   OR (Code = 'FIN' AND Name = 'Finance & Accounting')
                   OR (Code = 'IT' AND Name = 'Software & IT')
                   OR (Code = 'HR' AND Name = 'Human Resources (HR)')
                   OR (Code = 'MKT' AND Name = 'Sales & Marketing');
                """);
        }
    }
}

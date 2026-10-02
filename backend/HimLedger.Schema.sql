-- 1. Create Database
IF NOT EXISTS (SELECT name FROM sys.databases WHERE name = N'HimLedgerDB')
BEGIN
    CREATE DATABASE HimLedgerDB;
END
GO

USE HimLedgerDB;
GO

-- 2. Departments Table
CREATE TABLE Departments (
    DepartmentId INT IDENTITY(1,1) PRIMARY KEY,
    Name NVARCHAR(100) NOT NULL UNIQUE,
    Code NVARCHAR(10) NOT NULL UNIQUE,
    CreatedAt DATETIME2 NOT NULL DEFAULT GETUTCDATE()
);

INSERT INTO Departments (Name, Code)
VALUES
    ('Logistics & Operations', 'LOG'),
    ('Finance & Accounting', 'FIN'),
    ('Software & IT', 'IT'),
    ('Human Resources (HR)', 'HR'),
    ('Sales & Marketing', 'MKT');

-- 3. Roles Table
CREATE TABLE Roles (
    RoleId INT IDENTITY(1,1) PRIMARY KEY,
    Name NVARCHAR(50) NOT NULL UNIQUE
);

-- Seed Default Roles
INSERT INTO Roles (Name) VALUES ('Admin'), ('Manager'), ('Employee'), ('Finance');

-- 4. Users Table
CREATE TABLE Users (
    UserId INT IDENTITY(1,1) PRIMARY KEY,
    FirstName NVARCHAR(50) NOT NULL,
    LastName NVARCHAR(50) NOT NULL,
    Email NVARCHAR(100) NOT NULL UNIQUE,
    PasswordHash NVARCHAR(255) NOT NULL,
    EntraTenantId NVARCHAR(36) NULL,
    EntraObjectId NVARCHAR(36) NULL,
    RoleId INT NOT NULL,
    DepartmentId INT NULL,
    CreatedAt DATETIME2 NOT NULL DEFAULT GETUTCDATE(),
    FOREIGN KEY (RoleId) REFERENCES Roles(RoleId),
    FOREIGN KEY (DepartmentId) REFERENCES Departments(DepartmentId)
);
CREATE UNIQUE INDEX UX_Users_EntraTenantId_EntraObjectId
    ON Users (EntraTenantId, EntraObjectId)
    WHERE EntraTenantId IS NOT NULL AND EntraObjectId IS NOT NULL;

-- 5. Categories Table
CREATE TABLE Categories (
    CategoryId INT IDENTITY(1,1) PRIMARY KEY,
    Name NVARCHAR(50) NOT NULL UNIQUE,
    Description NVARCHAR(255) NULL
);

-- Seed Default Expense Categories
INSERT INTO Categories (Name, Description) VALUES
('Travel', 'Flights, taxis, and vehicle fuel'),
('Meals & Entertainment', 'Client lunches and team dinners'),
('Office Supplies', 'Hardware, software licenses, stationery'),
('Utilities', 'Internet, phone, and power bills');

-- 6. Budgets Table
CREATE TABLE Budgets (
    BudgetId INT IDENTITY(1,1) PRIMARY KEY,
    DepartmentId INT NOT NULL,
    FiscalYear INT NOT NULL,
    FiscalQuarter INT NOT NULL CHECK (FiscalQuarter BETWEEN 1 AND 4),
    AllocatedAmount DECIMAL(18,2) NOT NULL CHECK (AllocatedAmount >= 0),
    RemainingAmount DECIMAL(18,2) NOT NULL,
    RowVersion ROWVERSION NOT NULL,
    CreatedAt DATETIME2 NOT NULL DEFAULT GETUTCDATE(),
    FOREIGN KEY (DepartmentId) REFERENCES Departments(DepartmentId),
    CONSTRAINT UQ_Department_Quarter UNIQUE (DepartmentId, FiscalYear, FiscalQuarter)
);

-- 7. Expenses Table
CREATE TABLE Expenses (
    ExpenseId INT IDENTITY(1,1) PRIMARY KEY,
    UserId INT NOT NULL,
    CategoryId INT NOT NULL,
    DepartmentId INT NOT NULL,
    Title NVARCHAR(150) NOT NULL,
    Description NVARCHAR(500) NULL,
    Amount DECIMAL(18,2) NOT NULL CHECK (Amount > 0),
    ExpenseDate DATE NOT NULL,
    ReceiptUrl NVARCHAR(2083) NULL,
    Status NVARCHAR(20) NOT NULL DEFAULT 'Pending' CHECK (Status IN ('Pending', 'Approved', 'Rejected')),
    RowVersion ROWVERSION NOT NULL,
    CreatedAt DATETIME2 NOT NULL DEFAULT GETUTCDATE(),
    FOREIGN KEY (UserId) REFERENCES Users(UserId),
    FOREIGN KEY (CategoryId) REFERENCES Categories(CategoryId),
    FOREIGN KEY (DepartmentId) REFERENCES Departments(DepartmentId)
);

-- 8. ApprovalLogs Table
CREATE TABLE ApprovalLogs (
    ApprovalLogId INT IDENTITY(1,1) PRIMARY KEY,
    ExpenseId INT NOT NULL,
    ReviewedByUserId INT NOT NULL,
    Action NVARCHAR(20) NOT NULL CHECK (Action IN ('Approved', 'Rejected')),
    Comments NVARCHAR(500) NULL,
    ActionedAt DATETIME2 NOT NULL DEFAULT GETUTCDATE(),
    FOREIGN KEY (ExpenseId) REFERENCES Expenses(ExpenseId),
    FOREIGN KEY (ReviewedByUserId) REFERENCES Users(UserId)
);

-- Indexing for performance
CREATE INDEX IX_Expenses_UserId ON Expenses(UserId);
CREATE INDEX IX_Expenses_CategoryId ON Expenses(CategoryId);
CREATE INDEX IX_Expenses_DepartmentId_Status ON Expenses(DepartmentId, Status);
CREATE INDEX IX_Budgets_DepartmentId ON Budgets(DepartmentId);
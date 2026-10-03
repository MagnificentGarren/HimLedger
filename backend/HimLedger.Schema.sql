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
    IsActive BIT NOT NULL DEFAULT 1,
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
    Status NVARCHAR(30) NOT NULL DEFAULT 'Draft'
        CHECK (Status IN ('Draft', 'Submitted', 'Pending Approval', 'Approved', 'Rejected', 'Changes Requested', 'Resubmitted', 'Reimbursed')),
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
    Action NVARCHAR(40) NOT NULL CHECK (Action IN ('Approved', 'Rejected', 'Changes Requested', 'Reimbursed')),
    Comments NVARCHAR(500) NULL,
    ActionedAt DATETIME2 NOT NULL DEFAULT GETUTCDATE(),
    FOREIGN KEY (ExpenseId) REFERENCES Expenses(ExpenseId),
    FOREIGN KEY (ReviewedByUserId) REFERENCES Users(UserId)
);

-- Immutable lifecycle ledger. The trigger also protects against direct SQL updates/deletes.
CREATE TABLE ClaimStatusHistory (
    ClaimStatusHistoryId BIGINT IDENTITY(1,1) PRIMARY KEY,
    ExpenseId INT NOT NULL,
    ActorUserId INT NOT NULL,
    FromStatus NVARCHAR(30) NOT NULL,
    ToStatus NVARCHAR(30) NOT NULL,
    Decision NVARCHAR(40) NOT NULL,
    Notes NVARCHAR(1000) NULL,
    OccurredAt DATETIMEOFFSET NOT NULL DEFAULT SYSUTCDATETIME(),
    FOREIGN KEY (ExpenseId) REFERENCES Expenses(ExpenseId),
    FOREIGN KEY (ActorUserId) REFERENCES Users(UserId)
);

GO
CREATE TRIGGER dbo.TR_ClaimStatusHistory_AppendOnly
ON dbo.ClaimStatusHistory
INSTEAD OF UPDATE, DELETE
AS
BEGIN
    SET NOCOUNT ON;
    THROW 51000, 'ClaimStatusHistory is append-only.', 1;
END;
GO

CREATE TABLE ApprovalDelegations (
    ApprovalDelegationId INT IDENTITY(1,1) PRIMARY KEY,
    DepartmentId INT NOT NULL,
    DelegatorUserId INT NOT NULL,
    DelegateUserId INT NOT NULL,
    StartsAt DATETIMEOFFSET NOT NULL,
    EndsAt DATETIMEOFFSET NOT NULL,
    CreatedAt DATETIMEOFFSET NOT NULL DEFAULT SYSUTCDATETIME(),
    CONSTRAINT CK_ApprovalDelegations_DateRange CHECK (StartsAt < EndsAt),
    FOREIGN KEY (DepartmentId) REFERENCES Departments(DepartmentId),
    FOREIGN KEY (DelegatorUserId) REFERENCES Users(UserId),
    FOREIGN KEY (DelegateUserId) REFERENCES Users(UserId)
);

CREATE TABLE ExpenseAttachments (
    ExpenseAttachmentId INT IDENTITY(1,1) PRIMARY KEY,
    ExpenseId INT NOT NULL,
    UploadedByUserId INT NOT NULL,
    BlobName NVARCHAR(200) NOT NULL UNIQUE,
    FileName NVARCHAR(255) NOT NULL,
    ContentType NVARCHAR(100) NOT NULL,
    SizeBytes BIGINT NOT NULL,
    UploadedAt DATETIMEOFFSET NOT NULL DEFAULT SYSUTCDATETIME(),
    FOREIGN KEY (ExpenseId) REFERENCES Expenses(ExpenseId),
    FOREIGN KEY (UploadedByUserId) REFERENCES Users(UserId)
);

CREATE TABLE NotificationOutboxMessages (
    NotificationOutboxMessageId BIGINT IDENTITY(1,1) PRIMARY KEY,
    IdempotencyKey NVARCHAR(100) NOT NULL UNIQUE,
    EventType NVARCHAR(100) NOT NULL,
    Payload NVARCHAR(MAX) NOT NULL,
    CreatedAt DATETIMEOFFSET NOT NULL DEFAULT SYSUTCDATETIME(),
    DispatchedAt DATETIMEOFFSET NULL,
    LockedUntil DATETIMEOFFSET NULL,
    LockId NVARCHAR(MAX) NULL,
    NextAttemptAt DATETIMEOFFSET NULL,
    Attempts INT NOT NULL DEFAULT 0,
    LastError NVARCHAR(2000) NULL
);

CREATE TABLE UserNotifications (
    UserNotificationId BIGINT IDENTITY(1,1) PRIMARY KEY,
    UserId INT NOT NULL,
    ExpenseId INT NULL,
    Title NVARCHAR(120) NOT NULL,
    Message NVARCHAR(700) NOT NULL,
    CreatedAt DATETIMEOFFSET NOT NULL DEFAULT SYSUTCDATETIME(),
    ReadAt DATETIMEOFFSET NULL,
    FOREIGN KEY (UserId) REFERENCES Users(UserId),
    FOREIGN KEY (ExpenseId) REFERENCES Expenses(ExpenseId)
);

CREATE TABLE UserAccessAuditLogs (
    UserAccessAuditLogId BIGINT IDENTITY(1,1) PRIMARY KEY,
    UserId INT NOT NULL,
    ActorUserId INT NOT NULL,
    IsActive BIT NOT NULL,
    OccurredAt DATETIMEOFFSET NOT NULL DEFAULT SYSUTCDATETIME(),
    FOREIGN KEY (UserId) REFERENCES Users(UserId),
    FOREIGN KEY (ActorUserId) REFERENCES Users(UserId)
);

-- Indexing for performance
CREATE INDEX IX_Expenses_UserId ON Expenses(UserId);
CREATE INDEX IX_Expenses_CategoryId ON Expenses(CategoryId);
CREATE INDEX IX_Expenses_DepartmentId_Status ON Expenses(DepartmentId, Status);
CREATE INDEX IX_Budgets_DepartmentId ON Budgets(DepartmentId);
CREATE INDEX IX_ClaimStatusHistory_ExpenseId_OccurredAt ON ClaimStatusHistory(ExpenseId, OccurredAt);
CREATE INDEX IX_ApprovalDelegations_DepartmentId_DelegateUserId_StartsAt_EndsAt
    ON ApprovalDelegations(DepartmentId, DelegateUserId, StartsAt, EndsAt);
CREATE INDEX IX_NotificationOutboxMessages_DispatchedAt_NextAttemptAt_LockedUntil
    ON NotificationOutboxMessages(DispatchedAt, NextAttemptAt, LockedUntil);
CREATE INDEX IX_UserNotifications_UserId_ReadAt_CreatedAt
    ON UserNotifications(UserId, ReadAt, CreatedAt);
CREATE INDEX IX_UserNotifications_ExpenseId
    ON UserNotifications(ExpenseId);
CREATE INDEX IX_UserAccessAuditLogs_OccurredAt_UserAccessAuditLogId
    ON UserAccessAuditLogs(OccurredAt, UserAccessAuditLogId);
CREATE INDEX IX_UserAccessAuditLogs_UserId
    ON UserAccessAuditLogs(UserId);
CREATE INDEX IX_UserAccessAuditLogs_ActorUserId
    ON UserAccessAuditLogs(ActorUserId);
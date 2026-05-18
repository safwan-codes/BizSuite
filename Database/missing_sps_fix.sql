-- ============================================================
-- BIZSUITE ERP - MISSING STORED PROCEDURES FIX
-- Run this on your SmarterASP database (db_ac8d7f_safwan7424)
-- AFTER master_schema.sql and master_logic.sql have been run.
-- ============================================================

USE [db_ac8d7f_safwan7424];
GO

-- ── 1. sp_GetUsers ────────────────────────────────────────────────────────────
-- Returns all active staff members for a given company
CREATE OR ALTER PROCEDURE sp_GetUsers
    @CompanyId INT
AS
BEGIN
    SET NOCOUNT ON;
    SELECT
        U.UserId, U.CompanyId, U.FullName, U.Email, U.Phone,
        U.Department, U.IsDeleted, U.CreatedDate,
        U.ProfileImageData, U.ProfileImageMimeType,
        R.RoleName
    FROM Users U
    JOIN Roles R ON U.RoleId = R.RoleId
    WHERE U.CompanyId = @CompanyId AND U.IsDeleted = 0
    ORDER BY U.FullName;
END
GO

-- ── 2. sp_DeleteUser ─────────────────────────────────────────────────────────
-- Soft-deletes a staff member (never hard deletes)
CREATE OR ALTER PROCEDURE sp_DeleteUser
    @CompanyId INT,
    @UserId    INT
AS
BEGIN
    SET NOCOUNT ON;
    UPDATE Users
    SET IsDeleted = 1
    WHERE UserId = @UserId AND CompanyId = @CompanyId;
END
GO

-- ── 3. sp_DeleteProduct ──────────────────────────────────────────────────────
-- Soft-deletes a product
CREATE OR ALTER PROCEDURE sp_DeleteProduct
    @CompanyId INT,
    @ProductId INT
AS
BEGIN
    SET NOCOUNT ON;
    UPDATE Products
    SET IsDeleted = 1
    WHERE ProductId = @ProductId AND CompanyId = @CompanyId;
END
GO

-- ── 4. sp_DeleteCustomer ─────────────────────────────────────────────────────
-- Soft-deletes a customer
CREATE OR ALTER PROCEDURE sp_DeleteCustomer
    @CompanyId  INT,
    @CustomerId INT
AS
BEGIN
    SET NOCOUNT ON;
    UPDATE Customers
    SET IsDeleted = 1
    WHERE CustomerId = @CustomerId AND CompanyId = @CompanyId;
END
GO

-- ── 5. sp_UpsertCategory ─────────────────────────────────────────────────────
-- Inserts a new category or updates an existing one; returns CategoryId
CREATE OR ALTER PROCEDURE sp_UpsertCategory
    @CompanyId    INT,
    @CategoryId   INT = 0,
    @CategoryName NVARCHAR(100)
AS
BEGIN
    SET NOCOUNT ON;
    IF @CategoryId = 0
    BEGIN
        INSERT INTO Categories (CompanyId, CategoryName)
        VALUES (@CompanyId, @CategoryName);
        SELECT SCOPE_IDENTITY() AS CategoryId;
    END
    ELSE
    BEGIN
        UPDATE Categories
        SET CategoryName = @CategoryName
        WHERE CategoryId = @CategoryId AND CompanyId = @CompanyId;
        SELECT @CategoryId AS CategoryId;
    END
END
GO

-- ── 6. Password Reset SPs ─────────────────────────────────────────────────────
-- We need a column to store the reset code. Add it if it doesn't exist yet.
IF NOT EXISTS (
    SELECT 1 FROM sys.columns
    WHERE Name = N'ResetCode' AND Object_ID = OBJECT_ID(N'dbo.Users')
)
BEGIN
    ALTER TABLE dbo.Users ADD ResetCode NVARCHAR(10) NULL;
END
GO
IF NOT EXISTS (
    SELECT 1 FROM sys.columns
    WHERE Name = N'ResetCodeExpiry' AND Object_ID = OBJECT_ID(N'dbo.Users')
)
BEGIN
    ALTER TABLE dbo.Users ADD ResetCodeExpiry DATETIME NULL;
END
GO

-- Saves a one-time reset OTP code against the user record
CREATE OR ALTER PROCEDURE sp_UpdateResetCode
    @Email NVARCHAR(100),
    @Code  NVARCHAR(10)
AS
BEGIN
    SET NOCOUNT ON;
    UPDATE Users
    SET ResetCode       = @Code,
        ResetCodeExpiry = DATEADD(MINUTE, 15, GETDATE())   -- Code valid for 15 minutes
    WHERE Email = @Email AND IsDeleted = 0;

    SELECT @@ROWCOUNT AS RowsAffected;
END
GO

-- Verifies the OTP code is correct and has not expired
CREATE OR ALTER PROCEDURE sp_VerifyResetCode
    @Email NVARCHAR(100),
    @Code  NVARCHAR(10)
AS
BEGIN
    SET NOCOUNT ON;
    SELECT
        CASE
            WHEN COUNT(*) > 0 THEN 1
            ELSE 0
        END AS IsValid
    FROM Users
    WHERE Email           = @Email
      AND ResetCode       = @Code
      AND ResetCodeExpiry > GETDATE()
      AND IsDeleted       = 0;
END
GO

-- Updates the password and clears the reset code after successful reset
CREATE OR ALTER PROCEDURE sp_CompletePasswordReset
    @Email           NVARCHAR(100),
    @NewPasswordHash NVARCHAR(255)
AS
BEGIN
    SET NOCOUNT ON;
    UPDATE Users
    SET PasswordHash    = @NewPasswordHash,
        ResetCode       = NULL,
        ResetCodeExpiry = NULL
    WHERE Email = @Email AND IsDeleted = 0;
END
GO

PRINT '=== Missing SPs applied successfully ===';

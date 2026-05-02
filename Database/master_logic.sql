-- ============================================================
-- BIZSUITE ERP - MASTER LOGIC
-- Run this AFTER master_schema.sql.
-- Contains: User-Defined Types, Schema, Views, Stored Procedures,
--           Performance Indexes, and Row-Level Security Policies.
-- ============================================================

USE Bizsuite;
GO

-- ── User-Defined Table Types ──────────────────────────────────────────────────

IF TYPE_ID('OrderItemType') IS NULL
BEGIN
    CREATE TYPE OrderItemType AS TABLE
    (
        ProductId         INT,
        Quantity          INT,
        OverrideUnitPrice DECIMAL(18,2) NULL
    );
END
GO

-- ── Security Schema & Row-Level Security ──────────────────────────────────────

CREATE SCHEMA Security;
GO

CREATE OR ALTER FUNCTION Security.fn_TenantIsolationPredicate(@CompanyId INT)
    RETURNS TABLE
    WITH SCHEMABINDING
AS
    RETURN SELECT 1 AS fn_accessResult
    WHERE @CompanyId = CAST(SESSION_CONTEXT(N'CompanyId') AS INT)
       OR CAST(SESSION_CONTEXT(N'IsSystemAdmin') AS BIT) = 1;
GO

-- ── Views ─────────────────────────────────────────────────────────────────────

-- Sales summary view (includes fulfilment & shipping fields)
CREATE OR ALTER VIEW vw_SalesSummary AS
SELECT
    s.SalesOrderId,
    s.CompanyId,
    c.CustomerName,
    s.OrderDate,
    s.TotalAmount,
    s.Status              AS InvoiceStatus,
    ISNULL(s.FulfillmentStatus, 'Pending') AS FulfillmentStatus,
    s.Carrier,
    s.TrackingNumber,
    s.ShippedDate,
    (SELECT COUNT(*) FROM OrderItems WHERE SalesOrderId = s.SalesOrderId) AS TotalItems
FROM SalesOrders s
JOIN Customers c ON s.CustomerId = c.CustomerId;
GO

-- CRM intelligence view (customer tiers, lifetime value, recency)
CREATE OR ALTER VIEW vw_CRM_CustomerIntelligence AS
SELECT
    c.CompanyId,
    c.CustomerId,
    c.CustomerName,
    COUNT(s.SalesOrderId)                               AS TotalOrders,
    ISNULL(SUM(s.TotalAmount), 0)                       AS LifetimeValue,
    MAX(s.OrderDate)                                    AS LastPurchaseDate,
    DATEDIFF(day, MAX(s.OrderDate), GETDATE())          AS DaysSinceLastPurchase,
    CASE
        WHEN SUM(s.TotalAmount) > 50000 THEN 'Gold'
        WHEN SUM(s.TotalAmount) > 10000 THEN 'Silver'
        ELSE 'Bronze'
    END AS CustomerTier
FROM Customers c
LEFT JOIN SalesOrders s ON c.CustomerId = s.CustomerId AND s.IsDeleted = 0
WHERE c.IsDeleted = 0
GROUP BY c.CompanyId, c.CustomerId, c.CustomerName;
GO

-- ── Authentication & Registration ─────────────────────────────────────────────

SET ANSI_NULLS ON;
GO
SET QUOTED_IDENTIFIER ON;
GO

CREATE OR ALTER PROCEDURE sp_GetUserAuthData (@Email NVARCHAR(100))
AS
BEGIN
    SET NOCOUNT ON;
    SELECT
        U.UserId, U.FullName, U.Email, U.PasswordHash, U.CompanyId,
        U.Phone, U.Department,
        U.ProfileImageData, U.ProfileImageMimeType,
        R.RoleName, C.CompanyName, SP.PlanName AS SubscriptionTier
    FROM Users U
    JOIN Roles R             ON U.RoleId    = R.RoleId
    JOIN Companies C         ON U.CompanyId = C.CompanyId
    LEFT JOIN SubscriptionPlans SP ON C.PlanId = SP.PlanId
    WHERE U.Email = @Email AND U.IsDeleted = 0 AND C.SubscriptionStatus = 'Active';
END
GO

CREATE OR ALTER PROCEDURE sp_RegisterTenant
    @CompanyName NVARCHAR(150),
    @FullName    NVARCHAR(100),
    @Email       NVARCHAR(100),
    @PasswordHash NVARCHAR(255)
AS
BEGIN
    SET NOCOUNT ON;
    IF EXISTS (SELECT 1 FROM Users WHERE Email = @Email)
    BEGIN
        SELECT 'An account with this email already exists.' AS ErrorMessage;
        RETURN;
    END

    BEGIN TRY
        BEGIN TRANSACTION;
        DECLARE @NewCompanyId INT;

        INSERT INTO Companies (CompanyName, PlanId)
        VALUES (@CompanyName, (SELECT TOP 1 PlanId FROM SubscriptionPlans WHERE PlanName = 'Free'));
        SET @NewCompanyId = SCOPE_IDENTITY();

        INSERT INTO Users (CompanyId, FullName, Email, PasswordHash, RoleId, CreatedDate)
        VALUES (@NewCompanyId, @FullName, @Email, @PasswordHash, 1, GETDATE());

        COMMIT TRANSACTION;
        SELECT UserId = SCOPE_IDENTITY(), CompanyId = @NewCompanyId, FullName = @FullName, RoleName = 'Admin';
    END TRY
    BEGIN CATCH
        IF @@TRANCOUNT > 0 ROLLBACK TRANSACTION;
        SELECT ERROR_MESSAGE() AS ErrorMessage;
    END CATCH
END
GO

-- ── Staff / User Management ───────────────────────────────────────────────────

CREATE OR ALTER PROCEDURE sp_UpsertUser
(
    @CompanyId   INT,
    @UserId      INT          = 0,
    @RoleId      INT,
    @FullName    NVARCHAR(150),
    @Email       NVARCHAR(150),
    @Phone       NVARCHAR(20)  = NULL,
    @Department  NVARCHAR(100) = NULL,
    @PasswordHash NVARCHAR(255)= NULL,
    @CreatedBy   INT           = NULL
)
AS
BEGIN
    SET NOCOUNT ON;

    IF EXISTS (SELECT 1 FROM Users WHERE Email = @Email AND UserId <> @UserId AND IsDeleted = 0)
    BEGIN
        RAISERROR('The email address "%s" is already assigned to another staff member.', 16, 1, @Email);
        RETURN;
    END

    IF @UserId = 0
        INSERT INTO Users (CompanyId, RoleId, FullName, Email, PasswordHash, Phone, Department, CreatedBy)
        VALUES (@CompanyId, @RoleId, @FullName, @Email, @PasswordHash, @Phone, @Department, @CreatedBy);
    ELSE
        UPDATE Users
        SET RoleId       = @RoleId,
            FullName     = @FullName,
            Email        = @Email,
            Phone        = @Phone,
            Department   = @Department,
            PasswordHash = ISNULL(@PasswordHash, PasswordHash)
        WHERE UserId = @UserId AND CompanyId = @CompanyId;
END
GO

-- ── Customer Management ───────────────────────────────────────────────────────

-- Get all active customers with order metrics
CREATE OR ALTER PROCEDURE sp_GetCustomers (@CompanyId INT)
AS
BEGIN
    SET NOCOUNT ON;
    SELECT
        c.CustomerId, c.CompanyId, c.CustomerName, c.Email, c.Phone, c.Address,
        c.GSTIN, c.StateCode, c.IsDeleted, c.CreatedBy,
        c.ImageData, c.ImageMimeType,
        ISNULL((SELECT COUNT(*) FROM SalesOrders WHERE CustomerId = c.CustomerId AND IsDeleted = 0), 0)    AS Orders,
        ISNULL((SELECT SUM(TotalAmount) FROM SalesOrders WHERE CustomerId = c.CustomerId AND IsDeleted = 0), 0) AS Spend
    FROM Customers c
    WHERE c.CompanyId = @CompanyId AND c.IsDeleted = 0
    ORDER BY c.CustomerName;
END
GO

-- Get single customer by ID
CREATE OR ALTER PROCEDURE sp_GetCustomerById (@CompanyId INT, @CustomerId INT)
AS
BEGIN
    SET NOCOUNT ON;
    SELECT
        c.CustomerId, c.CompanyId, c.CustomerName, c.Email, c.Phone, c.Address,
        c.GSTIN, c.StateCode, c.IsDeleted, c.CreatedBy,
        c.ImageData, c.ImageMimeType,
        ISNULL((SELECT COUNT(*) FROM SalesOrders WHERE CustomerId = c.CustomerId AND IsDeleted = 0), 0)    AS Orders,
        ISNULL((SELECT SUM(TotalAmount) FROM SalesOrders WHERE CustomerId = c.CustomerId AND IsDeleted = 0), 0) AS Spend
    FROM Customers c
    WHERE c.CompanyId = @CompanyId AND c.CustomerId = @CustomerId AND c.IsDeleted = 0;
END
GO

-- Create or update a customer (preserves image if none provided)
CREATE OR ALTER PROCEDURE sp_UpsertCustomer
(
    @CompanyId    INT,
    @CustomerId   INT           = 0,
    @CustomerName NVARCHAR(150),
    @Email        NVARCHAR(100) = NULL,
    @Phone        NVARCHAR(20)  = NULL,
    @Address      NVARCHAR(250) = NULL,
    @GSTIN        NVARCHAR(15)  = NULL,
    @StateCode    NVARCHAR(2)   = NULL,
    @ImageData    VARBINARY(MAX)= NULL,
    @ImageMimeType NVARCHAR(50) = NULL,
    @UserId       INT
)
AS
BEGIN
    IF @CustomerId = 0
        INSERT INTO Customers
            (CompanyId, CustomerName, Email, Phone, Address, GSTIN, StateCode, ImageData, ImageMimeType, CreatedBy)
        VALUES
            (@CompanyId, @CustomerName, @Email, @Phone, @Address, @GSTIN, @StateCode, @ImageData, @ImageMimeType, @UserId);
    ELSE
    BEGIN
        IF @ImageData IS NOT NULL   -- only update image when a new one is uploaded
        BEGIN
            UPDATE Customers
            SET CustomerName  = @CustomerName, Email = @Email, Phone = @Phone,
                Address       = @Address, GSTIN = @GSTIN, StateCode = @StateCode,
                ImageData     = @ImageData, ImageMimeType = @ImageMimeType,
                ModifiedBy    = @UserId, ModifiedDate = GETDATE()
            WHERE CustomerId = @CustomerId AND CompanyId = @CompanyId;
        END
        ELSE
        BEGIN
            UPDATE Customers
            SET CustomerName = @CustomerName, Email = @Email, Phone = @Phone,
                Address      = @Address, GSTIN = @GSTIN, StateCode = @StateCode,
                ModifiedBy   = @UserId, ModifiedDate = GETDATE()
            WHERE CustomerId = @CustomerId AND CompanyId = @CompanyId;
        END
    END
END
GO

-- ── Product Management ────────────────────────────────────────────────────────

-- Get all active products
CREATE OR ALTER PROCEDURE sp_GetProducts (@CompanyId INT)
AS
BEGIN
    SET NOCOUNT ON;
    SELECT
        p.ProductId, p.CompanyId, p.CategoryId, p.SupplierId, p.ProductName,
        p.SKU, p.HSNCode, p.Price, p.TaxRate, p.StockQuantity, p.LowStockThreshold,
        p.IsPublished, p.IsDeleted,
        p.ImageData, p.ImageMimeType,
        c.CategoryName, s.SupplierName,
        CASE
            WHEN p.StockQuantity <= 0                    THEN 'Out of Stock'
            WHEN p.StockQuantity <= p.LowStockThreshold  THEN 'Low Stock'
            ELSE 'In Stock'
        END AS StockStatus
    FROM Products p
    LEFT JOIN Categories c ON p.CategoryId = c.CategoryId
    LEFT JOIN Suppliers  s ON p.SupplierId = s.SupplierId
    WHERE p.CompanyId = @CompanyId AND p.IsDeleted = 0
    ORDER BY p.ProductName;
END
GO

-- Get single product by ID
CREATE OR ALTER PROCEDURE sp_GetProductById (@CompanyId INT, @ProductId INT)
AS
BEGIN
    SET NOCOUNT ON;
    SELECT
        p.ProductId, p.CompanyId, p.CategoryId, p.SupplierId, p.ProductName,
        p.SKU, p.HSNCode, p.Price, p.TaxRate, p.StockQuantity, p.LowStockThreshold,
        p.IsPublished, p.IsDeleted,
        p.ImageData, p.ImageMimeType,
        c.CategoryName, s.SupplierName,
        CASE
            WHEN p.StockQuantity <= 0                    THEN 'Out of Stock'
            WHEN p.StockQuantity <= p.LowStockThreshold  THEN 'Low Stock'
            ELSE 'In Stock'
        END AS StockStatus
    FROM Products p
    LEFT JOIN Categories c ON p.CategoryId = c.CategoryId
    LEFT JOIN Suppliers  s ON p.SupplierId = s.SupplierId
    WHERE p.CompanyId = @CompanyId AND p.ProductId = @ProductId AND p.IsDeleted = 0;
END
GO

-- Create or update a product (preserves image if none provided)
CREATE OR ALTER PROCEDURE sp_UpsertProduct
(
    @CompanyId        INT,
    @ProductId        INT           = 0,
    @CategoryId       INT           = NULL,
    @SupplierId       INT           = NULL,
    @ProductName      NVARCHAR(150),
    @Price            DECIMAL(18,2),
    @StockQuantity    INT           = 0,
    @LowStockThreshold INT          = 10,
    @ImageData        VARBINARY(MAX)= NULL,
    @ImageMimeType    NVARCHAR(50)  = NULL,
    @UserId           INT
)
AS
BEGIN
    IF @ProductId = 0
        INSERT INTO Products
            (CompanyId, CategoryId, SupplierId, ProductName, Price, StockQuantity, LowStockThreshold, ImageData, ImageMimeType, CreatedBy)
        VALUES
            (@CompanyId, @CategoryId, @SupplierId, @ProductName, @Price, @StockQuantity, @LowStockThreshold, @ImageData, @ImageMimeType, @UserId);
    ELSE
    BEGIN
        IF @ImageData IS NOT NULL   -- only update image when a new one is uploaded
        BEGIN
            UPDATE Products
            SET CategoryId        = @CategoryId, SupplierId = @SupplierId, ProductName = @ProductName,
                Price             = @Price, StockQuantity = @StockQuantity, LowStockThreshold = @LowStockThreshold,
                ImageData         = @ImageData, ImageMimeType = @ImageMimeType,
                ModifiedBy        = @UserId, ModifiedDate = GETDATE()
            WHERE ProductId = @ProductId AND CompanyId = @CompanyId;
        END
        ELSE
        BEGIN
            UPDATE Products
            SET CategoryId       = @CategoryId, SupplierId = @SupplierId, ProductName = @ProductName,
                Price            = @Price, StockQuantity = @StockQuantity, LowStockThreshold = @LowStockThreshold,
                ModifiedBy       = @UserId, ModifiedDate = GETDATE()
            WHERE ProductId = @ProductId AND CompanyId = @CompanyId;
        END
    END
END
GO

-- ── Stock Management ──────────────────────────────────────────────────────────

CREATE OR ALTER PROCEDURE sp_AdjustStock
(
    @CompanyId INT,
    @ProductId INT,
    @Quantity  INT,         -- positive = IN, negative = OUT
    @Type      NVARCHAR(50), -- IN | OUT | ADJUSTMENT
    @UserId    INT
)
AS
BEGIN
    SET NOCOUNT ON;
    BEGIN TRANSACTION;
    BEGIN TRY
        UPDATE Products
        SET StockQuantity = StockQuantity + @Quantity
        WHERE ProductId = @ProductId AND CompanyId = @CompanyId;

        INSERT INTO StockTransactions (ProductId, TransactionType, Quantity, UserId)
        VALUES (@ProductId, @Type, @Quantity, @UserId);

        COMMIT TRANSACTION;
    END TRY
    BEGIN CATCH
        ROLLBACK TRANSACTION;
        THROW;
    END CATCH
END
GO

-- ── Sales Orders ──────────────────────────────────────────────────────────────

-- Create a new sales order with line items, auto-generate invoice, deduct stock
CREATE OR ALTER PROCEDURE sp_CreateSalesOrder
(
    @CompanyId      INT,
    @CustomerId     INT,
    @OrderDate      DATETIME,
    @OrderItemsJson NVARCHAR(MAX),   -- JSON array [{ProductId, Quantity, UnitPrice}]
    @UserId         INT,
    @NewOrderId     INT OUTPUT,
    @NewInvoiceId   INT OUTPUT
)
AS
BEGIN
    SET NOCOUNT ON;
    BEGIN TRY
        BEGIN TRANSACTION;

        INSERT INTO SalesOrders
            (CompanyId, CustomerId, OrderDate, TotalAmount, Status, FulfillmentStatus, CreatedBy)
        VALUES
            (@CompanyId, @CustomerId, @OrderDate, 0, 'Pending', 'Pending', @UserId);

        SET @NewOrderId = SCOPE_IDENTITY();

        -- Insert line items from JSON
        INSERT INTO OrderItems (CompanyId, SalesOrderId, ProductId, Quantity, UnitPrice, LineTotal)
        SELECT @CompanyId, @NewOrderId, ProductId, Quantity, UnitPrice, (Quantity * UnitPrice)
        FROM OPENJSON(@OrderItemsJson) WITH (
            ProductId INT           '$.ProductId',
            Quantity  INT           '$.Quantity',
            UnitPrice DECIMAL(18,2) '$.UnitPrice'
        );

        -- Calculate total with 18% GST
        DECLARE @Total DECIMAL(18,2);
        SELECT @Total = ISNULL(SUM(LineTotal), 0) * 1.18
        FROM OrderItems WHERE SalesOrderId = @NewOrderId;

        UPDATE SalesOrders SET TotalAmount = @Total WHERE SalesOrderId = @NewOrderId;

        -- Deduct stock for each ordered product
        UPDATE P
        SET P.StockQuantity = P.StockQuantity - J.Quantity
        FROM Products P
        INNER JOIN OPENJSON(@OrderItemsJson) WITH (
            ProductId INT '$.ProductId',
            Quantity  INT '$.Quantity'
        ) J ON P.ProductId = J.ProductId
        WHERE P.CompanyId = @CompanyId;

        -- Auto-generate linked invoice
        INSERT INTO Invoices (CompanyId, SalesOrderId, InvoiceDate, TotalAmount, Status)
        VALUES (@CompanyId, @NewOrderId, @OrderDate, @Total, 'Pending');
        SET @NewInvoiceId = SCOPE_IDENTITY();

        COMMIT TRANSACTION;
    END TRY
    BEGIN CATCH
        IF @@TRANCOUNT > 0 ROLLBACK TRANSACTION;
        THROW;
    END CATCH
END
GO

-- ── Finance ───────────────────────────────────────────────────────────────────

CREATE OR ALTER PROCEDURE sp_GetInvoices (@CompanyId INT)
AS
BEGIN
    SET NOCOUNT ON;
    SELECT
        I.InvoiceId,
        I.InvoiceDate,
        C.CustomerName,
        I.TotalAmount,
        I.Status,
        ISNULL(P.PaymentMethod, 'None') AS Method
    FROM Invoices I
    JOIN SalesOrders S ON I.SalesOrderId = S.SalesOrderId
    JOIN Customers   C ON S.CustomerId   = C.CustomerId
    LEFT JOIN Payments P ON P.InvoiceId  = I.InvoiceId
    WHERE I.CompanyId = @CompanyId
    ORDER BY I.InvoiceId DESC;
END
GO

CREATE OR ALTER PROCEDURE sp_AddPayment
(
    @CompanyId  INT,
    @InvoiceId  INT,
    @AmountPaid DECIMAL(18,2),
    @Method     NVARCHAR(50)
)
AS
BEGIN
    SET NOCOUNT ON;
    BEGIN TRANSACTION;
    BEGIN TRY
        INSERT INTO Payments (InvoiceId, AmountPaid, PaymentMethod, PaymentDate)
        VALUES (@InvoiceId, @AmountPaid, @Method, GETDATE());

        DECLARE @TotalInvoice DECIMAL(18,2), @TotalPaid DECIMAL(18,2);
        SELECT @TotalInvoice = TotalAmount FROM Invoices WHERE InvoiceId = @InvoiceId;
        SELECT @TotalPaid = SUM(AmountPaid) FROM Payments WHERE InvoiceId = @InvoiceId;

        IF @TotalPaid >= @TotalInvoice
            UPDATE Invoices SET Status = 'Paid'    WHERE InvoiceId = @InvoiceId AND CompanyId = @CompanyId;
        ELSE IF @TotalPaid > 0
            UPDATE Invoices SET Status = 'Partial' WHERE InvoiceId = @InvoiceId AND CompanyId = @CompanyId;

        COMMIT TRANSACTION;
    END TRY
    BEGIN CATCH
        ROLLBACK TRANSACTION;
        THROW;
    END CATCH
END
GO

-- ── Dashboard / Analytics ─────────────────────────────────────────────────────

CREATE OR ALTER PROCEDURE sp_GetDashboardStats @CompanyId INT
AS
BEGIN
    SET NOCOUNT ON;

    -- KPI summary row
    SELECT
        (SELECT COUNT(*)               FROM Customers   WHERE CompanyId = @CompanyId AND IsDeleted = 0) AS TotalCustomers,
        (SELECT COUNT(*)               FROM Products     WHERE CompanyId = @CompanyId AND IsDeleted = 0) AS TotalProducts,
        (SELECT ISNULL(SUM(TotalAmount),0) FROM SalesOrders WHERE CompanyId = @CompanyId) AS TotalSales,
        (SELECT ISNULL(SUM(TotalAmount),0) FROM SalesOrders WHERE CompanyId = @CompanyId
             AND MONTH(OrderDate) = MONTH(GETDATE()) AND YEAR(OrderDate) = YEAR(GETDATE())) AS SalesThisMonth,
        (SELECT ISNULL(SUM(TotalAmount),0) FROM SalesOrders WHERE CompanyId = @CompanyId
             AND MONTH(OrderDate) = MONTH(DATEADD(MONTH,-1,GETDATE()))
             AND YEAR(OrderDate)  = YEAR(DATEADD(MONTH,-1,GETDATE()))) AS SalesLastMonth;

    -- Low-stock alerts
    SELECT ProductName, StockQuantity, LowStockThreshold
    FROM Products
    WHERE CompanyId = @CompanyId AND IsDeleted = 0 AND StockQuantity <= LowStockThreshold;

    -- Top 5 customers by spend
    SELECT TOP 5 c.CustomerName, SUM(s.TotalAmount) AS TotalPurchase, COUNT(s.SalesOrderId) AS OrderCount
    FROM SalesOrders s
    JOIN Customers c ON s.CustomerId = c.CustomerId
    WHERE s.CompanyId = @CompanyId
    GROUP BY c.CustomerName
    ORDER BY TotalPurchase DESC;

    -- Top 5 revenue categories
    SELECT TOP 5 ISNULL(cat.CategoryName, 'Uncategorized') AS CategoryName, SUM(oi.LineTotal) AS Revenue
    FROM OrderItems oi
    JOIN Products p   ON oi.ProductId  = p.ProductId
    LEFT JOIN Categories cat ON p.CategoryId = cat.CategoryId
    WHERE oi.CompanyId = @CompanyId
    GROUP BY cat.CategoryName
    ORDER BY Revenue DESC;

    -- Monthly sales trend (last 12 months)
    SELECT YEAR(OrderDate) AS Year, MONTH(OrderDate) AS Month, SUM(TotalAmount) AS TotalSales
    FROM SalesOrders
    WHERE CompanyId = @CompanyId AND OrderDate >= DATEADD(MONTH, -12, GETDATE())
    GROUP BY YEAR(OrderDate), MONTH(OrderDate)
    ORDER BY Year DESC, Month DESC;

    -- Top 5 best-selling products
    SELECT TOP 5 p.ProductName, SUM(oi.Quantity) AS UnitsSold, SUM(oi.LineTotal) AS Revenue
    FROM OrderItems oi
    JOIN Products p ON oi.ProductId = p.ProductId
    WHERE oi.CompanyId = @CompanyId
    GROUP BY p.ProductName
    ORDER BY Revenue DESC;
END
GO

-- ── Audit Logging ─────────────────────────────────────────────────────────────

CREATE OR ALTER PROCEDURE sp_InsertAuditLog
    @CompanyId  INT,
    @UserId     INT,
    @TableName  NVARCHAR(100),
    @ActionType NVARCHAR(50),
    @Details    NVARCHAR(MAX)
AS
BEGIN
    SET NOCOUNT ON;
    INSERT INTO AuditLogs (CompanyId, UserId, TableName, ActionType, ActionDate, Details)
    VALUES (@CompanyId, @UserId, @TableName, @ActionType, GETDATE(), @Details);
END
GO

-- ── Performance Indexes ───────────────────────────────────────────────────────

-- Filtered indexes for active-record queries (tenant-aware)
CREATE NONCLUSTERED INDEX IX_Customers_Tenant_Active
    ON Customers(CompanyId, CustomerName) WHERE IsDeleted = 0;

CREATE NONCLUSTERED INDEX IX_Products_Tenant_Active
    ON Products(CompanyId, ProductName, SKU)
    INCLUDE (Price, StockQuantity) WHERE IsDeleted = 0;

CREATE NONCLUSTERED INDEX IX_SalesOrders_Tenant_Customer
    ON SalesOrders(CompanyId, CustomerId)
    INCLUDE (TotalAmount, OrderDate, Status) WHERE IsDeleted = 0;

-- General join-path indexes
CREATE INDEX IX_Products_CategoryId   ON Products(CategoryId);
CREATE INDEX IX_SalesOrders_CustomerId ON SalesOrders(CustomerId);
CREATE INDEX IX_OrderItems_SalesOrderId ON OrderItems(SalesOrderId);
CREATE INDEX IX_Invoices_SalesOrderId  ON Invoices(SalesOrderId);
CREATE INDEX IX_Payments_InvoiceId     ON Payments(InvoiceId);
CREATE INDEX IX_Products_SKU           ON Products(SKU);
CREATE INDEX IX_Customers_EmailPhone   ON Customers(Email, Phone);
GO

-- ── Row-Level Security Policies (Multi-Tenant Isolation) ─────────────────────

CREATE SECURITY POLICY Security.TenantPolicy_Customers
    ADD FILTER PREDICATE Security.fn_TenantIsolationPredicate(CompanyId) ON dbo.Customers,
    ADD BLOCK  PREDICATE Security.fn_TenantIsolationPredicate(CompanyId) ON dbo.Customers;

CREATE SECURITY POLICY Security.TenantPolicy_Products
    ADD FILTER PREDICATE Security.fn_TenantIsolationPredicate(CompanyId) ON dbo.Products,
    ADD BLOCK  PREDICATE Security.fn_TenantIsolationPredicate(CompanyId) ON dbo.Products;

CREATE SECURITY POLICY Security.TenantPolicy_SalesOrders
    ADD FILTER PREDICATE Security.fn_TenantIsolationPredicate(CompanyId) ON dbo.SalesOrders,
    ADD BLOCK  PREDICATE Security.fn_TenantIsolationPredicate(CompanyId) ON dbo.SalesOrders;

CREATE SECURITY POLICY Security.TenantPolicy_Invoices
    ADD FILTER PREDICATE Security.fn_TenantIsolationPredicate(CompanyId) ON dbo.Invoices,
    ADD BLOCK  PREDICATE Security.fn_TenantIsolationPredicate(CompanyId) ON dbo.Invoices;
GO

PRINT '=== Master Logic applied successfully ===';

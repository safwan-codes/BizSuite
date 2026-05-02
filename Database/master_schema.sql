-- ============================================================
-- BIZSUITE ERP - MASTER SCHEMA
-- Run this ONCE on a fresh SQL Server instance to create the
-- complete database structure.
-- ============================================================

USE master;
GO
IF NOT EXISTS (SELECT * FROM sys.databases WHERE name = 'Bizsuite')
    CREATE DATABASE Bizsuite;
GO
USE Bizsuite;
GO

-- ── Lookup / Reference Tables ─────────────────────────────────────────────────

CREATE TABLE SubscriptionPlans (
    PlanId          INT IDENTITY(1,1) PRIMARY KEY,
    PlanName        NVARCHAR(50)    NOT NULL UNIQUE,
    MonthlyPrice    DECIMAL(18,2)   NOT NULL,
    MaxUsers        INT             NOT NULL,
    MaxProducts     INT             NOT NULL,
    MaxCustomers    INT             NOT NULL,
    Features        NVARCHAR(MAX)   NULL
);

CREATE TABLE Companies (
    CompanyId          INT IDENTITY(1,1) PRIMARY KEY,
    CompanyName        NVARCHAR(150)   NOT NULL,
    PlanId             INT             NULL,
    SubscriptionStatus NVARCHAR(20)    DEFAULT 'Active',
    ExpiryDate         DATETIME        DEFAULT DATEADD(day, 14, GETDATE()),
    LogoUrl            NVARCHAR(MAX)   NULL,
    GSTIN              NVARCHAR(15)    NULL,
    StateCode          NVARCHAR(2)     NULL,
    Currency           NVARCHAR(10)    DEFAULT 'INR',
    CreatedDate        DATETIME        DEFAULT GETDATE(),
    CONSTRAINT FK_Companies_Plans FOREIGN KEY (PlanId) REFERENCES SubscriptionPlans(PlanId)
);

CREATE TABLE Roles (
    RoleId   INT IDENTITY(1,1) PRIMARY KEY,
    RoleName NVARCHAR(50) NOT NULL UNIQUE
);

-- ── Users / Staff ─────────────────────────────────────────────────────────────

CREATE TABLE Users (
    UserId              INT IDENTITY(1,1) PRIMARY KEY,
    CompanyId           INT             NOT NULL,
    RoleId              INT             NOT NULL,
    FullName            NVARCHAR(100)   NOT NULL,
    Email               NVARCHAR(100)   NOT NULL,
    PasswordHash        NVARCHAR(255)   NOT NULL,
    Phone               NVARCHAR(20)    NULL,
    Department          NVARCHAR(100)   NULL,
    ProfileImageData    VARBINARY(MAX)  NULL,
    ProfileImageMimeType NVARCHAR(50)   NULL,
    IsDeleted           BIT             DEFAULT 0,
    CreatedDate         DATETIME        DEFAULT GETDATE(),
    CreatedBy           INT             NULL,
    CONSTRAINT FK_Users_Company FOREIGN KEY (CompanyId) REFERENCES Companies(CompanyId),
    CONSTRAINT FK_Users_Roles   FOREIGN KEY (RoleId)    REFERENCES Roles(RoleId),
    CONSTRAINT UQ_Users_Email   UNIQUE (Email)
);

-- Staff Attendance (clock-in / clock-out)
CREATE TABLE Attendance (
    AttendanceId   INT IDENTITY(1,1) PRIMARY KEY,
    CompanyId      INT             NOT NULL,
    UserId         INT             NOT NULL,
    AttendanceDate DATE            NOT NULL DEFAULT CAST(GETDATE() AS DATE),
    ClockIn        DATETIME        NULL,
    ClockOut       DATETIME        NULL,
    AutoClockedOut BIT             NOT NULL DEFAULT 0,
    Status         NVARCHAR(20)    DEFAULT 'Present',
    Notes          NVARCHAR(500)   NULL,
    CreatedAt      DATETIME        NOT NULL DEFAULT GETDATE()
);

CREATE NONCLUSTERED INDEX IX_Attendance_Company_User_Date
    ON Attendance(CompanyId, UserId, AttendanceDate);

-- ── CRM ───────────────────────────────────────────────────────────────────────

CREATE TABLE Customers (
    CustomerId    INT IDENTITY(1,1) PRIMARY KEY,
    CompanyId     INT             NOT NULL,
    CustomerName  NVARCHAR(150)   NOT NULL,
    Email         NVARCHAR(100)   NULL,
    Phone         NVARCHAR(20)    NULL,
    Address       NVARCHAR(250)   NULL,
    GSTIN         NVARCHAR(15)    NULL,
    StateCode     NVARCHAR(2)     NULL,
    CustomerScore INT             DEFAULT 0,
    ImageData     VARBINARY(MAX)  NULL,
    ImageMimeType NVARCHAR(50)    NULL,
    IsDeleted     BIT             DEFAULT 0,
    CreatedDate   DATETIME        DEFAULT GETDATE(),
    ModifiedDate  DATETIME        NULL,
    ModifiedBy    INT             NULL,
    CreatedBy     INT             NULL,
    CONSTRAINT FK_Customers_Company FOREIGN KEY (CompanyId) REFERENCES Companies(CompanyId)
);

CREATE TABLE Leads (
    LeadId      INT IDENTITY(1,1) PRIMARY KEY,
    CompanyId   INT             NOT NULL,
    LeadName    NVARCHAR(150)   NOT NULL,
    Email       NVARCHAR(100)   NULL,
    Phone       NVARCHAR(20)    NULL,
    Status      NVARCHAR(50)    DEFAULT 'New',
    IsDeleted   BIT             DEFAULT 0,
    CreatedDate DATETIME        DEFAULT GETDATE(),
    CreatedBy   INT             NULL,
    CONSTRAINT FK_Leads_Company FOREIGN KEY (CompanyId) REFERENCES Companies(CompanyId)
);

-- ── Inventory ─────────────────────────────────────────────────────────────────

CREATE TABLE Categories (
    CategoryId   INT IDENTITY(1,1) PRIMARY KEY,
    CompanyId    INT             NOT NULL,
    CategoryName NVARCHAR(100)   NOT NULL,
    CONSTRAINT FK_Categories_Company FOREIGN KEY (CompanyId) REFERENCES Companies(CompanyId)
);

CREATE TABLE Suppliers (
    SupplierId   INT IDENTITY(1,1) PRIMARY KEY,
    CompanyId    INT             NOT NULL,
    SupplierName NVARCHAR(150)   NOT NULL,
    Email        NVARCHAR(100)   NULL,
    Phone        NVARCHAR(20)    NULL,
    CONSTRAINT FK_Suppliers_Company FOREIGN KEY (CompanyId) REFERENCES Companies(CompanyId)
);

CREATE TABLE Products (
    ProductId          INT IDENTITY(1,1) PRIMARY KEY,
    CompanyId          INT             NOT NULL,
    CategoryId         INT             NULL,
    SupplierId         INT             NULL,
    ProductName        NVARCHAR(150)   NOT NULL,
    SKU                NVARCHAR(50)    NULL,
    HSNCode            NVARCHAR(10)    NULL,
    Barcode            NVARCHAR(100)   NULL,
    Price              DECIMAL(18,2)   NOT NULL DEFAULT 0,
    TaxRate            DECIMAL(5,2)    DEFAULT 0,
    StockQuantity      INT             DEFAULT 0,
    LowStockThreshold  INT             DEFAULT 10,
    IsPublished        BIT             DEFAULT 0,
    ImageData          VARBINARY(MAX)  NULL,
    ImageMimeType      NVARCHAR(50)    NULL,
    ImageUrl           NVARCHAR(MAX)   NULL,
    StoreDescription   NVARCHAR(MAX)   NULL,
    IsDeleted          BIT             DEFAULT 0,
    CreatedDate        DATETIME        DEFAULT GETDATE(),
    ModifiedDate       DATETIME        NULL,
    ModifiedBy         INT             NULL,
    CreatedBy          INT             NULL,
    CONSTRAINT FK_Products_Company    FOREIGN KEY (CompanyId)   REFERENCES Companies(CompanyId),
    CONSTRAINT FK_Products_Categories FOREIGN KEY (CategoryId)  REFERENCES Categories(CategoryId),
    CONSTRAINT FK_Products_Suppliers  FOREIGN KEY (SupplierId)  REFERENCES Suppliers(SupplierId)
);

-- ── Sales / Orders ────────────────────────────────────────────────────────────

CREATE TABLE SalesOrders (
    SalesOrderId       INT IDENTITY(1,1) PRIMARY KEY,
    CompanyId          INT             NOT NULL,
    CustomerId         INT             NOT NULL,
    OrderDate          DATETIME        DEFAULT GETDATE(),
    TotalAmount        DECIMAL(18,2)   DEFAULT 0,
    Status             NVARCHAR(50)    DEFAULT 'Pending',      -- Invoice status
    FulfillmentStatus  NVARCHAR(50)    DEFAULT 'Pending',      -- Fulfillment status
    TrackingNumber     NVARCHAR(100)   NULL,
    Carrier            NVARCHAR(100)   NULL,
    ShippedDate        DATETIME        NULL,
    IsDeleted          BIT             DEFAULT 0,
    ModifiedDate       DATETIME        NULL,
    ModifiedBy         INT             NULL,
    CreatedBy          INT             NULL,
    CONSTRAINT FK_SalesOrders_Company   FOREIGN KEY (CompanyId)  REFERENCES Companies(CompanyId),
    CONSTRAINT FK_SalesOrders_Customers FOREIGN KEY (CustomerId) REFERENCES Customers(CustomerId)
);

CREATE TABLE OrderItems (
    OrderItemId    INT IDENTITY(1,1) PRIMARY KEY,
    CompanyId      INT             NOT NULL,
    SalesOrderId   INT             NOT NULL,
    ProductId      INT             NOT NULL,
    Quantity       INT             NOT NULL,
    UnitPrice      DECIMAL(18,2)   NOT NULL,
    TaxRate        DECIMAL(5,2)    DEFAULT 0,
    TaxAmount      DECIMAL(18,2)   DEFAULT 0,
    NetAmount      DECIMAL(18,2)   DEFAULT 0,
    DiscountAmount DECIMAL(18,2)   DEFAULT 0,
    LineTotal      DECIMAL(18,2)   NOT NULL,
    CONSTRAINT FK_OrderItems_Orders   FOREIGN KEY (SalesOrderId) REFERENCES SalesOrders(SalesOrderId),
    CONSTRAINT FK_OrderItems_Products FOREIGN KEY (ProductId)    REFERENCES Products(ProductId)
);

-- ── Finance ───────────────────────────────────────────────────────────────────

CREATE TABLE Invoices (
    InvoiceId    INT IDENTITY(1,1) PRIMARY KEY,
    CompanyId    INT             NOT NULL,
    SalesOrderId INT             NOT NULL,
    InvoiceDate  DATETIME        DEFAULT GETDATE(),
    TotalAmount  DECIMAL(18,2)   NOT NULL,
    Status       NVARCHAR(20)    DEFAULT 'Pending',    -- Pending | Partial | Paid
    IsDeleted    BIT             DEFAULT 0,
    CONSTRAINT FK_Invoices_Company FOREIGN KEY (CompanyId)    REFERENCES Companies(CompanyId),
    CONSTRAINT FK_Invoices_Orders  FOREIGN KEY (SalesOrderId) REFERENCES SalesOrders(SalesOrderId)
);

CREATE TABLE Payments (
    PaymentId     INT IDENTITY(1,1) PRIMARY KEY,
    InvoiceId     INT             NOT NULL,
    PaymentDate   DATETIME        DEFAULT GETDATE(),
    AmountPaid    DECIMAL(18,2)   NOT NULL,
    PaymentMethod NVARCHAR(50)    NULL,    -- Cash | Card | UPI | Bank Transfer
    CONSTRAINT FK_Payments_Invoices FOREIGN KEY (InvoiceId) REFERENCES Invoices(InvoiceId)
);

-- ── Stock Ledger ──────────────────────────────────────────────────────────────

CREATE TABLE StockTransactions (
    TransactionId   INT IDENTITY(1,1) PRIMARY KEY,
    ProductId       INT             NOT NULL,
    TransactionType NVARCHAR(50)    NULL,    -- IN | OUT | ADJUSTMENT
    Quantity        INT             NOT NULL,
    TransactionDate DATETIME        DEFAULT GETDATE(),
    UserId          INT             NULL,
    CONSTRAINT FK_StockTransactions_Products FOREIGN KEY (ProductId) REFERENCES Products(ProductId)
);

-- ── Audit / Notifications ─────────────────────────────────────────────────────

CREATE TABLE AuditLogs (
    LogId      INT IDENTITY(1,1) PRIMARY KEY,
    CompanyId  INT             NULL,
    UserId     INT             NULL,
    TableName  NVARCHAR(100)   NULL,
    ActionType NVARCHAR(50)    NULL,    -- INSERT | UPDATE | DELETE
    ActionDate DATETIME        DEFAULT GETDATE(),
    Details    NVARCHAR(MAX)   NULL,
    IPAddress  NVARCHAR(50)    NULL
);

CREATE TABLE Notifications (
    NotificationId INT IDENTITY(1,1) PRIMARY KEY,
    CompanyId      INT             NOT NULL,
    UserId         INT             NULL,
    Title          NVARCHAR(100)   NOT NULL,
    Message        NVARCHAR(500)   NOT NULL,
    Type           NVARCHAR(50)    NOT NULL,    -- Alert | Info | Warning
    IsRead         BIT             DEFAULT 0,
    CreatedDate    DATETIME        DEFAULT GETDATE(),
    CONSTRAINT FK_Notifications_Company FOREIGN KEY (CompanyId) REFERENCES Companies(CompanyId)
);

-- ── Fulfilment / Integrations ─────────────────────────────────────────────────

CREATE TABLE WebhookLogs (
    LogId           INT IDENTITY(1,1) PRIMARY KEY,
    Provider        NVARCHAR(50)    NOT NULL,
    EventType       NVARCHAR(100)   NOT NULL,
    Payload         NVARCHAR(MAX)   NOT NULL,
    ReceivedAt      DATETIME        DEFAULT GETDATE(),
    ProcessedStatus NVARCHAR(20)    DEFAULT 'Pending',
    SalesOrderId    INT             NULL
);

-- ── Seed Data ─────────────────────────────────────────────────────────────────

INSERT INTO Roles (RoleName)
VALUES ('Admin'), ('Manager'), ('Staff'), ('Developer');

INSERT INTO SubscriptionPlans (PlanName, MonthlyPrice, MaxUsers, MaxProducts, MaxCustomers)
VALUES
    ('Free',    0,    1,    50,    100),
    ('Basic',   999,  5,    1000,  5000),
    ('Premium', 2999, 50, 10000, 999999);

INSERT INTO Companies (CompanyName, PlanId)
VALUES ('BizSuite Default Corp', (SELECT PlanId FROM SubscriptionPlans WHERE PlanName = 'Free'));
GO

PRINT '=== Master Schema applied successfully ===';

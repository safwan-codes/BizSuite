USE [db_ac8d7f_safwan7424];
GO

-- Fix GST calculation to 5% for all new Sales Orders
CREATE OR ALTER PROCEDURE sp_CreateSalesOrder
(
    @CompanyId      INT,
    @CustomerId     INT,
    @OrderDate      DATETIME,
    @OrderItemsJson NVARCHAR(MAX),
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

        INSERT INTO OrderItems (CompanyId, SalesOrderId, ProductId, Quantity, UnitPrice, LineTotal)
        SELECT @CompanyId, @NewOrderId, ProductId, Quantity, UnitPrice, (Quantity * UnitPrice)
        FROM OPENJSON(@OrderItemsJson) WITH (
            ProductId INT           '$.ProductId',
            Quantity  INT           '$.Quantity',
            UnitPrice DECIMAL(18,2) '$.UnitPrice'
        );

        -- Calculate total with 5% GST
        DECLARE @Total DECIMAL(18,2);
        SELECT @Total = ISNULL(SUM(LineTotal), 0) * 1.05
        FROM OrderItems WHERE SalesOrderId = @NewOrderId;

        UPDATE SalesOrders SET TotalAmount = @Total WHERE SalesOrderId = @NewOrderId;

        -- Deduct stock
        UPDATE P
        SET P.StockQuantity = P.StockQuantity - J.Quantity
        FROM Products P
        INNER JOIN OPENJSON(@OrderItemsJson) WITH (
            ProductId INT '$.ProductId',
            Quantity  INT '$.Quantity'
        ) J ON P.ProductId = J.ProductId
        WHERE P.CompanyId = @CompanyId;

        -- Create Auto Invoice
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
PRINT 'sp_CreateSalesOrder updated to 5% GST successfully.'

select * from dbo.Users

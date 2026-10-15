-- ============================================================
--  Restaurant Management System
--  Database: RestaurantManagementDb
--  SQL Server 2017+
--  Stored Procedures & Functions Script
--  Run this AFTER EF Core migrations have created the tables.
-- ============================================================

USE RestaurantManagementDb;
GO

-- ============================================================
-- SECTION 1: SCALAR FUNCTIONS
-- ============================================================

-- -------------------------------------------------------
-- fn_CalculateGrandTotal
-- Calculates grand total for an order including taxes
-- -------------------------------------------------------
CREATE OR ALTER FUNCTION dbo.fn_CalculateGrandTotal
(
    @SubTotal        DECIMAL(18,2),
    @CGSTPercent     DECIMAL(18,2),
    @SGSTPercent     DECIMAL(18,2),
    @DiscountAmount  DECIMAL(18,2),
    @PackagingCharge DECIMAL(18,2),
    @DeliveryCharge  DECIMAL(18,2)
)
RETURNS DECIMAL(18,2)
AS
BEGIN
    DECLARE @CGST       DECIMAL(18,2) = ROUND(@SubTotal * @CGSTPercent / 100, 2);
    DECLARE @SGST       DECIMAL(18,2) = ROUND(@SubTotal * @SGSTPercent / 100, 2);
    DECLARE @TotalTax   DECIMAL(18,2) = @CGST + @SGST;
    DECLARE @GrandTotal DECIMAL(18,2) = @SubTotal + @TotalTax - @DiscountAmount + @PackagingCharge + @DeliveryCharge;
    RETURN @GrandTotal;
END;
GO

-- -------------------------------------------------------
-- fn_GetCustomerLoyaltyTier
-- Returns loyalty tier label based on total points
-- -------------------------------------------------------
CREATE OR ALTER FUNCTION dbo.fn_GetCustomerLoyaltyTier(@TotalPoints INT)
RETURNS NVARCHAR(20)
AS
BEGIN
    RETURN CASE
        WHEN @TotalPoints >= 5000 THEN 'Platinum'
        WHEN @TotalPoints >= 2000 THEN 'Gold'
        ELSE 'Silver'
    END;
END;
GO

-- -------------------------------------------------------
-- fn_GetTableOccupancyStatus
-- Returns human-readable table status
-- -------------------------------------------------------
CREATE OR ALTER FUNCTION dbo.fn_GetTableOccupancyStatus(@StatusId INT)
RETURNS NVARCHAR(20)
AS
BEGIN
    RETURN CASE @StatusId
        WHEN 1 THEN 'Available'
        WHEN 2 THEN 'Occupied'
        WHEN 3 THEN 'Reserved'
        WHEN 4 THEN 'Billing'
        WHEN 5 THEN 'Cleaning'
        WHEN 6 THEN 'Out of Service'
        ELSE 'Unknown'
    END;
END;
GO

-- -------------------------------------------------------
-- fn_GenerateOrderNumber
-- Generates a sequential order number like ORD-20241015-0001
-- -------------------------------------------------------
CREATE OR ALTER FUNCTION dbo.fn_GenerateOrderNumber(@CurrentDate DATE)
RETURNS NVARCHAR(30)
AS
BEGIN
    DECLARE @DateStr  NVARCHAR(8)  = FORMAT(@CurrentDate, 'yyyyMMdd');
    DECLARE @Count    INT;
    DECLARE @OrderNum NVARCHAR(30);

    SELECT @Count = COUNT(*) + 1
    FROM Orders
    WHERE CAST(CreatedAt AS DATE) = @CurrentDate;

    SET @OrderNum = 'ORD-' + @DateStr + '-' + RIGHT('0000' + CAST(@Count AS NVARCHAR), 4);
    RETURN @OrderNum;
END;
GO

-- ============================================================
-- SECTION 2: TABLE-VALUED FUNCTIONS
-- ============================================================

-- -------------------------------------------------------
-- fn_GetDailySalesSummary
-- Returns daily sales breakdown for a date range
-- -------------------------------------------------------
CREATE OR ALTER FUNCTION dbo.fn_GetDailySalesSummary
(
    @FromDate DATE,
    @ToDate   DATE
)
RETURNS TABLE
AS
RETURN
(
    SELECT
        CAST(o.CreatedAt AS DATE)                           AS SaleDate,
        COUNT(DISTINCT o.Id)                                AS TotalOrders,
        SUM(o.SubTotal)                                     AS TotalSubTotal,
        SUM(o.TotalTax)                                     AS TotalTax,
        SUM(o.DiscountAmount)                               AS TotalDiscount,
        SUM(o.GrandTotal)                                   AS TotalRevenue,
        COUNT(CASE WHEN o.OrderType = 1 THEN 1 END)         AS DineInOrders,
        COUNT(CASE WHEN o.OrderType = 2 THEN 1 END)         AS TakeawayOrders,
        COUNT(CASE WHEN o.OrderType = 3 THEN 1 END)         AS DeliveryOrders
    FROM Orders o
    WHERE o.IsSettled = 1
      AND CAST(o.CreatedAt AS DATE) BETWEEN @FromDate AND @ToDate
    GROUP BY CAST(o.CreatedAt AS DATE)
);
GO

-- -------------------------------------------------------
-- fn_GetTopSellingItems
-- Returns top N selling menu items by quantity
-- -------------------------------------------------------
CREATE OR ALTER FUNCTION dbo.fn_GetTopSellingItems
(
    @FromDate DATE,
    @ToDate   DATE,
    @TopN     INT
)
RETURNS TABLE
AS
RETURN
(
    SELECT TOP (@TopN)
        mi.Id                       AS MenuItemId,
        mi.Name                     AS MenuItemName,
        c.Name                      AS CategoryName,
        SUM(oi.Quantity)            AS TotalQuantitySold,
        SUM(oi.TotalPrice)          AS TotalRevenue,
        COUNT(DISTINCT oi.OrderId)  AS OrderCount
    FROM OrderItems oi
    INNER JOIN MenuItems mi ON mi.Id = oi.MenuItemId
    INNER JOIN Categories c ON c.Id = mi.CategoryId
    INNER JOIN Orders o ON o.Id = oi.OrderId
    WHERE o.IsSettled = 1
      AND CAST(o.CreatedAt AS DATE) BETWEEN @FromDate AND @ToDate
    GROUP BY mi.Id, mi.Name, c.Name
    ORDER BY TotalQuantitySold DESC
);
GO

-- ============================================================
-- SECTION 3: STORED PROCEDURES
-- ============================================================

-- -------------------------------------------------------
-- sp_GetDashboardStats
-- Returns key operational stats for the dashboard
-- -------------------------------------------------------
CREATE OR ALTER PROCEDURE dbo.sp_GetDashboardStats
AS
BEGIN
    SET NOCOUNT ON;
    DECLARE @Today DATE = CAST(GETDATE() AS DATE);

    SELECT
        ISNULL(SUM(o.GrandTotal), 0)                                    AS TodayRevenue,
        COUNT(o.Id)                                                     AS TodayOrders,
        COUNT(CASE WHEN o.OrderType = 1 THEN 1 END)                     AS DineInCount,
        COUNT(CASE WHEN o.OrderType = 2 THEN 1 END)                     AS TakeawayCount,
        COUNT(CASE WHEN o.OrderType = 3 THEN 1 END)                     AS DeliveryCount,
        ISNULL(AVG(o.GrandTotal), 0)                                    AS AvgOrderValue,
        (SELECT COUNT(*) FROM Tables WHERE Status = 2)                  AS OccupiedTables,
        (SELECT COUNT(*) FROM Tables WHERE IsActive = 1)                AS TotalActiveTables,
        (SELECT COUNT(*) FROM Reservations
             WHERE CAST(ReservationDate AS DATE) = @Today
             AND Status IN (1, 2))                                      AS TodayReservations
    FROM Orders o
    WHERE o.IsSettled = 1
      AND CAST(o.CreatedAt AS DATE) = @Today;
END;
GO

-- -------------------------------------------------------
-- sp_GetActiveOrders
-- Returns all currently active (non-settled) orders
-- -------------------------------------------------------
CREATE OR ALTER PROCEDURE dbo.sp_GetActiveOrders
AS
BEGIN
    SET NOCOUNT ON;
    SELECT
        o.Id,
        o.OrderNumber,
        o.OrderType,
        o.Status,
        t.TableNumber,
        f.Name          AS FloorName,
        o.CustomerName,
        o.SubTotal,
        o.GrandTotal,
        o.CreatedAt,
        o.Covers,
        o.IsSettled
    FROM Orders o
    LEFT JOIN Tables t ON t.Id = o.TableId
    LEFT JOIN Floors f ON f.Id = t.FloorId
    WHERE o.IsSettled = 0
      AND o.Status NOT IN (5, 6)  -- Exclude Completed/Cancelled
    ORDER BY o.CreatedAt DESC;
END;
GO

-- -------------------------------------------------------
-- sp_GetOrderDetails
-- Returns full details of a single order including items
-- -------------------------------------------------------
CREATE OR ALTER PROCEDURE dbo.sp_GetOrderDetails
    @OrderId INT
AS
BEGIN
    SET NOCOUNT ON;

    -- Order header
    SELECT
        o.Id, o.OrderNumber, o.OrderType, o.Status,
        o.TableId, t.TableNumber, f.Name AS FloorName,
        o.CustomerName, o.CustomerPhone, o.SpecialInstructions,
        o.SubTotal, o.CGST, o.SGST, o.TotalTax,
        o.DiscountAmount, o.PackagingCharge, o.DeliveryCharge,
        o.RoundOff, o.GrandTotal, o.Covers,
        o.OrderNote, o.DeliveryAddress, o.CreatedAt, o.IsSettled, o.SettledAt
    FROM Orders o
    LEFT JOIN Tables t ON t.Id = o.TableId
    LEFT JOIN Floors f ON f.Id = t.FloorId
    WHERE o.Id = @OrderId;

    -- Order items (VariantName is stored as SelectedVariantName in the DB)
    SELECT
        oi.Id, oi.MenuItemId, mi.Name AS MenuItemName,
        mi.IsVeg,
        oi.Quantity, oi.UnitPrice, oi.TotalPrice,
        oi.Status, oi.SpecialInstructions,
        oi.SelectedVariantName AS VariantName
    FROM OrderItems oi
    INNER JOIN MenuItems mi ON mi.Id = oi.MenuItemId
    WHERE oi.OrderId = @OrderId;
END;
GO

-- -------------------------------------------------------
-- sp_CreateOrder
-- Creates a new order and returns the OrderId
-- -------------------------------------------------------
CREATE OR ALTER PROCEDURE dbo.sp_CreateOrder
    @OrderType      INT,
    @TableId        INT = NULL,
    @CustomerName   NVARCHAR(200) = '',
    @CustomerPhone  NVARCHAR(20)  = '',
    @Covers         INT = 1,
    @WaiterId       INT = NULL,
    @CustomerId     INT = NULL,
    @NewOrderId     INT OUTPUT
AS
BEGIN
    SET NOCOUNT ON;
    BEGIN TRANSACTION;
    BEGIN TRY
        DECLARE @OrderNum NVARCHAR(30) = dbo.fn_GenerateOrderNumber(CAST(GETDATE() AS DATE));

        INSERT INTO Orders
            (OrderNumber, OrderType, Status, TableId, WaiterId, CustomerId,
             CustomerName, CustomerPhone, Covers,
             SubTotal, CGST, SGST, TotalTax, DiscountAmount, PackagingCharge,
             DeliveryCharge, RoundOff, GrandTotal,
             IsDraft, IsSettled, Version,
             SpecialInstructions, OrderNote, TokenNumber, DeliveryAddress,
             CustomerGSTIN, DraftUserName, CreatedByUserName, WalkoutReason, TableTransferNotes,
             CreatedAt, UpdatedAt, IsActive, IsDeleted)
        VALUES
            (@OrderNum, @OrderType, 1 /*Pending*/, @TableId, @WaiterId, @CustomerId,
             @CustomerName, @CustomerPhone, @Covers,
             0, 0, 0, 0, 0, 0, 0, 0, 0,
             0, 0, 1,
             '', '', '', '', '', '', '', '', '',
             GETDATE(), GETDATE(), 1, 0);

        SET @NewOrderId = SCOPE_IDENTITY();

        -- Mark table as Occupied if DineIn
        IF @OrderType = 1 AND @TableId IS NOT NULL
        BEGIN
            UPDATE Tables
            SET Status = 2, CurrentOrderId = @NewOrderId, UpdatedAt = GETDATE()
            WHERE Id = @TableId;
        END;

        COMMIT TRANSACTION;
    END TRY
    BEGIN CATCH
        ROLLBACK TRANSACTION;
        THROW;
    END CATCH;
END;
GO

-- -------------------------------------------------------
-- sp_SettleOrder
-- Settles/closes an order, updates table status
-- -------------------------------------------------------
CREATE OR ALTER PROCEDURE dbo.sp_SettleOrder
    @OrderId   INT,
    @RoundOff  DECIMAL(18,2) = 0
AS
BEGIN
    SET NOCOUNT ON;
    BEGIN TRANSACTION;
    BEGIN TRY
        DECLARE @TableId INT;

        UPDATE Orders
        SET IsSettled = 1, SettledAt = GETDATE(), Status = 5 /*Completed*/,
            RoundOff = @RoundOff, UpdatedAt = GETDATE()
        WHERE Id = @OrderId;

        SELECT @TableId = TableId FROM Orders WHERE Id = @OrderId;

        IF @TableId IS NOT NULL
        BEGIN
            UPDATE Tables
            SET Status = 5 /*Cleaning*/, CurrentOrderId = NULL, UpdatedAt = GETDATE()
            WHERE Id = @TableId;
        END;

        COMMIT TRANSACTION;
    END TRY
    BEGIN CATCH
        ROLLBACK TRANSACTION;
        THROW;
    END CATCH;
END;
GO

-- -------------------------------------------------------
-- sp_GetMenuWithCategories
-- Returns full menu grouped by category
-- -------------------------------------------------------
CREATE OR ALTER PROCEDURE dbo.sp_GetMenuWithCategories
    @IsAvailableOnly BIT = 1
AS
BEGIN
    SET NOCOUNT ON;
    SELECT
        c.Id                        AS CategoryId,
        c.Name                      AS CategoryName,
        c.DisplayOrder              AS CategoryOrder,
        mi.Id                       AS MenuItemId,
        mi.Name                     AS ItemName,
        mi.Description,
        mi.Price,
        mi.IsVeg,
        mi.IsAvailable,
        mi.PreparationTimeMinutes,
        mi.ImageUrl,
        mi.TaxPercentage
    FROM Categories c
    INNER JOIN MenuItems mi ON mi.CategoryId = c.Id
    WHERE c.IsActive = 1
      AND (@IsAvailableOnly = 0 OR mi.IsAvailable = 1)
    ORDER BY c.DisplayOrder, mi.Name;
END;
GO

-- -------------------------------------------------------
-- sp_GetInventoryAlerts
-- Returns inventory items at or below reorder level
-- -------------------------------------------------------
CREATE OR ALTER PROCEDURE dbo.sp_GetInventoryAlerts
AS
BEGIN
    SET NOCOUNT ON;
    SELECT
        Id,
        Name,
        Category,
        Unit,
        CurrentStock,
        MinimumStock,
        ReorderLevel,
        UnitCost,
        CASE
            WHEN CurrentStock <= MinimumStock THEN 'CRITICAL'
            WHEN CurrentStock <= ReorderLevel THEN 'LOW'
            ELSE 'OK'
        END AS StockStatus
    FROM InventoryItems
    WHERE CurrentStock <= ReorderLevel
      AND IsActive = 1
    ORDER BY CurrentStock ASC;
END;
GO

-- -------------------------------------------------------
-- sp_GetSalesReport
-- Returns sales summary for a date range
-- -------------------------------------------------------
CREATE OR ALTER PROCEDURE dbo.sp_GetSalesReport
    @FromDate DATE,
    @ToDate   DATE
AS
BEGIN
    SET NOCOUNT ON;

    -- Daily summary
    SELECT * FROM dbo.fn_GetDailySalesSummary(@FromDate, @ToDate)
    ORDER BY SaleDate;

    -- Top 10 items in the period
    SELECT * FROM dbo.fn_GetTopSellingItems(@FromDate, @ToDate, 10);

    -- Payment method breakdown (PaymentStatus: 3 = Paid)
    SELECT
        CASE p.PaymentMethod
            WHEN 1 THEN 'Cash'
            WHEN 2 THEN 'UPI'
            WHEN 3 THEN 'Credit Card'
            WHEN 4 THEN 'Debit Card'
            WHEN 5 THEN 'Wallet'
            WHEN 6 THEN 'Split'
        END                 AS PaymentMethod,
        COUNT(*)            AS TransactionCount,
        SUM(p.Amount)       AS TotalAmount
    FROM Payments p
    INNER JOIN Bills b ON b.Id = p.BillId
    INNER JOIN Orders o ON o.Id = b.OrderId
    WHERE p.PaymentStatus = 3  -- Paid
      AND CAST(o.CreatedAt AS DATE) BETWEEN @FromDate AND @ToDate
    GROUP BY p.PaymentMethod
    ORDER BY TotalAmount DESC;
END;
GO

-- -------------------------------------------------------
-- sp_GetFloorPlan
-- Returns floor + table layout with live status
-- -------------------------------------------------------
CREATE OR ALTER PROCEDURE dbo.sp_GetFloorPlan
AS
BEGIN
    SET NOCOUNT ON;
    SELECT
        f.Id            AS FloorId,
        f.Name          AS FloorName,
        f.DisplayOrder,
        t.Id            AS TableId,
        t.TableNumber,
        t.Capacity,
        t.Status        AS TableStatusId,
        dbo.fn_GetTableOccupancyStatus(t.Status) AS TableStatusLabel,
        t.CurrentOrderId,
        o.OrderNumber,
        o.GrandTotal    AS RunningTotal
    FROM Floors f
    LEFT JOIN Tables t ON t.FloorId = f.Id AND t.IsActive = 1
    LEFT JOIN Orders o ON o.Id = t.CurrentOrderId
    WHERE f.IsActive = 1
    ORDER BY f.DisplayOrder, t.TableNumber;
END;
GO

-- -------------------------------------------------------
-- sp_GetCustomerHistory
-- Returns order history for a specific customer
-- -------------------------------------------------------
CREATE OR ALTER PROCEDURE dbo.sp_GetCustomerHistory
    @CustomerId INT
AS
BEGIN
    SET NOCOUNT ON;

    -- Customer info + loyalty points
    SELECT
        c.Id, c.Name, c.Phone, c.Email,
        c.TotalVisits, c.TotalSpent,
        c.LoyaltyPoints,
        c.Tier,
        dbo.fn_GetCustomerLoyaltyTier(c.LoyaltyPoints) AS TierLabel,
        la.CurrentPoints            AS LoyaltyCurrentPoints,
        la.TotalEarnedPoints
    FROM Customers c
    LEFT JOIN LoyaltyAccounts la ON la.CustomerId = c.Id
    WHERE c.Id = @CustomerId;

    -- Past orders (last 20)
    SELECT TOP 20
        o.Id, o.OrderNumber, o.OrderType, o.GrandTotal,
        o.CreatedAt, o.IsSettled
    FROM Orders o
    WHERE o.CustomerId = @CustomerId
    ORDER BY o.CreatedAt DESC;
END;
GO

-- -------------------------------------------------------
-- sp_UpdateTableStatus
-- Updates a table's status
-- -------------------------------------------------------
CREATE OR ALTER PROCEDURE dbo.sp_UpdateTableStatus
    @TableId INT,
    @Status  INT
AS
BEGIN
    SET NOCOUNT ON;
    UPDATE Tables
    SET Status = @Status, UpdatedAt = GETDATE()
    WHERE Id = @TableId;
END;
GO

-- -------------------------------------------------------
-- sp_CreateKOT
-- Creates a Kitchen Order Ticket for an order
-- -------------------------------------------------------
CREATE OR ALTER PROCEDURE dbo.sp_CreateKOT
    @OrderId  INT,
    @KOTType  INT = 1,   -- 1=Initial, 2=Addition, 3=Cancellation
    @Notes    NVARCHAR(500) = '',
    @NewKOTId INT OUTPUT
AS
BEGIN
    SET NOCOUNT ON;
    BEGIN TRANSACTION;
    BEGIN TRY
        DECLARE @KOTNumber NVARCHAR(20);
        DECLARE @KOTCount  INT;
        DECLARE @TableNum  NVARCHAR(20);

        SELECT @KOTCount = COUNT(*) + 1 FROM KOTs WHERE OrderId = @OrderId;
        SELECT @TableNum = t.TableNumber
        FROM Orders o
        LEFT JOIN Tables t ON t.Id = o.TableId
        WHERE o.Id = @OrderId;

        SET @KOTNumber = 'KOT-' + CAST(@OrderId AS NVARCHAR) + '-' + CAST(@KOTCount AS NVARCHAR);

        INSERT INTO KOTs
            (OrderId, KOTNumber, Status, Type, Notes,
             TableNumber, WaiterName, Station, Reason, PrintFailed,
             CreatedAt, UpdatedAt, IsActive, IsDeleted)
        VALUES
            (@OrderId, @KOTNumber, 1 /*New*/, @KOTType, @Notes,
             ISNULL(@TableNum, ''), '', '', '', 0,
             GETDATE(), GETDATE(), 1, 0);

        SET @NewKOTId = SCOPE_IDENTITY();
        COMMIT TRANSACTION;
    END TRY
    BEGIN CATCH
        ROLLBACK TRANSACTION;
        THROW;
    END CATCH;
END;
GO

-- -------------------------------------------------------
-- sp_GetStaffAttendanceSummary
-- Returns attendance summary for a month
-- Attendance.Status: 1=Present, 2=Absent, 3=HalfDay, 4=Leave
-- -------------------------------------------------------
CREATE OR ALTER PROCEDURE dbo.sp_GetStaffAttendanceSummary
    @Month INT,
    @Year  INT
AS
BEGIN
    SET NOCOUNT ON;
    SELECT
        e.Id            AS EmployeeId,
        e.Name,
        e.Role,
        e.Shift,
        COUNT(a.Id)                                             AS TotalDays,
        SUM(CASE WHEN a.Status = 1 THEN 1 ELSE 0 END)          AS PresentDays,
        SUM(CASE WHEN a.Status = 2 THEN 1 ELSE 0 END)          AS AbsentDays,
        SUM(CASE WHEN a.Status = 3 THEN 1 ELSE 0 END)          AS HalfDays,
        SUM(CASE WHEN a.Status = 4 THEN 1 ELSE 0 END)          AS LeaveDays
    FROM Employees e
    LEFT JOIN Attendances a
        ON a.EmployeeId = e.Id
        AND MONTH(a.Date) = @Month
        AND YEAR(a.Date) = @Year
    WHERE e.IsActive = 1
    GROUP BY e.Id, e.Name, e.Role, e.Shift
    ORDER BY e.Name;
END;
GO

-- ============================================================
-- SECTION 4: VERIFY
-- ============================================================
PRINT '====================================================';
PRINT 'Verifying created objects...';
PRINT '====================================================';
GO

SELECT 'TABLE'  AS ObjectType, TABLE_NAME AS ObjectName
FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_TYPE = 'BASE TABLE'
UNION ALL
SELECT 'PROC', name FROM sys.objects WHERE type = 'P' AND name LIKE 'sp_%'
UNION ALL
SELECT 'FUNCTION', name FROM sys.objects WHERE type IN ('FN','TF') AND name LIKE 'fn_%'
ORDER BY ObjectType, ObjectName;
GO

PRINT '';
PRINT '✅ All stored procedures and functions created successfully in RestaurantManagementDb';
GO

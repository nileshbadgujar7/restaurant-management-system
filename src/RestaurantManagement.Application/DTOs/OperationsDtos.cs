using RestaurantManagement.Domain.Enums;

namespace RestaurantManagement.Application.DTOs;

public class InventoryItemDto
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Category { get; set; } = string.Empty;
    public string Unit { get; set; } = "KG";
    public decimal CurrentStock { get; set; }
    public decimal MinimumStock { get; set; }
    public decimal UnitCost { get; set; }
    public decimal ReorderLevel { get; set; }
    public bool IsLowStock => CurrentStock <= MinimumStock;
}

public class StockAdjustmentDto
{
    public int InventoryItemId { get; set; }
    public decimal Quantity { get; set; }
    public string TransactionType { get; set; } = "IN"; // IN, OUT, ADJUSTMENT, WASTAGE
    public string Notes { get; set; } = string.Empty;
}

public class RecipeDto
{
    public int Id { get; set; }
    public int MenuItemId { get; set; }
    public string MenuItemName { get; set; } = string.Empty;
    public string Instructions { get; set; } = string.Empty;
    public List<RecipeItemDto> Items { get; set; } = new();
}

public class RecipeItemDto
{
    public int Id { get; set; }
    public int InventoryItemId { get; set; }
    public string InventoryItemName { get; set; } = string.Empty;
    public decimal QuantityRequired { get; set; }
    public string Unit { get; set; } = "KG";
}

public class EmployeeDto
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string Phone { get; set; } = string.Empty;
    public UserRole Role { get; set; }
    public string RoleName => Role.ToString();
    public DateTime JoiningDate { get; set; }
    public decimal BaseSalary { get; set; }
    public string Shift { get; set; } = "Morning";
    public bool IsActive { get; set; }
}

public class AttendanceDto
{
    public int Id { get; set; }
    public int EmployeeId { get; set; }
    public string EmployeeName { get; set; } = string.Empty;
    public DateTime Date { get; set; }
    public DateTime CheckInTime { get; set; }
    public DateTime? CheckOutTime { get; set; }
    public string Status { get; set; } = "Present";
}

public class ExpenseDto
{
    public int Id { get; set; }
    public string Category { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public decimal Amount { get; set; }
    public DateTime ExpenseDate { get; set; }
    public string RecordedBy { get; set; } = string.Empty;
}

public class DashboardSummaryDto
{
    public decimal TodaySales { get; set; }
    public decimal YesterdaySales { get; set; }
    public int TodayOrdersCount { get; set; }
    public int TotalTablesCount { get; set; }
    public int OccupiedTablesCount { get; set; }
    public int AvailableTablesCount { get; set; }
    public int ReservedTablesCount { get; set; }
    public int PendingKOTCount { get; set; }
    public int CompletedOrdersCount { get; set; }
    public int CancelledOrdersCount { get; set; }
    public int NewOrdersCount { get; set; }
    public int PreparingOrdersCount { get; set; }
    public int ReadyOrdersCount { get; set; }
    public int ServedOrdersCount { get; set; }
    public List<LowStockDto> LowStockAlerts { get; set; } = new();
    public List<TopSellingItemDto> TopSellingItems { get; set; } = new();
    public List<FloorDto> FloorTables { get; set; } = new();
    public List<DailySalesSummaryDto> WeeklySalesTrend { get; set; } = new();
    public int DineInOrdersCount { get; set; }
    public int TakeawayOrdersCount { get; set; }
    public int DeliveryOrdersCount { get; set; }
    public List<RoleStaffCountDto> StaffRoleAttendance { get; set; } = new();
    public int TotalStaffCount { get; set; }
    public int PresentStaffCount { get; set; }
    public List<RecentOrderSummaryDto> RecentOrders { get; set; } = new();
}

public class RecentOrderSummaryDto
{
    public int Id { get; set; }
    public string OrderNumber { get; set; } = string.Empty;
    public string OrderType { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public int? TableId { get; set; }
    public string TableNumber { get; set; } = string.Empty;
    public decimal GrandTotal { get; set; }
    public int TotalItems { get; set; }
    public DateTime CreatedAt { get; set; }
}

public class RoleStaffCountDto
{
    public string RoleName { get; set; } = string.Empty;
    public int TotalCount { get; set; }
    public int PresentCount { get; set; }
}

public class LowStockDto
{
    public string ItemName { get; set; } = string.Empty;
    public decimal CurrentStock { get; set; }
    public decimal MinimumStock { get; set; }
    public string Unit { get; set; } = "KG";
    public string AlertLevel { get; set; } = "Low Stock"; // Low Stock or Critical
}

public class TopSellingItemDto
{
    public string MenuItemName { get; set; } = string.Empty;
    public string CategoryName { get; set; } = string.Empty;
    public int TotalQuantitySold { get; set; }
    public decimal TotalRevenue { get; set; }
}

public class SalesReportDto
{
    public DateTime StartDate { get; set; }
    public DateTime EndDate { get; set; }
    public decimal TotalRevenue { get; set; }
    public int TotalOrders { get; set; }
    public decimal DineInSales { get; set; }
    public decimal TakeawaySales { get; set; }
    public decimal DeliverySales { get; set; }
    public decimal TotalDiscount { get; set; }
    public decimal TotalCGST { get; set; }
    public decimal TotalSGST { get; set; }
    public List<DailySalesSummaryDto> DailySummaries { get; set; } = new();
}

public class DailySalesSummaryDto
{
    public DateTime Date { get; set; }
    public decimal SalesAmount { get; set; }
    public int OrdersCount { get; set; }
}

public class EmployeeLedgerDto
{
    public int Id { get; set; }
    public int EmployeeId { get; set; }
    public string EmployeeName { get; set; } = string.Empty;
    public string EmployeeRole { get; set; } = string.Empty;
    public DateTime TransactionDate { get; set; }
    public string TransactionType { get; set; } = "Advance"; // Advance, Repayment, SalaryPayment, Deduction
    public decimal Amount { get; set; }
    public string PaymentMethod { get; set; } = "Cash";
    public string ReferenceNumber { get; set; } = string.Empty;
    public string Notes { get; set; } = string.Empty;
    public string RecordedBy { get; set; } = string.Empty;
    public decimal RunningBalance { get; set; }
}

public class EmployeeLedgerSummaryDto
{
    public int EmployeeId { get; set; }
    public string EmployeeName { get; set; } = string.Empty;
    public string Phone { get; set; } = string.Empty;
    public string Role { get; set; } = string.Empty;
    public decimal BaseSalary { get; set; }
    public decimal TotalAdvanceGiven { get; set; }
    public decimal TotalRepaidOrDeducted { get; set; }
    public decimal OutstandingAdvance => Math.Max(0, TotalAdvanceGiven - TotalRepaidOrDeducted);
    public List<EmployeeLedgerDto> Transactions { get; set; } = new();
}

public class CreateLedgerTransactionDto
{
    public int EmployeeId { get; set; }
    public string TransactionType { get; set; } = "Advance"; // Advance or Repayment
    public decimal Amount { get; set; }
    public string PaymentMethod { get; set; } = "Cash";
    public string ReferenceNumber { get; set; } = string.Empty;
    public string Notes { get; set; } = string.Empty;
    public string RecordedBy { get; set; } = "Admin";
}

public class PayrollCalculationDto
{
    public int EmployeeId { get; set; }
    public string EmployeeName { get; set; } = string.Empty;
    public string Role { get; set; } = string.Empty;
    public decimal BaseSalary { get; set; }
    public int Month { get; set; }
    public int Year { get; set; }
    public int TotalDaysInMonth { get; set; } = 30;
    public decimal PresentDays { get; set; }
    public decimal HalfDays { get; set; }
    public decimal PaidLeaveDays { get; set; }
    public decimal UnpaidLeaveDays { get; set; }
    public decimal EffectivePaidDays => PresentDays + (HalfDays * 0.5m) + PaidLeaveDays;
    public decimal DailyRate { get; set; }
    public decimal GrossSalary { get; set; }
    public decimal OutstandingAdvance { get; set; }
    public decimal AdvanceDeduction { get; set; }
    public decimal OtherDeductions { get; set; }
    public decimal Bonus { get; set; }
    public decimal NetSalary { get; set; }
    public string Status { get; set; } = "Pending"; // Pending, Paid
    public bool IsPaid { get; set; }
    public int? PayrollRecordId { get; set; }
    public DateTime? PaymentDate { get; set; }
    public string PaymentMethod { get; set; } = "Cash";
}

public class DisburseSalaryDto
{
    public int EmployeeId { get; set; }
    public int Month { get; set; }
    public int Year { get; set; }
    public decimal AdvanceDeduction { get; set; }
    public decimal OtherDeductions { get; set; }
    public decimal Bonus { get; set; }
    public string PaymentMethod { get; set; } = "Cash";
    public string Notes { get; set; } = string.Empty;
}

public class StockMovementLogDto
{
    public int Id { get; set; }
    public int InventoryItemId { get; set; }
    public string ItemName { get; set; } = string.Empty;
    public string Category { get; set; } = string.Empty;
    public string Unit { get; set; } = "KG";
    public decimal Quantity { get; set; }
    public string TransactionType { get; set; } = "IN";
    public string Notes { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
}

public class ComprehensiveReportDto
{
    public string PeriodType { get; set; } = "DayWise"; // DayWise, MonthWise, YearWise, Custom
    public DateTime StartDate { get; set; }
    public DateTime EndDate { get; set; }
    public int SelectedYear { get; set; } = DateTime.UtcNow.Year;
    public int SelectedMonth { get; set; } = DateTime.UtcNow.Month;
    
    // High Level KPIs
    public decimal TotalRevenue { get; set; }
    public int TotalOrders { get; set; }
    public decimal AverageOrderValue => TotalOrders > 0 ? Math.Round(TotalRevenue / TotalOrders, 2) : 0m;
    public decimal DineInSales { get; set; }
    public decimal TakeawaySales { get; set; }
    public decimal DeliverySales { get; set; }
    public decimal TotalTax { get; set; }
    public decimal TotalCGST { get; set; }
    public decimal TotalSGST { get; set; }
    public decimal TotalDiscount { get; set; }

    // Breakdown Rows
    public List<ReportPeriodSummaryDto> BreakdownList { get; set; } = new();
    public List<PaymentMethodSummaryDto> PaymentBreakdown { get; set; } = new();
    public List<CategorySalesSummaryDto> CategoryBreakdown { get; set; } = new();
}

public class ReportPeriodSummaryDto
{
    public string PeriodLabel { get; set; } = string.Empty; // e.g. "25 Oct 2026", "October 2026", "2026"
    public DateTime Date { get; set; }
    public int OrdersCount { get; set; }
    public decimal GrossSales { get; set; }
    public decimal TaxCollected { get; set; }
    public decimal DiscountGiven { get; set; }
    public decimal NetRevenue { get; set; }
    public int DineInCount { get; set; }
    public int TakeawayCount { get; set; }
}

public class PaymentMethodSummaryDto
{
    public string Method { get; set; } = string.Empty;
    public decimal TotalAmount { get; set; }
    public int TransactionCount { get; set; }
    public decimal Percentage { get; set; }
}

public class CategorySalesSummaryDto
{
    public string CategoryName { get; set; } = string.Empty;
    public decimal TotalSales { get; set; }
    public int ItemsSold { get; set; }
    public decimal Percentage { get; set; }
}

public class CustomerLedgerDto
{
    public int Id { get; set; }
    public int CustomerId { get; set; }
    public string CustomerName { get; set; } = string.Empty;
    public string CustomerPhone { get; set; } = string.Empty;
    public DateTime Date { get; set; }
    public string Description { get; set; } = string.Empty;
    public decimal DebitAmount { get; set; }
    public decimal CreditAmount { get; set; }
    public decimal RunningBalance { get; set; }
    public string ReferenceInvoiceNumber { get; set; } = string.Empty;
    public string Notes { get; set; } = string.Empty;
    public string RecordedBy { get; set; } = string.Empty;
}

public class RecordCustomerCreditDto
{
    public int CustomerId { get; set; }
    public decimal Amount { get; set; }
    public string Type { get; set; } = "CreditGiven"; // CreditGiven (Debit) or PaymentReceived (Credit)
    public string ReferenceInvoiceNumber { get; set; } = string.Empty;
    public string Notes { get; set; } = string.Empty;
    public string RecordedBy { get; set; } = "Admin";
}

public class LeaveRequestDto
{
    public int Id { get; set; }
    public int EmployeeId { get; set; }
    public string EmployeeName { get; set; } = string.Empty;
    public string Role { get; set; } = string.Empty;
    public DateTime StartDate { get; set; }
    public DateTime EndDate { get; set; }
    public int DaysCount => Math.Max(1, (int)(EndDate.Date - StartDate.Date).TotalDays + 1);
    public string LeaveType { get; set; } = "Casual";
    public string Reason { get; set; } = string.Empty;
    public string Status { get; set; } = "Pending";
    public string ApprovedBy { get; set; } = string.Empty;
    public DateTime? ApprovedDate { get; set; }
    public string RejectionReason { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}

public class CreateLeaveRequestDto
{
    public int EmployeeId { get; set; }
    public DateTime StartDate { get; set; } = DateTime.UtcNow.Date;
    public DateTime EndDate { get; set; } = DateTime.UtcNow.Date;
    public string LeaveType { get; set; } = "Casual";
    public string Reason { get; set; } = string.Empty;
}



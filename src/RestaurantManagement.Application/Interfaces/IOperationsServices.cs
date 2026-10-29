using RestaurantManagement.Application.DTOs;

namespace RestaurantManagement.Application.Interfaces;

public interface IInventoryService
{
    Task<IEnumerable<InventoryItemDto>> GetInventoryItemsAsync();
    Task<IEnumerable<InventoryItemDto>> GetLowStockItemsAsync();
    Task<InventoryItemDto> AdjustStockAsync(StockAdjustmentDto dto);
    Task<IEnumerable<StockMovementLogDto>> GetStockMovementLogsAsync(int? inventoryItemId = null);
    Task<InventoryItemDto> CreateInventoryItemAsync(InventoryItemDto dto);
    Task DeductStockForOrderAsync(int orderId);
}

public interface IStaffService
{
    Task<IEnumerable<EmployeeDto>> GetEmployeesAsync();
    Task<EmployeeDto?> GetEmployeeByIdAsync(int id);
    Task<EmployeeDto> CreateEmployeeAsync(EmployeeDto dto);
    Task<EmployeeDto> UpdateEmployeeAsync(int id, EmployeeDto dto);
    Task<bool> DeleteEmployeeAsync(int id);
    Task<IEnumerable<AttendanceDto>> GetTodayAttendanceAsync();
    Task<AttendanceDto> RecordAttendanceAsync(int employeeId, string status);
    Task<ExpenseDto> AddExpenseAsync(ExpenseDto dto);
    Task<IEnumerable<ExpenseDto>> GetExpensesAsync(DateTime? startDate = null, DateTime? endDate = null);

    // Employee Ledger & Advances
    Task<EmployeeLedgerSummaryDto> GetEmployeeLedgerSummaryAsync(int employeeId);
    Task<IEnumerable<EmployeeLedgerSummaryDto>> GetAllEmployeeLedgersAsync();
    Task<EmployeeLedgerDto> AddLedgerTransactionAsync(CreateLedgerTransactionDto dto);

    // Attendance-Based Payroll
    Task<IEnumerable<PayrollCalculationDto>> CalculateMonthlyPayrollAsync(int month, int year);
    Task<PayrollCalculationDto> DisburseSalaryAsync(DisburseSalaryDto dto);

    // Leave Management
    Task<IEnumerable<LeaveRequestDto>> GetLeaveRequestsAsync();
    Task<LeaveRequestDto> CreateLeaveRequestAsync(CreateLeaveRequestDto dto);
    Task<bool> ApproveLeaveRequestAsync(int leaveRequestId, string approvedBy);
    Task<bool> RejectLeaveRequestAsync(int leaveRequestId, string rejectedBy, string reason);

    // Customer Credit / Udhari Ledger
    Task<IEnumerable<CustomerLedgerDto>> GetCustomerLedgersAsync(int? customerId = null);
    Task<CustomerLedgerDto> RecordCustomerCreditTransactionAsync(RecordCustomerCreditDto dto);
}

public interface ISupplierService
{
    Task<IEnumerable<SupplierDto>> GetSuppliersAsync();
    Task<SupplierDto?> GetSupplierByIdAsync(int id);
    Task<SupplierDto> CreateSupplierAsync(SupplierCreateUpdateDto dto);
    Task<SupplierDto> UpdateSupplierAsync(int id, SupplierCreateUpdateDto dto);
    Task<bool> DeleteSupplierAsync(int id);
}

public interface IDashboardService
{
    Task<DashboardSummaryDto> GetDashboardSummaryAsync();
}

public interface IReportService
{
    Task<SalesReportDto> GetSalesReportAsync(DateTime startDate, DateTime endDate);
    Task<ComprehensiveReportDto> GetComprehensiveReportAsync(string periodType, int? year = null, int? month = null, DateTime? startDate = null, DateTime? endDate = null);
}

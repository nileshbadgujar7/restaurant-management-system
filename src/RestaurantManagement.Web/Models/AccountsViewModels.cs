using RestaurantManagement.Application.DTOs;

namespace RestaurantManagement.Web.Models;

public class AccountsViewModel
{
    public string ActiveTab { get; set; } = "ledger"; // ledger, payroll, expenses
    public decimal TotalRevenue { get; set; }
    public decimal TotalExpenses { get; set; }
    public decimal TotalAdvancesOutstanding { get; set; }
    public decimal TotalSalariesPaid { get; set; }
    public decimal NetOperatingBalance => TotalRevenue - TotalExpenses;

    // Tab 1: Employee Ledgers
    public List<EmployeeLedgerSummaryDto> EmployeeLedgers { get; set; } = new();
    public EmployeeLedgerSummaryDto? SelectedEmployeeLedger { get; set; }

    // Tab 2: Monthly Attendance Payroll
    public int SelectedMonth { get; set; } = DateTime.UtcNow.Month;
    public int SelectedYear { get; set; } = DateTime.UtcNow.Year;
    public List<PayrollCalculationDto> PayrollRecords { get; set; } = new();

    // Tab 3: Expenses
    public List<ExpenseDto> Expenses { get; set; } = new();
}

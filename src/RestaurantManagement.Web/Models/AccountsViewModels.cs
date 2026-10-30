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

    // Tab 4: Customer Credit (Udhari) Ledger
    public decimal TotalCustomerCreditOutstanding { get; set; }
    public List<CustomerLedgerDto> CustomerLedgers { get; set; } = new();
    public List<CustomerDto> Customers { get; set; } = new();

    // Tab 5: Income & Expenses Report
    public IncomeExpenseReportDto IncomeExpenseReport { get; set; } = new();
}

public class IncomeExpenseReportDto
{
    public string PeriodLabel { get; set; } = string.Empty;
    public int Month { get; set; }
    public int Year { get; set; }
    public DateTime StartDate { get; set; }
    public DateTime EndDate { get; set; }

    // Income Side
    public decimal DineInSales { get; set; }
    public decimal TakeawaySales { get; set; }
    public decimal DeliverySales { get; set; }
    public decimal CustomerCreditCollected { get; set; }
    public decimal TotalIncome { get; set; }

    // Expense Side
    public decimal SalaryDisbursements { get; set; }
    public decimal RawMaterialPurchases { get; set; }
    public decimal RentAndUtilities { get; set; }
    public decimal MaintenanceExpenses { get; set; }
    public decimal MarketingExpenses { get; set; }
    public decimal MiscellaneousExpenses { get; set; }
    public decimal TotalExpenses { get; set; }

    // Bottom Line
    public decimal NetProfitOrLoss => TotalIncome - TotalExpenses;
    public decimal ProfitMargin => TotalIncome > 0 ? Math.Round((NetProfitOrLoss / TotalIncome) * 100m, 1) : 0m;

    public List<IncomeExpenseItemDto> LineItems { get; set; } = new();
}

public class IncomeExpenseItemDto
{
    public DateTime Date { get; set; }
    public string Type { get; set; } = "Income"; // Income or Expense
    public string Category { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string ReferenceNumber { get; set; } = string.Empty;
    public decimal Amount { get; set; }
    public string PaymentMode { get; set; } = "Cash";
}


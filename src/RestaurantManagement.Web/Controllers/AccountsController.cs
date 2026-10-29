using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using RestaurantManagement.Application.DTOs;
using RestaurantManagement.Application.Interfaces;
using RestaurantManagement.Domain.Enums;
using RestaurantManagement.Web.Models;

namespace RestaurantManagement.Web.Controllers;

[Authorize(Roles = "Admin,Manager,Cashier")]
public class AccountsController : Controller
{
    private readonly IStaffService _staffService;
    private readonly IReportService _reportService;
    private readonly IUnitOfWork _unitOfWork;

    public AccountsController(IStaffService staffService, IReportService reportService, IUnitOfWork unitOfWork)
    {
        _staffService = staffService;
        _reportService = reportService;
        _unitOfWork = unitOfWork;
    }

    public async Task<IActionResult> Index(string tab = "ledger", int? employeeId = null, int? month = null, int? year = null)
    {
        int curMonth = month ?? DateTime.UtcNow.Month;
        int curYear = year ?? DateTime.UtcNow.Year;

        var ledgers = (await _staffService.GetAllEmployeeLedgersAsync()).ToList();
        EmployeeLedgerSummaryDto? selectedLedger = null;

        if (employeeId.HasValue && employeeId.Value > 0)
        {
            selectedLedger = ledgers.FirstOrDefault(l => l.EmployeeId == employeeId.Value);
            if (selectedLedger == null)
            {
                try { selectedLedger = await _staffService.GetEmployeeLedgerSummaryAsync(employeeId.Value); }
                catch { }
            }
        }
        else if (ledgers.Any())
        {
            selectedLedger = ledgers.FirstOrDefault(l => l.OutstandingAdvance > 0) ?? ledgers.FirstOrDefault();
        }

        var payroll = (await _staffService.CalculateMonthlyPayrollAsync(curMonth, curYear)).ToList();
        var expenses = (await _staffService.GetExpensesAsync()).ToList();

        // Customer Credit (Udhari)
        var customerLedgers = (await _staffService.GetCustomerLedgersAsync()).ToList();
        var allCustomers = (await _unitOfWork.Customers.GetAllAsync()).Where(c => !c.IsDeleted).Select(c => new CustomerDto
        {
            Id = c.Id,
            Name = c.Name,
            Phone = c.Phone,
            Email = c.Email,
            OutstandingCredit = c.OutstandingCredit,
            TotalVisits = c.TotalVisits,
            TotalSpent = c.TotalSpent
        }).ToList();
        decimal totalCustomerCredit = allCustomers.Sum(c => c.OutstandingCredit);

        // Financial KPIs
        var orders = await _unitOfWork.Orders.GetAllAsync();
        decimal totalRevenue = orders.Where(o => !o.IsDeleted && (o.Status == OrderStatus.Completed || o.IsSettled)).Sum(o => o.GrandTotal);
        decimal totalExpenses = expenses.Sum(e => e.Amount);
        decimal totalAdvances = ledgers.Sum(l => l.OutstandingAdvance);
        decimal salariesPaid = expenses.Where(e => e.Category == "Salaries").Sum(e => e.Amount);

        var viewModel = new AccountsViewModel
        {
            ActiveTab = tab.ToLower(),
            TotalRevenue = totalRevenue,
            TotalExpenses = totalExpenses,
            TotalAdvancesOutstanding = totalAdvances,
            TotalSalariesPaid = salariesPaid,
            TotalCustomerCreditOutstanding = totalCustomerCredit,
            EmployeeLedgers = ledgers,
            SelectedEmployeeLedger = selectedLedger,
            SelectedMonth = curMonth,
            SelectedYear = curYear,
            PayrollRecords = payroll,
            Expenses = expenses,
            CustomerLedgers = customerLedgers,
            Customers = allCustomers
        };

        return View(viewModel);
    }

    [HttpPost]
    public async Task<IActionResult> CollectCustomerPayment(RecordCustomerCreditDto dto)
    {
        try
        {
            dto.Type = "PaymentReceived";
            dto.RecordedBy = User.Identity?.Name ?? "Cashier";
            await _staffService.RecordCustomerCreditTransactionAsync(dto);
            TempData["Success"] = $"Collected credit payment of ₹{dto.Amount:N0} successfully.";
        }
        catch (Exception ex)
        {
            TempData["Error"] = ex.Message;
        }

        return RedirectToAction(nameof(Index), new { tab = "customercredit" });
    }

    [HttpPost]
    public async Task<IActionResult> GiveAdvance(CreateLedgerTransactionDto dto)
    {
        try
        {
            dto.TransactionType = "Advance";
            dto.RecordedBy = User.Identity?.Name ?? "Admin";
            await _staffService.AddLedgerTransactionAsync(dto);
            TempData["Success"] = $"Advance payment of ₹{dto.Amount:N0} successfully issued to employee.";
        }
        catch (Exception ex)
        {
            TempData["Error"] = ex.Message;
        }

        return RedirectToAction(nameof(Index), new { tab = "ledger", employeeId = dto.EmployeeId });
    }

    [HttpPost]
    public async Task<IActionResult> CollectAdvancePayment(CreateLedgerTransactionDto dto)
    {
        try
        {
            dto.TransactionType = "Repayment";
            dto.RecordedBy = User.Identity?.Name ?? "Cashier";
            await _staffService.AddLedgerTransactionAsync(dto);
            TempData["Success"] = $"Repayment collection of ₹{dto.Amount:N0} successfully recorded.";
        }
        catch (Exception ex)
        {
            TempData["Error"] = ex.Message;
        }

        return RedirectToAction(nameof(Index), new { tab = "ledger", employeeId = dto.EmployeeId });
    }

    [HttpPost]
    public async Task<IActionResult> DisburseSalary(DisburseSalaryDto dto)
    {
        try
        {
            var res = await _staffService.DisburseSalaryAsync(dto);
            TempData["Success"] = $"Salary for {res.EmployeeName} issued successfully! Net paid: ₹{res.NetSalary:N0} (Advance deducted: ₹{res.AdvanceDeduction:N0})";
        }
        catch (Exception ex)
        {
            TempData["Error"] = ex.Message;
        }

        return RedirectToAction(nameof(Index), new { tab = "payroll", month = dto.Month, year = dto.Year });
    }

    [HttpGet]
    public async Task<IActionResult> SalarySlip(int employeeId, int month, int year)
    {
        var payrollList = await _staffService.CalculateMonthlyPayrollAsync(month, year);
        var record = payrollList.FirstOrDefault(p => p.EmployeeId == employeeId);
        if (record == null)
        {
            TempData["Error"] = "Salary record not found.";
            return RedirectToAction(nameof(Index), new { tab = "payroll" });
        }

        var setting = (await _unitOfWork.RestaurantSettings.GetAllAsync()).FirstOrDefault();
        ViewBag.Setting = setting;
        return View(record);
    }

    [HttpPost]
    public async Task<IActionResult> AddExpense(ExpenseDto dto)
    {
        try
        {
            dto.RecordedBy = User.Identity?.Name ?? "Admin";
            await _staffService.AddExpenseAsync(dto);
            TempData["Success"] = "Expense recorded successfully.";
        }
        catch (Exception ex)
        {
            TempData["Error"] = ex.Message;
        }

        return RedirectToAction(nameof(Index), new { tab = "expenses" });
    }
}

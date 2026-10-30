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
        decimal salariesPaid = expenses.Where(e => e.Category == "Salaries" || e.Category == "Payroll").Sum(e => e.Amount);

        // Calculate Income & Expense Report for Selected Month/Year
        DateTime monthStart = new DateTime(curYear, curMonth, 1);
        DateTime monthEnd = new DateTime(curYear, curMonth, DateTime.DaysInMonth(curYear, curMonth), 23, 59, 59);

        var monthOrders = orders
            .Where(o => !o.IsDeleted && (o.Status == OrderStatus.Completed || o.IsSettled) && o.CreatedAt >= monthStart && o.CreatedAt <= monthEnd)
            .ToList();

        decimal monthDineIn = monthOrders.Where(o => o.OrderType == OrderType.DineIn).Sum(o => o.GrandTotal);
        decimal monthTakeaway = monthOrders.Where(o => o.OrderType == OrderType.Takeaway).Sum(o => o.GrandTotal);
        decimal monthDelivery = monthOrders.Where(o => o.OrderType == OrderType.Delivery).Sum(o => o.GrandTotal);
        decimal monthCreditCollected = customerLedgers.Where(c => c.Date >= monthStart && c.Date <= monthEnd && c.CreditAmount > 0).Sum(c => c.CreditAmount);
        decimal totalMonthIncome = monthDineIn + monthTakeaway + monthDelivery + monthCreditCollected;

        var monthExpenses = expenses.Where(e => e.ExpenseDate >= monthStart && e.ExpenseDate <= monthEnd).ToList();
        decimal monthSalaries = monthExpenses.Where(e => e.Category == "Salaries" || e.Category == "Payroll").Sum(e => e.Amount);
        if (monthSalaries == 0)
        {
            monthSalaries = payroll.Sum(p => p.NetSalary);
        }
        decimal monthRawMaterials = monthExpenses.Where(e => e.Category == "Raw Materials" || e.Category == "Stock" || e.Category == "Groceries").Sum(e => e.Amount);
        decimal monthRentUtils = monthExpenses.Where(e => e.Category == "Rent" || e.Category == "Electricity" || e.Category == "Gas" || e.Category == "Utilities").Sum(e => e.Amount);
        decimal monthMaint = monthExpenses.Where(e => e.Category == "Maintenance" || e.Category == "Repairs").Sum(e => e.Amount);
        decimal monthMarketing = monthExpenses.Where(e => e.Category == "Marketing" || e.Category == "Advertising").Sum(e => e.Amount);
        decimal monthMisc = monthExpenses.Where(e => e.Category == "Miscellaneous" || e.Category == "General" || string.IsNullOrEmpty(e.Category)).Sum(e => e.Amount);
        decimal totalMonthExpenses = monthExpenses.Sum(e => e.Amount);
        if (totalMonthExpenses == 0 && monthSalaries > 0)
        {
            totalMonthExpenses = monthSalaries;
        }

        var lineItems = new List<IncomeExpenseItemDto>();
        foreach (var o in monthOrders.OrderByDescending(o => o.CreatedAt))
        {
            lineItems.Add(new IncomeExpenseItemDto
            {
                Date = o.CreatedAt,
                Type = "Income",
                Category = o.OrderType == OrderType.DineIn ? "Dine-in Sales" : "Takeaway / Parcel",
                Description = $"Order #{o.OrderNumber} - {o.CustomerName}",
                ReferenceNumber = o.OrderNumber,
                Amount = o.GrandTotal,
                PaymentMode = "Settled"
            });
        }
        foreach (var e in monthExpenses.OrderByDescending(e => e.ExpenseDate))
        {
            lineItems.Add(new IncomeExpenseItemDto
            {
                Date = e.ExpenseDate,
                Type = "Expense",
                Category = e.Category,
                Description = string.IsNullOrEmpty(e.Description) ? e.Category : e.Description,
                ReferenceNumber = $"EXP-{e.Id}",
                Amount = e.Amount,
                PaymentMode = "Cash"
            });
        }
        lineItems = lineItems.OrderByDescending(i => i.Date).ToList();

        var incomeExpenseReport = new IncomeExpenseReportDto
        {
            PeriodLabel = $"{monthStart:MMMM yyyy}",
            Month = curMonth,
            Year = curYear,
            StartDate = monthStart,
            EndDate = monthEnd,
            DineInSales = monthDineIn,
            TakeawaySales = monthTakeaway,
            DeliverySales = monthDelivery,
            CustomerCreditCollected = monthCreditCollected,
            TotalIncome = totalMonthIncome,
            SalaryDisbursements = monthSalaries,
            RawMaterialPurchases = monthRawMaterials,
            RentAndUtilities = monthRentUtils,
            MaintenanceExpenses = monthMaint,
            MarketingExpenses = monthMarketing,
            MiscellaneousExpenses = monthMisc,
            TotalExpenses = totalMonthExpenses,
            LineItems = lineItems
        };

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
            Customers = allCustomers,
            IncomeExpenseReport = incomeExpenseReport
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

    [HttpGet]
    public async Task<IActionResult> ExportIncomeExpenseExcel(int? month = null, int? year = null)
    {
        int curMonth = month ?? DateTime.UtcNow.Month;
        int curYear = year ?? DateTime.UtcNow.Year;

        DateTime monthStart = new DateTime(curYear, curMonth, 1);
        DateTime monthEnd = new DateTime(curYear, curMonth, DateTime.DaysInMonth(curYear, curMonth), 23, 59, 59);

        var orders = await _unitOfWork.Orders.GetAllAsync();
        var monthOrders = orders
            .Where(o => !o.IsDeleted && (o.Status == OrderStatus.Completed || o.IsSettled) && o.CreatedAt >= monthStart && o.CreatedAt <= monthEnd)
            .ToList();

        var expenses = (await _staffService.GetExpensesAsync())
            .Where(e => e.ExpenseDate >= monthStart && e.ExpenseDate <= monthEnd)
            .ToList();

        var customerLedgers = (await _staffService.GetCustomerLedgersAsync())
            .Where(c => c.Date >= monthStart && c.Date <= monthEnd && c.CreditAmount > 0)
            .ToList();

        decimal dineIn = monthOrders.Where(o => o.OrderType == OrderType.DineIn).Sum(o => o.GrandTotal);
        decimal takeaway = monthOrders.Where(o => o.OrderType == OrderType.Takeaway).Sum(o => o.GrandTotal);
        decimal delivery = monthOrders.Where(o => o.OrderType == OrderType.Delivery).Sum(o => o.GrandTotal);
        decimal creditCollected = customerLedgers.Sum(c => c.CreditAmount);
        decimal totalIncome = dineIn + takeaway + delivery + creditCollected;
        decimal totalExpenses = expenses.Sum(e => e.Amount);
        decimal netProfit = totalIncome - totalExpenses;

        var sb = new System.Text.StringBuilder();
        sb.AppendLine($"INCOME & EXPENSES (PROFIT & LOSS) REPORT");
        sb.AppendLine($"Period: {monthStart:MMMM yyyy} ({monthStart:dd-MMM-yyyy} to {monthEnd:dd-MMM-yyyy})");
        sb.AppendLine($"Total Income: INR {totalIncome:N2}, Total Expenses: INR {totalExpenses:N2}, Net Profit/Loss: INR {netProfit:N2}");
        sb.AppendLine();

        sb.AppendLine("=== INCOME SUMMARY ===");
        sb.AppendLine("Source / Category,Amount (INR)");
        sb.AppendLine($"\"Dine-In Sales\",{dineIn:F2}");
        sb.AppendLine($"\"Takeaway / Parcel Sales\",{takeaway:F2}");
        sb.AppendLine($"\"Delivery Orders\",{delivery:F2}");
        sb.AppendLine($"\"Customer Credit Collections (Udhari)\",{creditCollected:F2}");
        sb.AppendLine($"\"TOTAL INCOME\",{totalIncome:F2}");
        sb.AppendLine();

        sb.AppendLine("=== EXPENSES SUMMARY ===");
        sb.AppendLine("Expense Category,Amount (INR)");
        var expenseGroups = expenses.GroupBy(e => string.IsNullOrWhiteSpace(e.Category) ? "General" : e.Category);
        foreach (var eg in expenseGroups)
        {
            sb.AppendLine($"\"{eg.Key}\",{eg.Sum(e => e.Amount):F2}");
        }
        sb.AppendLine($"\"TOTAL EXPENSES\",{totalExpenses:F2}");
        sb.AppendLine();
        sb.AppendLine($"\"NET PROFIT / (LOSS)\",{netProfit:F2}");
        sb.AppendLine();

        sb.AppendLine("=== DETAILED LINE TRANSACTIONS ===");
        sb.AppendLine("Date & Time,Type,Category,Description,Reference #,Payment Mode,Amount (INR)");
        foreach (var o in monthOrders.OrderByDescending(o => o.CreatedAt))
        {
            sb.AppendLine($"{o.CreatedAt:yyyy-MM-dd HH:mm},\"Income\",\"{o.OrderType}\",\"{o.CustomerName}\",\"{o.OrderNumber}\",\"Settled\",{o.GrandTotal:F2}");
        }
        foreach (var e in expenses.OrderByDescending(e => e.ExpenseDate))
        {
            sb.AppendLine($"{e.ExpenseDate:yyyy-MM-dd HH:mm},\"Expense\",\"{e.Category}\",\"{e.Description}\",\"EXP-{e.Id}\",\"Expense\",-{e.Amount:F2}");
        }

        byte[] preamble = System.Text.Encoding.UTF8.GetPreamble();
        byte[] content = System.Text.Encoding.UTF8.GetBytes(sb.ToString());
        byte[] fileBytes = new byte[preamble.Length + content.Length];
        Buffer.BlockCopy(preamble, 0, fileBytes, 0, preamble.Length);
        Buffer.BlockCopy(content, 0, fileBytes, preamble.Length, content.Length);

        string fileName = $"Income_Expenses_Report_{curYear}_{curMonth:D2}.csv";
        return File(fileBytes, "text/csv; charset=utf-8", fileName);
    }
}

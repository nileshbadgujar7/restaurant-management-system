using RestaurantManagement.Application.DTOs;
using RestaurantManagement.Application.Interfaces;
using RestaurantManagement.Domain.Entities;
using RestaurantManagement.Domain.Enums;

namespace RestaurantManagement.Application.Services;

public class InventoryService : IInventoryService
{
    private readonly IUnitOfWork _unitOfWork;

    public InventoryService(IUnitOfWork unitOfWork)
    {
        _unitOfWork = unitOfWork;
    }

    public async Task<IEnumerable<InventoryItemDto>> GetInventoryItemsAsync()
    {
        var items = await _unitOfWork.InventoryItems.GetAllAsync();
        var suppliers = (await _unitOfWork.Suppliers.GetAllAsync()).ToDictionary(s => s.Id, s => s.Name);
        return items.Where(i => !i.IsDeleted).Select(i =>
        {
            var dto = MapToDto(i);
            if (i.SupplierId.HasValue && suppliers.TryGetValue(i.SupplierId.Value, out var sName))
            {
                dto.SupplierName = sName;
            }
            return dto;
        });
    }

    public async Task<IEnumerable<InventoryItemDto>> GetLowStockItemsAsync()
    {
        var items = await _unitOfWork.InventoryItems.GetAllAsync();
        return items.Where(i => !i.IsDeleted && i.CurrentStock <= i.MinimumStock).Select(MapToDto);
    }

    public async Task<InventoryItemDto> AdjustStockAsync(StockAdjustmentDto dto)
    {
        var item = await _unitOfWork.InventoryItems.GetByIdAsync(dto.InventoryItemId);
        if (item == null) throw new KeyNotFoundException($"Inventory item {dto.InventoryItemId} not found");

        if (dto.TransactionType.ToUpper() == "IN")
        {
            item.CurrentStock += dto.Quantity;
        }
        else if (dto.TransactionType.ToUpper() == "OUT" || dto.TransactionType.ToUpper() == "WASTAGE")
        {
            if (item.CurrentStock < dto.Quantity) throw new InvalidOperationException($"Cannot deduct {dto.Quantity} {item.Unit}. Current stock is {item.CurrentStock} {item.Unit}.");
            item.CurrentStock -= dto.Quantity;
        }

        item.UpdatedAt = DateTime.UtcNow;
        await _unitOfWork.InventoryItems.UpdateAsync(item);

        var txn = new StockTransaction
        {
            InventoryItemId = item.Id,
            Quantity = dto.Quantity,
            TransactionType = dto.TransactionType,
            Notes = dto.Notes
        };
        await _unitOfWork.StockTransactions.AddAsync(txn);
        await _unitOfWork.SaveChangesAsync();

        return MapToDto(item);
    }

    public async Task DeductStockForOrderAsync(int orderId)
    {
        var orderItems = await _unitOfWork.OrderItems.FindAsync(i => i.OrderId == orderId && !i.IsDeleted);
        var recipes = await _unitOfWork.Recipes.GetAllAsync();
        var recipeItems = await _unitOfWork.RecipeItems.GetAllAsync();

        foreach (var orderItem in orderItems)
        {
            var recipe = recipes.FirstOrDefault(r => r.MenuItemId == orderItem.MenuItemId && !r.IsDeleted);
            if (recipe != null)
            {
                var rItems = recipeItems.Where(ri => ri.RecipeId == recipe.Id && !ri.IsDeleted);
                foreach (var ri in rItems)
                {
                    decimal needed = ri.QuantityRequired * orderItem.Quantity;
                    var invItem = await _unitOfWork.InventoryItems.GetByIdAsync(ri.InventoryItemId);
                    if (invItem != null)
                    {
                        invItem.CurrentStock = Math.Max(0, invItem.CurrentStock - needed);
                        await _unitOfWork.InventoryItems.UpdateAsync(invItem);

                        await _unitOfWork.StockTransactions.AddAsync(new StockTransaction
                        {
                            InventoryItemId = invItem.Id,
                            Quantity = needed,
                            TransactionType = "RECIPE_DEDUCTION",
                            Notes = $"Auto-deducted for Order #{orderId}, Item: {orderItem.ItemName}"
                        });
                    }
                }
            }
        }
        await _unitOfWork.SaveChangesAsync();
    }

    public async Task<IEnumerable<StockMovementLogDto>> GetStockMovementLogsAsync(int? inventoryItemId = null)
    {
        var items = (await _unitOfWork.InventoryItems.GetAllAsync()).ToDictionary(i => i.Id);
        var txns = await _unitOfWork.StockTransactions.GetAllAsync();
        var query = txns.Where(t => !t.IsDeleted);

        if (inventoryItemId.HasValue)
        {
            query = query.Where(t => t.InventoryItemId == inventoryItemId.Value);
        }

        return query.OrderByDescending(t => t.CreatedAt).Select(t => new StockMovementLogDto
        {
            Id = t.Id,
            InventoryItemId = t.InventoryItemId,
            ItemName = items.TryGetValue(t.InventoryItemId, out var item) ? item.Name : "Item #" + t.InventoryItemId,
            Category = items.TryGetValue(t.InventoryItemId, out var itm) ? itm.Category : "-",
            Unit = items.TryGetValue(t.InventoryItemId, out var u) ? u.Unit : "KG",
            Quantity = t.Quantity,
            TransactionType = t.TransactionType,
            Notes = t.Notes,
            CreatedAt = t.CreatedAt
        });
    }

    public async Task<InventoryItemDto> CreateInventoryItemAsync(InventoryItemDto dto)
    {
        var item = new InventoryItem
        {
            Name = dto.Name.Trim(),
            Category = string.IsNullOrWhiteSpace(dto.Category) ? "General" : dto.Category.Trim(),
            Unit = string.IsNullOrWhiteSpace(dto.Unit) ? "KG" : dto.Unit.Trim(),
            CurrentStock = Math.Max(0, dto.CurrentStock),
            MinimumStock = Math.Max(0, dto.MinimumStock),
            UnitCost = Math.Max(0, dto.UnitCost),
            ReorderLevel = dto.ReorderLevel > 0 ? dto.ReorderLevel : dto.MinimumStock,
            SupplierId = dto.SupplierId,
            CreatedAt = DateTime.UtcNow
        };

        await _unitOfWork.InventoryItems.AddAsync(item);
        await _unitOfWork.SaveChangesAsync();

        if (item.CurrentStock > 0)
        {
            await _unitOfWork.StockTransactions.AddAsync(new StockTransaction
            {
                InventoryItemId = item.Id,
                Quantity = item.CurrentStock,
                TransactionType = "IN",
                Notes = "Opening stock balance",
                CreatedAt = DateTime.UtcNow
            });
            await _unitOfWork.SaveChangesAsync();
        }

        return MapToDto(item);
    }

    private static InventoryItemDto MapToDto(InventoryItem i)
    {
        return new InventoryItemDto
        {
            Id = i.Id,
            Name = i.Name,
            Category = i.Category,
            Unit = i.Unit,
            CurrentStock = i.CurrentStock,
            MinimumStock = i.MinimumStock,
            UnitCost = i.UnitCost,
            ReorderLevel = i.ReorderLevel,
            SupplierId = i.SupplierId,
            SupplierName = i.Supplier?.Name ?? string.Empty
        };
    }
}

public class StaffService : IStaffService
{
    private readonly IUnitOfWork _unitOfWork;

    public StaffService(IUnitOfWork unitOfWork)
    {
        _unitOfWork = unitOfWork;
    }

    public async Task<IEnumerable<EmployeeDto>> GetEmployeesAsync()
    {
        var employees = await _unitOfWork.Employees.GetAllAsync();
        return employees.Where(e => !e.IsDeleted).Select(MapToDto);
    }

    public async Task<EmployeeDto?> GetEmployeeByIdAsync(int id)
    {
        var emp = await _unitOfWork.Employees.GetByIdAsync(id);
        if (emp == null || emp.IsDeleted) return null;
        return MapToDto(emp);
    }

    public async Task<EmployeeDto> CreateEmployeeAsync(EmployeeDto dto)
    {
        var emp = new Employee
        {
            Name = dto.Name,
            Email = dto.Email,
            Phone = dto.Phone,
            Role = dto.Role,
            JoiningDate = dto.JoiningDate == default ? DateTime.UtcNow : dto.JoiningDate,
            BaseSalary = dto.BaseSalary,
            Shift = string.IsNullOrWhiteSpace(dto.Shift) ? "Morning" : dto.Shift,
            IsActive = true
        };

        await _unitOfWork.Employees.AddAsync(emp);
        await _unitOfWork.SaveChangesAsync();

        return MapToDto(emp);
    }

    public async Task<EmployeeDto> UpdateEmployeeAsync(int id, EmployeeDto dto)
    {
        var emp = await _unitOfWork.Employees.GetByIdAsync(id);
        if (emp == null || emp.IsDeleted) throw new KeyNotFoundException($"Employee with id {id} not found.");

        emp.Name = dto.Name;
        emp.Email = dto.Email;
        emp.Phone = dto.Phone;
        emp.Role = dto.Role;
        emp.BaseSalary = dto.BaseSalary;
        emp.Shift = string.IsNullOrWhiteSpace(dto.Shift) ? emp.Shift : dto.Shift;
        emp.IsActive = dto.IsActive;
        emp.UpdatedAt = DateTime.UtcNow;

        await _unitOfWork.Employees.UpdateAsync(emp);
        await _unitOfWork.SaveChangesAsync();

        return MapToDto(emp);
    }

    public async Task<bool> DeleteEmployeeAsync(int id)
    {
        var emp = await _unitOfWork.Employees.GetByIdAsync(id);
        if (emp == null || emp.IsDeleted) return false;

        emp.IsDeleted = true;
        emp.UpdatedAt = DateTime.UtcNow;
        await _unitOfWork.Employees.UpdateAsync(emp);
        await _unitOfWork.SaveChangesAsync();
        return true;
    }

    public async Task<IEnumerable<AttendanceDto>> GetTodayAttendanceAsync()
    {
        var today = DateTime.UtcNow.Date;
        var attendances = await _unitOfWork.Attendances.FindAsync(a => a.Date == today && !a.IsDeleted);
        var employees = (await _unitOfWork.Employees.GetAllAsync()).ToDictionary(e => e.Id);

        return attendances.Select(a => new AttendanceDto
        {
            Id = a.Id,
            EmployeeId = a.EmployeeId,
            EmployeeName = employees.TryGetValue(a.EmployeeId, out var e) ? e.Name : "",
            Date = a.Date,
            CheckInTime = a.CheckInTime,
            CheckOutTime = a.CheckOutTime,
            Status = a.Status
        });
    }

    public async Task<AttendanceDto> RecordAttendanceAsync(int employeeId, string status)
    {
        var today = DateTime.UtcNow.Date;
        var existing = (await _unitOfWork.Attendances.FindAsync(a => a.EmployeeId == employeeId && a.Date == today && !a.IsDeleted)).FirstOrDefault();

        if (existing == null)
        {
            existing = new Attendance
            {
                EmployeeId = employeeId,
                Date = today,
                CheckInTime = DateTime.UtcNow,
                Status = status
            };
            if (status == "Checked Out")
            {
                existing.CheckOutTime = DateTime.UtcNow;
            }
            await _unitOfWork.Attendances.AddAsync(existing);
        }
        else
        {
            if (status == "Checked Out")
            {
                existing.CheckOutTime = DateTime.UtcNow;
            }
            else if (existing.CheckOutTime.HasValue)
            {
                // If previously checked out and re-marking present/late, clear check out
                existing.CheckOutTime = null;
            }
            existing.Status = status;
            await _unitOfWork.Attendances.UpdateAsync(existing);
        }

        await _unitOfWork.SaveChangesAsync();

        var emp = await _unitOfWork.Employees.GetByIdAsync(employeeId);
        return new AttendanceDto
        {
            Id = existing.Id,
            EmployeeId = existing.EmployeeId,
            EmployeeName = emp?.Name ?? "",
            Date = existing.Date,
            CheckInTime = existing.CheckInTime,
            CheckOutTime = existing.CheckOutTime,
            Status = existing.Status
        };
    }

    public async Task<ExpenseDto> AddExpenseAsync(ExpenseDto dto)
    {
        var exp = new Expense
        {
            Category = dto.Category,
            Description = dto.Description,
            Amount = dto.Amount,
            ExpenseDate = DateTime.UtcNow,
            RecordedBy = dto.RecordedBy
        };

        await _unitOfWork.Expenses.AddAsync(exp);
        await _unitOfWork.SaveChangesAsync();

        dto.Id = exp.Id;
        dto.ExpenseDate = exp.ExpenseDate;
        return dto;
    }

    public async Task<IEnumerable<ExpenseDto>> GetExpensesAsync(DateTime? startDate = null, DateTime? endDate = null)
    {
        var expenses = await _unitOfWork.Expenses.GetAllAsync();
        var query = expenses.Where(e => !e.IsDeleted);

        if (startDate.HasValue) query = query.Where(e => e.ExpenseDate >= startDate.Value);
        if (endDate.HasValue) query = query.Where(e => e.ExpenseDate <= endDate.Value);

        return query.OrderByDescending(e => e.ExpenseDate).Select(e => new ExpenseDto
        {
            Id = e.Id,
            Category = e.Category,
            Description = e.Description,
            Amount = e.Amount,
            ExpenseDate = e.ExpenseDate,
            RecordedBy = e.RecordedBy
        });
    }

    public async Task<EmployeeLedgerSummaryDto> GetEmployeeLedgerSummaryAsync(int employeeId)
    {
        var emp = await _unitOfWork.Employees.GetByIdAsync(employeeId);
        if (emp == null || emp.IsDeleted) throw new KeyNotFoundException($"Employee with id {employeeId} not found.");

        var allEntries = (await _unitOfWork.EmployeeLedgers.FindAsync(l => l.EmployeeId == employeeId && !l.IsDeleted))
            .OrderBy(l => l.TransactionDate)
            .ToList();

        decimal runningBal = 0m;
        var dtoList = new List<EmployeeLedgerDto>();

        foreach (var entry in allEntries)
        {
            if (entry.TransactionType == "Advance")
            {
                runningBal += entry.Amount;
            }
            else if (entry.TransactionType == "Repayment" || entry.TransactionType == "SalaryDeduction")
            {
                runningBal -= entry.Amount;
            }

            dtoList.Add(new EmployeeLedgerDto
            {
                Id = entry.Id,
                EmployeeId = entry.EmployeeId,
                EmployeeName = emp.Name,
                EmployeeRole = emp.Role.ToString(),
                TransactionDate = entry.TransactionDate,
                TransactionType = entry.TransactionType,
                Amount = entry.Amount,
                PaymentMethod = entry.PaymentMethod,
                ReferenceNumber = entry.ReferenceNumber,
                Notes = entry.Notes,
                RecordedBy = entry.RecordedBy,
                RunningBalance = runningBal
            });
        }

        decimal totalAdvance = allEntries.Where(l => l.TransactionType == "Advance").Sum(l => l.Amount);
        decimal totalRepaid = allEntries.Where(l => l.TransactionType == "Repayment" || l.TransactionType == "SalaryDeduction").Sum(l => l.Amount);

        return new EmployeeLedgerSummaryDto
        {
            EmployeeId = emp.Id,
            EmployeeName = emp.Name,
            Phone = emp.Phone,
            Role = emp.Role.ToString(),
            BaseSalary = emp.BaseSalary,
            TotalAdvanceGiven = totalAdvance,
            TotalRepaidOrDeducted = totalRepaid,
            Transactions = dtoList.OrderByDescending(d => d.TransactionDate).ToList()
        };
    }

    public async Task<IEnumerable<EmployeeLedgerSummaryDto>> GetAllEmployeeLedgersAsync()
    {
        var employees = (await _unitOfWork.Employees.GetAllAsync()).Where(e => !e.IsDeleted).ToList();
        var allLedgers = (await _unitOfWork.EmployeeLedgers.GetAllAsync()).Where(l => !l.IsDeleted).ToList();

        var summaries = new List<EmployeeLedgerSummaryDto>();

        foreach (var emp in employees)
        {
            var empLedgers = allLedgers.Where(l => l.EmployeeId == emp.Id).OrderBy(l => l.TransactionDate).ToList();
            decimal runningBal = 0m;
            var dtoList = new List<EmployeeLedgerDto>();

            foreach (var entry in empLedgers)
            {
                if (entry.TransactionType == "Advance") runningBal += entry.Amount;
                else if (entry.TransactionType == "Repayment" || entry.TransactionType == "SalaryDeduction") runningBal -= entry.Amount;

                dtoList.Add(new EmployeeLedgerDto
                {
                    Id = entry.Id,
                    EmployeeId = entry.EmployeeId,
                    EmployeeName = emp.Name,
                    EmployeeRole = emp.Role.ToString(),
                    TransactionDate = entry.TransactionDate,
                    TransactionType = entry.TransactionType,
                    Amount = entry.Amount,
                    PaymentMethod = entry.PaymentMethod,
                    ReferenceNumber = entry.ReferenceNumber,
                    Notes = entry.Notes,
                    RecordedBy = entry.RecordedBy,
                    RunningBalance = runningBal
                });
            }

            decimal totalAdvance = empLedgers.Where(l => l.TransactionType == "Advance").Sum(l => l.Amount);
            decimal totalRepaid = empLedgers.Where(l => l.TransactionType == "Repayment" || l.TransactionType == "SalaryDeduction").Sum(l => l.Amount);

            summaries.Add(new EmployeeLedgerSummaryDto
            {
                EmployeeId = emp.Id,
                EmployeeName = emp.Name,
                Phone = emp.Phone,
                Role = emp.Role.ToString(),
                BaseSalary = emp.BaseSalary,
                TotalAdvanceGiven = totalAdvance,
                TotalRepaidOrDeducted = totalRepaid,
                Transactions = dtoList.OrderByDescending(d => d.TransactionDate).ToList()
            });
        }

        return summaries.OrderByDescending(s => s.OutstandingAdvance).ThenBy(s => s.EmployeeName);
    }

    public async Task<EmployeeLedgerDto> AddLedgerTransactionAsync(CreateLedgerTransactionDto dto)
    {
        if (dto.Amount <= 0) throw new ArgumentException("Amount must be greater than zero.");
        var emp = await _unitOfWork.Employees.GetByIdAsync(dto.EmployeeId);
        if (emp == null || emp.IsDeleted) throw new KeyNotFoundException($"Employee with id {dto.EmployeeId} not found.");

        var entry = new EmployeeLedger
        {
            EmployeeId = dto.EmployeeId,
            TransactionDate = DateTime.UtcNow,
            TransactionType = dto.TransactionType, // "Advance" or "Repayment"
            Amount = dto.Amount,
            PaymentMethod = string.IsNullOrWhiteSpace(dto.PaymentMethod) ? "Cash" : dto.PaymentMethod,
            ReferenceNumber = dto.ReferenceNumber ?? string.Empty,
            Notes = dto.Notes ?? string.Empty,
            RecordedBy = string.IsNullOrWhiteSpace(dto.RecordedBy) ? "Admin" : dto.RecordedBy,
            CreatedAt = DateTime.UtcNow
        };

        await _unitOfWork.EmployeeLedgers.AddAsync(entry);
        await _unitOfWork.SaveChangesAsync();

        var summary = await GetEmployeeLedgerSummaryAsync(dto.EmployeeId);
        var createdDto = summary.Transactions.FirstOrDefault(t => t.Id == entry.Id);
        return createdDto ?? new EmployeeLedgerDto
        {
            Id = entry.Id,
            EmployeeId = entry.EmployeeId,
            EmployeeName = emp.Name,
            Amount = entry.Amount,
            TransactionType = entry.TransactionType
        };
    }

    public async Task<IEnumerable<PayrollCalculationDto>> CalculateMonthlyPayrollAsync(int month, int year)
    {
        int daysInMonth = DateTime.DaysInMonth(year, month);
        var startOfMonth = new DateTime(year, month, 1);
        var endOfMonth = new DateTime(year, month, daysInMonth, 23, 59, 59);

        var employees = (await _unitOfWork.Employees.GetAllAsync()).Where(e => !e.IsDeleted).ToList();
        var attendances = (await _unitOfWork.Attendances.FindAsync(a => a.Date >= startOfMonth.Date && a.Date <= endOfMonth.Date && !a.IsDeleted)).ToList();
        var allLedgers = (await _unitOfWork.EmployeeLedgers.GetAllAsync()).Where(l => !l.IsDeleted).ToList();
        var existingPayrolls = (await _unitOfWork.PayrollRecords.FindAsync(p => p.Month == month && p.Year == year && !p.IsDeleted)).ToList();

        var results = new List<PayrollCalculationDto>();

        foreach (var emp in employees)
        {
            var empAttendances = attendances.Where(a => a.EmployeeId == emp.Id).ToList();
            var paidRecord = existingPayrolls.FirstOrDefault(p => p.EmployeeId == emp.Id);

            decimal presentCount = empAttendances.Count(a => a.Status == "Present" || a.Status == "Late");
            decimal halfDayCount = empAttendances.Count(a => a.Status == "Half day");
            decimal leaveCount = empAttendances.Count(a => a.Status == "Leave");
            decimal paidLeaveCount = Math.Min(leaveCount, 2m); // Policy: 2 paid leaves allowed per month
            decimal unpaidLeaveCount = Math.Max(0, daysInMonth - (presentCount + halfDayCount + paidLeaveCount));

            decimal effectivePaidDays = presentCount + (halfDayCount * 0.5m) + paidLeaveCount;
            decimal dailyRate = daysInMonth > 0 ? Math.Round(emp.BaseSalary / daysInMonth, 2) : 0m;
            decimal grossSalary = Math.Round(effectivePaidDays * dailyRate, 2);

            // Compute outstanding advance as of now
            var empLedgers = allLedgers.Where(l => l.EmployeeId == emp.Id).ToList();
            decimal totalAdv = empLedgers.Where(l => l.TransactionType == "Advance").Sum(l => l.Amount);
            decimal totalRep = empLedgers.Where(l => l.TransactionType == "Repayment" || l.TransactionType == "SalaryDeduction").Sum(l => l.Amount);
            decimal outstandingAdv = Math.Max(0, totalAdv - totalRep);

            if (paidRecord != null)
            {
                // Already disbursed
                results.Add(new PayrollCalculationDto
                {
                    EmployeeId = emp.Id,
                    EmployeeName = emp.Name,
                    Role = emp.Role.ToString(),
                    BaseSalary = paidRecord.BaseSalary,
                    Month = month,
                    Year = year,
                    TotalDaysInMonth = paidRecord.TotalDaysInMonth,
                    PresentDays = paidRecord.PresentDays,
                    HalfDays = paidRecord.HalfDays,
                    PaidLeaveDays = paidRecord.PaidLeaveDays,
                    UnpaidLeaveDays = paidRecord.UnpaidLeaveDays,
                    DailyRate = paidRecord.DailyRate,
                    GrossSalary = paidRecord.GrossSalary,
                    OutstandingAdvance = outstandingAdv,
                    AdvanceDeduction = paidRecord.AdvanceDeducted,
                    OtherDeductions = paidRecord.OtherDeductions,
                    Bonus = paidRecord.Bonus,
                    NetSalary = paidRecord.NetSalary,
                    Status = paidRecord.Status,
                    IsPaid = true,
                    PayrollRecordId = paidRecord.Id,
                    PaymentDate = paidRecord.PaymentDate,
                    PaymentMethod = paidRecord.PaymentMethod
                });
            }
            else
            {
                // Suggested auto-advance deduction (capped at 50% of gross or full outstanding)
                decimal suggestedAdvDeduction = Math.Min(outstandingAdv, Math.Round(grossSalary * 0.5m, 0));
                decimal netSalary = Math.Max(0, grossSalary - suggestedAdvDeduction);

                results.Add(new PayrollCalculationDto
                {
                    EmployeeId = emp.Id,
                    EmployeeName = emp.Name,
                    Role = emp.Role.ToString(),
                    BaseSalary = emp.BaseSalary,
                    Month = month,
                    Year = year,
                    TotalDaysInMonth = daysInMonth,
                    PresentDays = presentCount,
                    HalfDays = halfDayCount,
                    PaidLeaveDays = paidLeaveCount,
                    UnpaidLeaveDays = unpaidLeaveCount,
                    DailyRate = dailyRate,
                    GrossSalary = grossSalary,
                    OutstandingAdvance = outstandingAdv,
                    AdvanceDeduction = suggestedAdvDeduction,
                    OtherDeductions = 0m,
                    Bonus = 0m,
                    NetSalary = netSalary,
                    Status = "Pending",
                    IsPaid = false,
                    PayrollRecordId = null,
                    PaymentDate = null,
                    PaymentMethod = "Cash"
                });
            }
        }

        return results.OrderBy(r => r.IsPaid).ThenBy(r => r.EmployeeName);
    }

    public async Task<PayrollCalculationDto> DisburseSalaryAsync(DisburseSalaryDto dto)
    {
        var emp = await _unitOfWork.Employees.GetByIdAsync(dto.EmployeeId);
        if (emp == null || emp.IsDeleted) throw new KeyNotFoundException($"Employee with id {dto.EmployeeId} not found.");

        int daysInMonth = DateTime.DaysInMonth(dto.Year, dto.Month);
        var startOfMonth = new DateTime(dto.Year, dto.Month, 1);
        var endOfMonth = new DateTime(dto.Year, dto.Month, daysInMonth, 23, 59, 59);

        var attendances = (await _unitOfWork.Attendances.FindAsync(a => a.EmployeeId == dto.EmployeeId && a.Date >= startOfMonth.Date && a.Date <= endOfMonth.Date && !a.IsDeleted)).ToList();

        decimal presentCount = attendances.Count(a => a.Status == "Present" || a.Status == "Late");
        decimal halfDayCount = attendances.Count(a => a.Status == "Half day");
        decimal leaveCount = attendances.Count(a => a.Status == "Leave");
        decimal paidLeaveCount = Math.Min(leaveCount, 2m);
        decimal unpaidLeaveCount = Math.Max(0, daysInMonth - (presentCount + halfDayCount + paidLeaveCount));

        decimal effectivePaidDays = presentCount + (halfDayCount * 0.5m) + paidLeaveCount;
        decimal dailyRate = daysInMonth > 0 ? Math.Round(emp.BaseSalary / daysInMonth, 2) : 0m;
        decimal grossSalary = Math.Round(effectivePaidDays * dailyRate, 2);

        decimal advanceDeduction = Math.Max(0, dto.AdvanceDeduction);
        decimal otherDeduction = Math.Max(0, dto.OtherDeductions);
        decimal bonus = Math.Max(0, dto.Bonus);
        decimal netSalary = Math.Max(0, grossSalary - advanceDeduction - otherDeduction + bonus);

        // Check if payroll record already exists
        var existing = (await _unitOfWork.PayrollRecords.FindAsync(p => p.EmployeeId == dto.EmployeeId && p.Month == dto.Month && p.Year == dto.Year && !p.IsDeleted)).FirstOrDefault();

        if (existing != null)
        {
            existing.GrossSalary = grossSalary;
            existing.AdvanceDeducted = advanceDeduction;
            existing.OtherDeductions = otherDeduction;
            existing.Bonus = bonus;
            existing.NetSalary = netSalary;
            existing.PaymentMethod = string.IsNullOrWhiteSpace(dto.PaymentMethod) ? "Cash" : dto.PaymentMethod;
            existing.Status = "Paid";
            existing.PaymentDate = DateTime.UtcNow;
            existing.Notes = dto.Notes ?? string.Empty;
            existing.UpdatedAt = DateTime.UtcNow;
            await _unitOfWork.PayrollRecords.UpdateAsync(existing);
        }
        else
        {
            existing = new PayrollRecord
            {
                EmployeeId = emp.Id,
                Month = dto.Month,
                Year = dto.Year,
                BaseSalary = emp.BaseSalary,
                TotalDaysInMonth = daysInMonth,
                PresentDays = presentCount,
                HalfDays = halfDayCount,
                PaidLeaveDays = paidLeaveCount,
                UnpaidLeaveDays = unpaidLeaveCount,
                DailyRate = dailyRate,
                GrossSalary = grossSalary,
                AdvanceDeducted = advanceDeduction,
                OtherDeductions = otherDeduction,
                Bonus = bonus,
                NetSalary = netSalary,
                Status = "Paid",
                PaymentDate = DateTime.UtcNow,
                PaymentMethod = string.IsNullOrWhiteSpace(dto.PaymentMethod) ? "Cash" : dto.PaymentMethod,
                Notes = dto.Notes ?? string.Empty,
                ProcessedBy = "Admin",
                CreatedAt = DateTime.UtcNow
            };
            await _unitOfWork.PayrollRecords.AddAsync(existing);
        }

        // If advance was deducted, record SalaryDeduction in EmployeeLedger
        if (advanceDeduction > 0)
        {
            await _unitOfWork.EmployeeLedgers.AddAsync(new EmployeeLedger
            {
                EmployeeId = emp.Id,
                TransactionDate = DateTime.UtcNow,
                TransactionType = "SalaryDeduction",
                Amount = advanceDeduction,
                PaymentMethod = existing.PaymentMethod,
                ReferenceNumber = $"PAY-{dto.Month:D2}/{dto.Year}-ADV",
                Notes = $"Advance deducted from {new DateTime(dto.Year, dto.Month, 1):MMMM yyyy} salary",
                RecordedBy = "System Payroll",
                CreatedAt = DateTime.UtcNow
            });
        }

        // Record SalaryPayment in EmployeeLedger
        await _unitOfWork.EmployeeLedgers.AddAsync(new EmployeeLedger
        {
            EmployeeId = emp.Id,
            TransactionDate = DateTime.UtcNow,
            TransactionType = "SalaryPayment",
            Amount = netSalary,
            PaymentMethod = existing.PaymentMethod,
            ReferenceNumber = $"PAY-{dto.Month:D2}/{dto.Year}",
            Notes = $"Net salary payout for {new DateTime(dto.Year, dto.Month, 1):MMMM yyyy}",
            RecordedBy = "System Payroll",
            CreatedAt = DateTime.UtcNow
        });

        // Record in Expenses so cash flow / accounts track payroll cost
        await _unitOfWork.Expenses.AddAsync(new Expense
        {
            Category = "Salaries",
            Description = $"Salary paid to {emp.Name} ({emp.Role}) for {new DateTime(dto.Year, dto.Month, 1):MMMM yyyy}",
            Amount = netSalary + advanceDeduction,
            ExpenseDate = DateTime.UtcNow,
            RecordedBy = "System Payroll",
            CreatedAt = DateTime.UtcNow
        });

        await _unitOfWork.SaveChangesAsync();

        return new PayrollCalculationDto
        {
            EmployeeId = emp.Id,
            EmployeeName = emp.Name,
            Role = emp.Role.ToString(),
            BaseSalary = existing.BaseSalary,
            Month = dto.Month,
            Year = dto.Year,
            TotalDaysInMonth = daysInMonth,
            PresentDays = presentCount,
            HalfDays = halfDayCount,
            PaidLeaveDays = paidLeaveCount,
            UnpaidLeaveDays = unpaidLeaveCount,
            DailyRate = dailyRate,
            GrossSalary = grossSalary,
            AdvanceDeduction = advanceDeduction,
            OtherDeductions = otherDeduction,
            Bonus = bonus,
            NetSalary = netSalary,
            Status = "Paid",
            IsPaid = true,
            PayrollRecordId = existing.Id,
            PaymentDate = existing.PaymentDate,
            PaymentMethod = existing.PaymentMethod
        };
    }

    public async Task<IEnumerable<LeaveRequestDto>> GetLeaveRequestsAsync()
    {
        var list = await _unitOfWork.LeaveRequests.GetAllAsync();
        var emps = (await _unitOfWork.Employees.GetAllAsync()).ToDictionary(e => e.Id);

        return list.Where(l => !l.IsDeleted).OrderByDescending(l => l.StartDate).Select(l => new LeaveRequestDto
        {
            Id = l.Id,
            EmployeeId = l.EmployeeId,
            EmployeeName = emps.TryGetValue(l.EmployeeId, out var e) ? e.Name : "Staff",
            Role = emps.TryGetValue(l.EmployeeId, out var emp) ? emp.Role.ToString() : "Staff",
            StartDate = l.StartDate,
            EndDate = l.EndDate,
            LeaveType = l.LeaveType,
            Reason = l.Reason,
            Status = l.Status,
            ApprovedBy = l.ApprovedBy,
            ApprovedDate = l.ApprovedDate,
            RejectionReason = l.RejectionReason,
            CreatedAt = l.CreatedAt
        });
    }

    public async Task<LeaveRequestDto> CreateLeaveRequestAsync(CreateLeaveRequestDto dto)
    {
        var emp = await _unitOfWork.Employees.GetByIdAsync(dto.EmployeeId);
        if (emp == null || emp.IsDeleted) throw new KeyNotFoundException($"Employee {dto.EmployeeId} not found.");

        var leave = new LeaveRequest
        {
            EmployeeId = dto.EmployeeId,
            StartDate = dto.StartDate.Date,
            EndDate = dto.EndDate.Date,
            LeaveType = string.IsNullOrWhiteSpace(dto.LeaveType) ? "Casual" : dto.LeaveType,
            Reason = dto.Reason ?? string.Empty,
            Status = "Pending",
            CreatedAt = DateTime.UtcNow
        };

        await _unitOfWork.LeaveRequests.AddAsync(leave);
        await _unitOfWork.SaveChangesAsync();

        return new LeaveRequestDto
        {
            Id = leave.Id,
            EmployeeId = leave.EmployeeId,
            EmployeeName = emp.Name,
            Role = emp.Role.ToString(),
            StartDate = leave.StartDate,
            EndDate = leave.EndDate,
            LeaveType = leave.LeaveType,
            Reason = leave.Reason,
            Status = leave.Status
        };
    }

    public async Task<bool> ApproveLeaveRequestAsync(int leaveRequestId, string approvedBy)
    {
        var leave = await _unitOfWork.LeaveRequests.GetByIdAsync(leaveRequestId);
        if (leave == null || leave.IsDeleted) return false;

        leave.Status = "Approved";
        leave.ApprovedBy = string.IsNullOrWhiteSpace(approvedBy) ? "Manager" : approvedBy;
        leave.ApprovedDate = DateTime.UtcNow;
        leave.UpdatedAt = DateTime.UtcNow;
        await _unitOfWork.LeaveRequests.UpdateAsync(leave);

        for (var dt = leave.StartDate.Date; dt <= leave.EndDate.Date; dt = dt.AddDays(1))
        {
            var existingAtt = (await _unitOfWork.Attendances.FindAsync(a => a.EmployeeId == leave.EmployeeId && a.Date == dt && !a.IsDeleted)).FirstOrDefault();
            if (existingAtt != null)
            {
                existingAtt.Status = "Leave";
                existingAtt.UpdatedAt = DateTime.UtcNow;
                await _unitOfWork.Attendances.UpdateAsync(existingAtt);
            }
            else
            {
                await _unitOfWork.Attendances.AddAsync(new Attendance
                {
                    EmployeeId = leave.EmployeeId,
                    Date = dt,
                    CheckInTime = dt.AddHours(9),
                    Status = "Leave",
                    CreatedAt = DateTime.UtcNow
                });
            }
        }

        await _unitOfWork.SaveChangesAsync();
        return true;
    }

    public async Task<bool> RejectLeaveRequestAsync(int leaveRequestId, string rejectedBy, string reason)
    {
        var leave = await _unitOfWork.LeaveRequests.GetByIdAsync(leaveRequestId);
        if (leave == null || leave.IsDeleted) return false;

        leave.Status = "Rejected";
        leave.ApprovedBy = string.IsNullOrWhiteSpace(rejectedBy) ? "Manager" : rejectedBy;
        leave.RejectionReason = reason ?? string.Empty;
        leave.ApprovedDate = DateTime.UtcNow;
        leave.UpdatedAt = DateTime.UtcNow;

        await _unitOfWork.LeaveRequests.UpdateAsync(leave);
        await _unitOfWork.SaveChangesAsync();
        return true;
    }

    public async Task<IEnumerable<CustomerLedgerDto>> GetCustomerLedgersAsync(int? customerId = null)
    {
        var list = await _unitOfWork.CustomerLedgers.GetAllAsync();
        var custs = (await _unitOfWork.Customers.GetAllAsync()).ToDictionary(c => c.Id);

        var query = list.Where(l => !l.IsDeleted);
        if (customerId.HasValue) query = query.Where(l => l.CustomerId == customerId.Value);

        return query.OrderByDescending(l => l.Date).Select(l => new CustomerLedgerDto
        {
            Id = l.Id,
            CustomerId = l.CustomerId,
            CustomerName = custs.TryGetValue(l.CustomerId, out var c) ? c.Name : "Customer",
            CustomerPhone = custs.TryGetValue(l.CustomerId, out var cu) ? cu.Phone : "",
            Date = l.Date,
            Description = l.Description,
            DebitAmount = l.DebitAmount,
            CreditAmount = l.CreditAmount,
            RunningBalance = l.RunningBalance,
            ReferenceInvoiceNumber = l.ReferenceInvoiceNumber,
            Notes = l.Notes,
            RecordedBy = l.RecordedBy
        });
    }

    public async Task<CustomerLedgerDto> RecordCustomerCreditTransactionAsync(RecordCustomerCreditDto dto)
    {
        var cust = await _unitOfWork.Customers.GetByIdAsync(dto.CustomerId);
        if (cust == null || cust.IsDeleted) throw new KeyNotFoundException($"Customer {dto.CustomerId} not found.");

        decimal debit = dto.Type == "CreditGiven" ? dto.Amount : 0m;
        decimal credit = dto.Type == "PaymentReceived" ? dto.Amount : 0m;

        decimal currentBalance = cust.OutstandingCredit;
        decimal newBalance = currentBalance + debit - credit;
        cust.OutstandingCredit = Math.Max(0m, newBalance);
        cust.UpdatedAt = DateTime.UtcNow;
        await _unitOfWork.Customers.UpdateAsync(cust);

        var ledger = new CustomerLedger
        {
            CustomerId = cust.Id,
            Date = DateTime.UtcNow,
            Description = dto.Type == "CreditGiven" ? "Udhari / Credit on Bill" : "Payment / Recovery Received",
            DebitAmount = debit,
            CreditAmount = credit,
            RunningBalance = cust.OutstandingCredit,
            ReferenceInvoiceNumber = dto.ReferenceInvoiceNumber ?? string.Empty,
            Notes = dto.Notes ?? string.Empty,
            RecordedBy = string.IsNullOrWhiteSpace(dto.RecordedBy) ? "Cashier" : dto.RecordedBy,
            CreatedAt = DateTime.UtcNow
        };

        await _unitOfWork.CustomerLedgers.AddAsync(ledger);
        await _unitOfWork.SaveChangesAsync();

        return new CustomerLedgerDto
        {
            Id = ledger.Id,
            CustomerId = cust.Id,
            CustomerName = cust.Name,
            CustomerPhone = cust.Phone,
            Date = ledger.Date,
            Description = ledger.Description,
            DebitAmount = ledger.DebitAmount,
            CreditAmount = ledger.CreditAmount,
            RunningBalance = ledger.RunningBalance,
            ReferenceInvoiceNumber = ledger.ReferenceInvoiceNumber,
            Notes = ledger.Notes,
            RecordedBy = ledger.RecordedBy
        };
    }

    private static EmployeeDto MapToDto(Employee e)
    {
        return new EmployeeDto
        {
            Id = e.Id,
            Name = e.Name,
            Email = e.Email,
            Phone = e.Phone,
            Role = e.Role,
            JoiningDate = e.JoiningDate,
            BaseSalary = e.BaseSalary,
            Shift = e.Shift,
            IsActive = e.IsActive
        };
    }
}

public class DashboardService : IDashboardService
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly ITableService _tableService;

    public DashboardService(IUnitOfWork unitOfWork, ITableService tableService)
    {
        _unitOfWork = unitOfWork;
        _tableService = tableService;
    }

    public async Task<DashboardSummaryDto> GetDashboardSummaryAsync()
    {
        var today = DateTime.UtcNow.Date;
        var todayLocal = DateTime.Today;
        var yesterday = today.AddDays(-1);

        var orders = await _unitOfWork.Orders.GetAllAsync();
        var allValidOrders = orders.Where(o => !o.IsDeleted).ToList();

        // Business Day Orders (Only orders created during today)
        var todayAllOrders = allValidOrders.Where(o => o.CreatedAt.Date == today || o.CreatedAt.Date == todayLocal).ToList();
        var todayCompletedOrders = todayAllOrders.Where(o => o.Status == OrderStatus.Completed).ToList();
        var yesterdayCompletedOrders = allValidOrders.Where(o => (o.CreatedAt.Date == yesterday || o.CreatedAt.Date == todayLocal.AddDays(-1)) && o.Status == OrderStatus.Completed).ToList();

        decimal todaySales = todayCompletedOrders.Sum(o => o.GrandTotal);
        decimal yesterdaySales = yesterdayCompletedOrders.Sum(o => o.GrandTotal);

        // Operational Live State: Tables & Pending KOTs must reflect current real-time state, regardless of start date
        var tables = (await _unitOfWork.Tables.GetAllAsync()).Where(t => !t.IsDeleted).ToList();
        int totalTables = tables.Count;
        int occupiedTables = tables.Count(t => t.Status == TableStatus.Occupied || t.Status == TableStatus.Billing);
        int availableTables = tables.Count(t => t.Status == TableStatus.Available);
        int reservedTables = tables.Count(t => t.Status == TableStatus.Reserved);

        var kots = await _unitOfWork.KOTs.GetAllAsync();
        int pendingKots = kots.Count(k => !k.IsDeleted && (k.Status == KOTStatus.New || k.Status == KOTStatus.Preparing));

        // Accurate daily status counts (excluding cancelled from main counts)
        int todayOrdersCount = todayAllOrders.Count(o => o.Status != OrderStatus.Cancelled);
        int completedCount = todayCompletedOrders.Count;
        int cancelledCount = todayAllOrders.Count(o => o.Status == OrderStatus.Cancelled);
        int newOrdersCount = todayAllOrders.Count(o => o.Status == OrderStatus.Pending || o.Status == OrderStatus.Confirmed);
        int preparingOrdersCount = todayAllOrders.Count(o => o.Status == OrderStatus.Preparing);
        int readyOrdersCount = todayAllOrders.Count(o => o.Status == OrderStatus.Ready);
        int servedOrdersCount = todayCompletedOrders.Count;

        var inventory = await _unitOfWork.InventoryItems.GetAllAsync();
        var lowStock = inventory.Where(i => !i.IsDeleted && i.CurrentStock <= i.MinimumStock)
            .Select(i => new LowStockDto
            {
                ItemName = i.Name,
                CurrentStock = i.CurrentStock,
                MinimumStock = i.MinimumStock,
                Unit = i.Unit,
                AlertLevel = i.CurrentStock <= (i.MinimumStock / 2m) ? "Critical" : "Low Stock"
            }).ToList();

        var todayOrderIds = todayAllOrders.Select(o => o.Id).ToHashSet();
        var allOrderItems = await _unitOfWork.OrderItems.GetAllAsync();
        var orderItems = allOrderItems.Where(i => !i.IsDeleted && todayOrderIds.Contains(i.OrderId)).ToList();
        var topSelling = orderItems
            .GroupBy(i => i.ItemName)
            .Select(g => new TopSellingItemDto
            {
                MenuItemName = g.Key,
                CategoryName = "Main Menu",
                TotalQuantitySold = g.Sum(i => i.Quantity),
                TotalRevenue = g.Sum(i => i.TotalPrice)
            })
            .OrderByDescending(t => t.TotalQuantitySold)
            .Take(5)
            .ToList();

        var floorTables = (await _tableService.GetFloorsWithTablesAsync()).ToList();

        var sevenDaysAgo = today.AddDays(-6);
        var weeklyOrders = allValidOrders.Where(o => o.CreatedAt.Date >= sevenDaysAgo && o.CreatedAt.Date <= today).ToList();
        var weeklyTrend = new List<DailySalesSummaryDto>();
        for (int i = 6; i >= 0; i--)
        {
            var d = today.AddDays(-i);
            var dayOrders = weeklyOrders.Where(o => o.CreatedAt.Date == d && o.Status == OrderStatus.Completed).ToList();
            weeklyTrend.Add(new DailySalesSummaryDto
            {
                Date = d,
                SalesAmount = dayOrders.Sum(o => o.GrandTotal),
                OrdersCount = dayOrders.Count
            });
        }

        int dineInCount = todayAllOrders.Count(o => o.OrderType == OrderType.DineIn && o.Status != OrderStatus.Cancelled);
        int takeawayCount = todayAllOrders.Count(o => o.OrderType == OrderType.Takeaway && o.Status != OrderStatus.Cancelled);
        int deliveryCount = todayAllOrders.Count(o => o.OrderType == OrderType.Delivery && o.Status != OrderStatus.Cancelled);

        var allEmployees = (await _unitOfWork.Employees.GetAllAsync()).Where(e => !e.IsDeleted).ToList();
        var todayAttendances = (await _unitOfWork.Attendances.FindAsync(a => a.Date == today && !a.IsDeleted && a.Status == "Present")).ToList();

        var roleAttendanceList = new List<RoleStaffCountDto>();
        var rolesToReport = new[] { UserRole.Manager, UserRole.Cook, UserRole.Waiter, UserRole.Housekeeping, UserRole.Cashier, UserRole.KitchenStaff, UserRole.Admin };

        foreach (var role in rolesToReport)
        {
            var roleEmployees = allEmployees.Where(e => e.Role == role).ToList();
            if (roleEmployees.Any())
            {
                int totalRole = roleEmployees.Count;
                int presentRole = roleEmployees.Count(e => todayAttendances.Any(a => a.EmployeeId == e.Id) || e.IsActive);
                string displayRoleName = role switch
                {
                    UserRole.Cook => "Chefs / Cooks",
                    UserRole.KitchenStaff => "Kitchen Staff",
                    UserRole.Housekeeping => "Housekeeping",
                    UserRole.Waiter => "Waiters",
                    UserRole.Manager => "Managers",
                    UserRole.Cashier => "Cashiers",
                    UserRole.Admin => "Admins",
                    _ => role.ToString()
                };

                roleAttendanceList.Add(new RoleStaffCountDto
                {
                    RoleName = displayRoleName,
                    TotalCount = totalRole,
                    PresentCount = presentRole
                });
            }
        }

        int totalStaff = allEmployees.Count;
        int presentStaff = allEmployees.Count(e => todayAttendances.Any(a => a.EmployeeId == e.Id) || e.IsActive);

        var tableDict = tables.ToDictionary(t => t.Id, t => t.TableNumber.Replace("Table", "").Trim());
        var orderItemQtyDict = orderItems.Where(i => !i.IsDeleted)
            .GroupBy(i => i.OrderId)
            .ToDictionary(g => g.Key, g => g.Sum(i => i.Quantity));

        // Recent Orders: Today's orders, sorted by latest order time first
        var recentOrders = todayAllOrders
            .OrderByDescending(o => o.CreatedAt)
            .Take(10)
            .Select(o => new RecentOrderSummaryDto
            {
                Id = o.Id,
                OrderNumber = o.OrderNumber,
                OrderType = o.OrderType == OrderType.DineIn ? "Dine-in" : o.OrderType.ToString(),
                Status = o.Status.ToString(),
                TableId = o.TableId,
                TableNumber = o.TableId.HasValue && tableDict.TryGetValue(o.TableId.Value, out var num) ? num : string.Empty,
                GrandTotal = o.GrandTotal,
                TotalItems = orderItemQtyDict.TryGetValue(o.Id, out var qty) ? qty : 0,
                CreatedAt = o.CreatedAt
            })
            .ToList();

        return new DashboardSummaryDto
        {
            TodaySales = todaySales,
            YesterdaySales = yesterdaySales,
            TodayOrdersCount = todayOrdersCount,
            TotalTablesCount = totalTables,
            OccupiedTablesCount = occupiedTables,
            AvailableTablesCount = availableTables,
            ReservedTablesCount = reservedTables,
            PendingKOTCount = pendingKots,
            CompletedOrdersCount = completedCount,
            CancelledOrdersCount = cancelledCount,
            NewOrdersCount = newOrdersCount,
            PreparingOrdersCount = preparingOrdersCount,
            ReadyOrdersCount = readyOrdersCount,
            ServedOrdersCount = servedOrdersCount,
            LowStockAlerts = lowStock,
            TopSellingItems = topSelling,
            FloorTables = floorTables,
            WeeklySalesTrend = weeklyTrend,
            DineInOrdersCount = dineInCount,
            TakeawayOrdersCount = takeawayCount,
            DeliveryOrdersCount = deliveryCount,
            StaffRoleAttendance = roleAttendanceList,
            TotalStaffCount = totalStaff,
            PresentStaffCount = presentStaff,
            RecentOrders = recentOrders
        };
    }
}

public class SupplierService : ISupplierService
{
    private readonly IUnitOfWork _unitOfWork;

    public SupplierService(IUnitOfWork unitOfWork)
    {
        _unitOfWork = unitOfWork;
    }

    public async Task<IEnumerable<SupplierDto>> GetSuppliersAsync()
    {
        var suppliers = await _unitOfWork.Suppliers.GetAllAsync();
        var pos = await _unitOfWork.PurchaseOrders.GetAllAsync();
        var poCountDict = pos.Where(p => !p.IsDeleted).GroupBy(p => p.SupplierId).ToDictionary(g => g.Key, g => g.Count());

        return suppliers.Where(s => !s.IsDeleted).Select(s => new SupplierDto
        {
            Id = s.Id,
            Name = s.Name,
            ContactPerson = s.ContactPerson,
            Phone = s.Phone,
            Email = s.Email,
            Address = s.Address,
            GSTNumber = s.GSTNumber,
            OpeningBalance = s.OpeningBalance,
            IsActive = s.IsActive,
            PurchaseOrdersCount = poCountDict.TryGetValue(s.Id, out int c) ? c : 0
        }).OrderBy(s => s.Name);
    }

    public async Task<SupplierDto?> GetSupplierByIdAsync(int id)
    {
        var s = await _unitOfWork.Suppliers.GetByIdAsync(id);
        if (s == null || s.IsDeleted) return null;
        var pos = await _unitOfWork.PurchaseOrders.FindAsync(p => p.SupplierId == id && !p.IsDeleted);

        return new SupplierDto
        {
            Id = s.Id,
            Name = s.Name,
            ContactPerson = s.ContactPerson,
            Phone = s.Phone,
            Email = s.Email,
            Address = s.Address,
            GSTNumber = s.GSTNumber,
            OpeningBalance = s.OpeningBalance,
            IsActive = s.IsActive,
            PurchaseOrdersCount = pos.Count()
        };
    }

    public async Task<SupplierDto> CreateSupplierAsync(SupplierCreateUpdateDto dto)
    {
        if (string.IsNullOrWhiteSpace(dto.Name)) throw new ArgumentException("Supplier name is required.");

        var existing = (await _unitOfWork.Suppliers.FindAsync(s => !s.IsDeleted && s.Name.ToLower() == dto.Name.Trim().ToLower())).FirstOrDefault();
        if (existing != null) throw new InvalidOperationException($"Supplier with name '{dto.Name}' already exists.");

        var supplier = new Supplier
        {
            Name = dto.Name.Trim(),
            ContactPerson = dto.ContactPerson?.Trim() ?? string.Empty,
            Phone = dto.Phone?.Trim() ?? string.Empty,
            Email = dto.Email?.Trim() ?? string.Empty,
            Address = dto.Address?.Trim() ?? string.Empty,
            GSTNumber = dto.GSTNumber?.Trim() ?? string.Empty,
            OpeningBalance = dto.OpeningBalance,
            IsActive = dto.IsActive,
            CreatedAt = DateTime.UtcNow
        };

        await _unitOfWork.Suppliers.AddAsync(supplier);
        await _unitOfWork.SaveChangesAsync();

        return new SupplierDto
        {
            Id = supplier.Id,
            Name = supplier.Name,
            ContactPerson = supplier.ContactPerson,
            Phone = supplier.Phone,
            Email = supplier.Email,
            Address = supplier.Address,
            GSTNumber = supplier.GSTNumber,
            OpeningBalance = supplier.OpeningBalance,
            IsActive = supplier.IsActive,
            PurchaseOrdersCount = 0
        };
    }

    public async Task<SupplierDto> UpdateSupplierAsync(int id, SupplierCreateUpdateDto dto)
    {
        var supplier = await _unitOfWork.Suppliers.GetByIdAsync(id);
        if (supplier == null || supplier.IsDeleted) throw new KeyNotFoundException($"Supplier {id} not found.");

        var duplicate = (await _unitOfWork.Suppliers.FindAsync(s => !s.IsDeleted && s.Id != id && s.Name.ToLower() == dto.Name.Trim().ToLower())).FirstOrDefault();
        if (duplicate != null) throw new InvalidOperationException($"Supplier with name '{dto.Name}' already exists.");

        supplier.Name = dto.Name.Trim();
        supplier.ContactPerson = dto.ContactPerson?.Trim() ?? string.Empty;
        supplier.Phone = dto.Phone?.Trim() ?? string.Empty;
        supplier.Email = dto.Email?.Trim() ?? string.Empty;
        supplier.Address = dto.Address?.Trim() ?? string.Empty;
        supplier.GSTNumber = dto.GSTNumber?.Trim() ?? string.Empty;
        supplier.OpeningBalance = dto.OpeningBalance;
        supplier.IsActive = dto.IsActive;
        supplier.UpdatedAt = DateTime.UtcNow;

        await _unitOfWork.Suppliers.UpdateAsync(supplier);
        await _unitOfWork.SaveChangesAsync();

        return new SupplierDto
        {
            Id = supplier.Id,
            Name = supplier.Name,
            ContactPerson = supplier.ContactPerson,
            Phone = supplier.Phone,
            Email = supplier.Email,
            Address = supplier.Address,
            GSTNumber = supplier.GSTNumber,
            OpeningBalance = supplier.OpeningBalance,
            IsActive = supplier.IsActive
        };
    }

    public async Task<bool> DeleteSupplierAsync(int id)
    {
        var supplier = await _unitOfWork.Suppliers.GetByIdAsync(id);
        if (supplier == null || supplier.IsDeleted) return false;

        var po = await _unitOfWork.PurchaseOrders.FindAsync(p => p.SupplierId == id && !p.IsDeleted);
        if (po.Any()) throw new InvalidOperationException($"Cannot delete supplier '{supplier.Name}' because active purchase orders depend on it.");

        supplier.IsDeleted = true;
        supplier.UpdatedAt = DateTime.UtcNow;
        await _unitOfWork.Suppliers.UpdateAsync(supplier);
        await _unitOfWork.SaveChangesAsync();
        return true;
    }
}

public class ReportService : IReportService
{
    private readonly IUnitOfWork _unitOfWork;

    public ReportService(IUnitOfWork unitOfWork)
    {
        _unitOfWork = unitOfWork;
    }

    public async Task<SalesReportDto> GetSalesReportAsync(DateTime startDate, DateTime endDate)
    {
        var orders = await _unitOfWork.Orders.GetAllAsync();
        var filtered = orders.Where(o => !o.IsDeleted && o.Status == OrderStatus.Completed && o.CreatedAt >= startDate && o.CreatedAt <= endDate).ToList();

        decimal totalRevenue = filtered.Sum(o => o.GrandTotal);
        decimal dineInSales = filtered.Where(o => o.OrderType == OrderType.DineIn).Sum(o => o.GrandTotal);
        decimal takeawaySales = filtered.Where(o => o.OrderType == OrderType.Takeaway).Sum(o => o.GrandTotal);
        decimal deliverySales = filtered.Where(o => o.OrderType == OrderType.Delivery).Sum(o => o.GrandTotal);

        var dailySummaries = filtered.GroupBy(o => o.CreatedAt.Date)
            .Select(g => new DailySalesSummaryDto
            {
                Date = g.Key,
                SalesAmount = g.Sum(o => o.GrandTotal),
                OrdersCount = g.Count()
            })
            .OrderBy(d => d.Date)
            .ToList();

        return new SalesReportDto
        {
            StartDate = startDate,
            EndDate = endDate,
            TotalRevenue = totalRevenue,
            TotalOrders = filtered.Count,
            DineInSales = dineInSales,
            TakeawaySales = takeawaySales,
            DeliverySales = deliverySales,
            TotalDiscount = filtered.Sum(o => o.DiscountAmount),
            TotalCGST = filtered.Sum(o => o.CGST),
            TotalSGST = filtered.Sum(o => o.SGST),
            DailySummaries = dailySummaries
        };
    }

    public async Task<ComprehensiveReportDto> GetComprehensiveReportAsync(string periodType, int? year = null, int? month = null, DateTime? startDate = null, DateTime? endDate = null)
    {
        int reportYear = year ?? DateTime.UtcNow.Year;
        int reportMonth = month ?? DateTime.UtcNow.Month;
        string type = string.IsNullOrWhiteSpace(periodType) ? "DayWise" : periodType;

        DateTime rangeStart;
        DateTime rangeEnd;

        if (type.Equals("DayWise", StringComparison.OrdinalIgnoreCase))
        {
            rangeStart = new DateTime(reportYear, reportMonth, 1);
            rangeEnd = new DateTime(reportYear, reportMonth, DateTime.DaysInMonth(reportYear, reportMonth), 23, 59, 59);
        }
        else if (type.Equals("MonthWise", StringComparison.OrdinalIgnoreCase))
        {
            rangeStart = new DateTime(reportYear, 1, 1);
            rangeEnd = new DateTime(reportYear, 12, 31, 23, 59, 59);
        }
        else if (type.Equals("YearWise", StringComparison.OrdinalIgnoreCase))
        {
            rangeStart = new DateTime(reportYear - 4, 1, 1);
            rangeEnd = new DateTime(reportYear, 12, 31, 23, 59, 59);
        }
        else
        {
            rangeStart = startDate?.Date ?? DateTime.UtcNow.AddDays(-30).Date;
            rangeEnd = (endDate?.Date ?? DateTime.UtcNow.Date).AddDays(1).AddSeconds(-1);
        }

        var allOrders = (await _unitOfWork.Orders.GetAllAsync()).Where(o => !o.IsDeleted).ToList();
        var completedOrders = allOrders
            .Where(o => (o.Status == OrderStatus.Completed || o.IsSettled) && o.CreatedAt >= rangeStart && o.CreatedAt <= rangeEnd)
            .ToList();

        decimal totalRev = completedOrders.Sum(o => o.GrandTotal);
        decimal dineInSales = completedOrders.Where(o => o.OrderType == OrderType.DineIn).Sum(o => o.GrandTotal);
        decimal takeawaySales = completedOrders.Where(o => o.OrderType == OrderType.Takeaway).Sum(o => o.GrandTotal);
        decimal deliverySales = completedOrders.Where(o => o.OrderType == OrderType.Delivery).Sum(o => o.GrandTotal);
        decimal cgst = completedOrders.Sum(o => o.CGST);
        decimal sgst = completedOrders.Sum(o => o.SGST);
        decimal tax = completedOrders.Sum(o => o.TotalTax);
        decimal discount = completedOrders.Sum(o => o.DiscountAmount);

        var breakdownList = new List<ReportPeriodSummaryDto>();

        if (type.Equals("DayWise", StringComparison.OrdinalIgnoreCase))
        {
            int days = DateTime.DaysInMonth(reportYear, reportMonth);
            for (int d = 1; d <= days; d++)
            {
                var curDate = new DateTime(reportYear, reportMonth, d);
                var dayOrders = completedOrders.Where(o => o.CreatedAt.Date == curDate.Date).ToList();

                breakdownList.Add(new ReportPeriodSummaryDto
                {
                    PeriodLabel = curDate.ToString("dd MMM yyyy (ddd)"),
                    Date = curDate,
                    OrdersCount = dayOrders.Count,
                    GrossSales = dayOrders.Sum(o => o.SubTotal),
                    TaxCollected = dayOrders.Sum(o => o.TotalTax),
                    DiscountGiven = dayOrders.Sum(o => o.DiscountAmount),
                    NetRevenue = dayOrders.Sum(o => o.GrandTotal),
                    DineInCount = dayOrders.Count(o => o.OrderType == OrderType.DineIn),
                    TakeawayCount = dayOrders.Count(o => o.OrderType == OrderType.Takeaway)
                });
            }
        }
        else if (type.Equals("MonthWise", StringComparison.OrdinalIgnoreCase))
        {
            for (int m = 1; m <= 12; m++)
            {
                var monthStart = new DateTime(reportYear, m, 1);
                var monthOrders = completedOrders.Where(o => o.CreatedAt.Year == reportYear && o.CreatedAt.Month == m).ToList();

                breakdownList.Add(new ReportPeriodSummaryDto
                {
                    PeriodLabel = monthStart.ToString("MMMM yyyy"),
                    Date = monthStart,
                    OrdersCount = monthOrders.Count,
                    GrossSales = monthOrders.Sum(o => o.SubTotal),
                    TaxCollected = monthOrders.Sum(o => o.TotalTax),
                    DiscountGiven = monthOrders.Sum(o => o.DiscountAmount),
                    NetRevenue = monthOrders.Sum(o => o.GrandTotal),
                    DineInCount = monthOrders.Count(o => o.OrderType == OrderType.DineIn),
                    TakeawayCount = monthOrders.Count(o => o.OrderType == OrderType.Takeaway)
                });
            }
        }
        else if (type.Equals("YearWise", StringComparison.OrdinalIgnoreCase))
        {
            for (int y = reportYear - 4; y <= reportYear; y++)
            {
                var yrStart = new DateTime(y, 1, 1);
                var yrOrders = completedOrders.Where(o => o.CreatedAt.Year == y).ToList();

                breakdownList.Add(new ReportPeriodSummaryDto
                {
                    PeriodLabel = y.ToString(),
                    Date = yrStart,
                    OrdersCount = yrOrders.Count,
                    GrossSales = yrOrders.Sum(o => o.SubTotal),
                    TaxCollected = yrOrders.Sum(o => o.TotalTax),
                    DiscountGiven = yrOrders.Sum(o => o.DiscountAmount),
                    NetRevenue = yrOrders.Sum(o => o.GrandTotal),
                    DineInCount = yrOrders.Count(o => o.OrderType == OrderType.DineIn),
                    TakeawayCount = yrOrders.Count(o => o.OrderType == OrderType.Takeaway)
                });
            }
        }
        else
        {
            // Custom Range grouped by date
            var groups = completedOrders.GroupBy(o => o.CreatedAt.Date).OrderBy(g => g.Key);
            foreach (var g in groups)
            {
                breakdownList.Add(new ReportPeriodSummaryDto
                {
                    PeriodLabel = g.Key.ToString("dd MMM yyyy (ddd)"),
                    Date = g.Key,
                    OrdersCount = g.Count(),
                    GrossSales = g.Sum(o => o.SubTotal),
                    TaxCollected = g.Sum(o => o.TotalTax),
                    DiscountGiven = g.Sum(o => o.DiscountAmount),
                    NetRevenue = g.Sum(o => o.GrandTotal),
                    DineInCount = g.Count(o => o.OrderType == OrderType.DineIn),
                    TakeawayCount = g.Count(o => o.OrderType == OrderType.Takeaway)
                });
            }
        }

        // Payment modes breakdown
        var allPayments = (await _unitOfWork.Payments.GetAllAsync()).Where(p => !p.IsDeleted).ToList();
        var matchedPayments = allPayments.Where(p => p.PaymentDate >= rangeStart && p.PaymentDate <= rangeEnd).ToList();
        var paymentList = new List<PaymentMethodSummaryDto>();

        if (matchedPayments.Any())
        {
            var pGroups = matchedPayments.GroupBy(p => p.PaymentMethod.ToString());
            decimal pTotal = matchedPayments.Sum(p => p.Amount);
            foreach (var pg in pGroups)
            {
                decimal amt = pg.Sum(p => p.Amount);
                paymentList.Add(new PaymentMethodSummaryDto
                {
                    Method = pg.Key,
                    TotalAmount = amt,
                    TransactionCount = pg.Count(),
                    Percentage = pTotal > 0 ? Math.Round((amt / pTotal) * 100m, 1) : 0m
                });
            }
        }
        else
        {
            // Estimate based on orders
            paymentList.Add(new PaymentMethodSummaryDto { Method = "Cash", TotalAmount = Math.Round(totalRev * 0.45m, 2), TransactionCount = Math.Max(1, (int)(completedOrders.Count * 0.45)), Percentage = 45m });
            paymentList.Add(new PaymentMethodSummaryDto { Method = "UPI / QR", TotalAmount = Math.Round(totalRev * 0.40m, 2), TransactionCount = Math.Max(1, (int)(completedOrders.Count * 0.40)), Percentage = 40m });
            paymentList.Add(new PaymentMethodSummaryDto { Method = "Card", TotalAmount = Math.Round(totalRev * 0.15m, 2), TransactionCount = Math.Max(1, (int)(completedOrders.Count * 0.15)), Percentage = 15m });
        }

        // Category breakdown
        var orderIds = completedOrders.Select(o => o.Id).ToHashSet();
        var orderItems = (await _unitOfWork.OrderItems.GetAllAsync()).Where(i => !i.IsDeleted && orderIds.Contains(i.OrderId)).ToList();
        var menuItems = (await _unitOfWork.MenuItems.GetAllAsync()).ToDictionary(m => m.Id);
        var categories = (await _unitOfWork.Categories.GetAllAsync()).ToDictionary(c => c.Id);

        var catList = new List<CategorySalesSummaryDto>();
        if (orderItems.Any())
        {
            var catGroups = orderItems.GroupBy(i =>
            {
                if (menuItems.TryGetValue(i.MenuItemId, out var mi) && categories.TryGetValue(mi.CategoryId, out var cat))
                {
                    return cat.Name;
                }
                return "Main Course";
            });

            decimal itemsTotalRev = orderItems.Sum(i => i.TotalPrice);
            foreach (var cg in catGroups)
            {
                decimal cRev = cg.Sum(i => i.TotalPrice);
                catList.Add(new CategorySalesSummaryDto
                {
                    CategoryName = cg.Key,
                    TotalSales = cRev,
                    ItemsSold = cg.Sum(i => i.Quantity),
                    Percentage = itemsTotalRev > 0 ? Math.Round((cRev / itemsTotalRev) * 100m, 1) : 0m
                });
            }
        }

        // Table collection breakdown
        var tables = (await _unitOfWork.Tables.GetAllAsync()).ToDictionary(t => t.Id);
        var floors = (await _unitOfWork.Floors.GetAllAsync()).ToDictionary(f => f.Id);
        var tableList = new List<TableCollectionSummaryDto>();

        var tableGroups = completedOrders
            .Where(o => o.TableId.HasValue)
            .GroupBy(o => o.TableId!.Value);

        foreach (var tg in tableGroups)
        {
            int tableId = tg.Key;
            string tableNumber = tables.TryGetValue(tableId, out var tbl) ? tbl.TableNumber : $"Table {tableId}";
            string floorName = tbl != null && floors.TryGetValue(tbl.FloorId, out var flr) ? flr.Name : "Main Floor";
            decimal total = tg.Sum(o => o.GrandTotal);
            int count = tg.Count();
            tableList.Add(new TableCollectionSummaryDto
            {
                TableId = tableId,
                TableNumber = tableNumber,
                FloorName = floorName,
                OrdersCount = count,
                TotalAmount = total,
                Percentage = totalRev > 0 ? Math.Round((total / totalRev) * 100m, 1) : 0m
            });
        }

        var takeawayOrders = completedOrders.Where(o => o.OrderType == OrderType.Takeaway).ToList();
        if (takeawayOrders.Any())
        {
            decimal tTotal = takeawayOrders.Sum(o => o.GrandTotal);
            tableList.Add(new TableCollectionSummaryDto
            {
                TableId = 0,
                TableNumber = "Takeaway / Parcel",
                FloorName = "Counter",
                OrdersCount = takeawayOrders.Count,
                TotalAmount = tTotal,
                Percentage = totalRev > 0 ? Math.Round((tTotal / totalRev) * 100m, 1) : 0m
            });
        }
        tableList = tableList.OrderByDescending(t => t.TotalAmount).ToList();

        // Menu item-wise collection breakdown
        var itemList = new List<MenuItemSalesSummaryDto>();
        if (orderItems.Any())
        {
            var itemGroups = orderItems.GroupBy(i => i.MenuItemId);
            decimal itemsTotalRev = orderItems.Sum(i => i.TotalPrice);

            foreach (var ig in itemGroups)
            {
                int menuItemId = ig.Key;
                menuItems.TryGetValue(menuItemId, out var mi);
                string itemName = mi?.Name ?? ig.FirstOrDefault()?.ItemName ?? "Unknown Item";
                string itemNameMr = mi?.NameMarathi ?? string.Empty;
                string itemCode = mi?.ItemCode ?? string.Empty;
                string shortCode = mi?.ShortCode ?? string.Empty;
                string categoryName = mi != null && categories.TryGetValue(mi.CategoryId, out var cat) ? cat.Name : "General";
                decimal unitPrice = mi?.Price ?? (ig.FirstOrDefault()?.UnitPrice ?? 0m);
                int qtySold = ig.Sum(i => i.Quantity);
                decimal revenue = ig.Sum(i => i.TotalPrice);

                itemList.Add(new MenuItemSalesSummaryDto
                {
                    MenuItemId = menuItemId,
                    ItemName = itemName,
                    ItemNameMarathi = itemNameMr,
                    ItemCode = itemCode,
                    ShortCode = shortCode,
                    CategoryName = categoryName,
                    UnitPrice = unitPrice,
                    QuantitySold = qtySold,
                    TotalRevenue = revenue,
                    Percentage = itemsTotalRev > 0 ? Math.Round((revenue / itemsTotalRev) * 100m, 1) : 0m
                });
            }
            itemList = itemList.OrderByDescending(i => i.TotalRevenue).ToList();
        }

        return new ComprehensiveReportDto
        {
            PeriodType = type,
            StartDate = rangeStart,
            EndDate = rangeEnd,
            SelectedYear = reportYear,
            SelectedMonth = reportMonth,
            TotalRevenue = totalRev,
            TotalOrders = completedOrders.Count,
            DineInSales = dineInSales,
            TakeawaySales = takeawaySales,
            DeliverySales = deliverySales,
            TotalTax = tax,
            TotalCGST = cgst,
            TotalSGST = sgst,
            TotalDiscount = discount,
            BreakdownList = breakdownList,
            PaymentBreakdown = paymentList.OrderByDescending(p => p.TotalAmount).ToList(),
            CategoryBreakdown = catList.OrderByDescending(c => c.TotalSales).ToList(),
            TableBreakdown = tableList,
            MenuItemBreakdown = itemList
        };
    }
}

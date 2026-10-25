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
        return items.Where(i => !i.IsDeleted).Select(MapToDto);
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
            ReorderLevel = i.ReorderLevel
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
        var yesterday = today.AddDays(-1);

        var orders = await _unitOfWork.Orders.GetAllAsync();
        var activeOrders = orders.Where(o => !o.IsDeleted).ToList();

        var todayOrders = activeOrders.Where(o => o.CreatedAt.Date == today && o.Status == OrderStatus.Completed).ToList();
        var yesterdayOrders = activeOrders.Where(o => o.CreatedAt.Date == yesterday && o.Status == OrderStatus.Completed).ToList();

        decimal todaySales = todayOrders.Sum(o => o.GrandTotal);
        decimal yesterdaySales = yesterdayOrders.Sum(o => o.GrandTotal);

        var tables = (await _unitOfWork.Tables.GetAllAsync()).Where(t => !t.IsDeleted).ToList();
        int totalTables = tables.Count;
        int occupiedTables = tables.Count(t => t.Status == TableStatus.Occupied || t.Status == TableStatus.Billing);
        int availableTables = tables.Count(t => t.Status == TableStatus.Available);
        int reservedTables = tables.Count(t => t.Status == TableStatus.Reserved);

        var kots = await _unitOfWork.KOTs.GetAllAsync();
        int pendingKots = kots.Count(k => !k.IsDeleted && (k.Status == KOTStatus.New || k.Status == KOTStatus.Preparing));

        int completedCount = activeOrders.Count(o => o.Status == OrderStatus.Completed);
        int cancelledCount = activeOrders.Count(o => o.Status == OrderStatus.Cancelled);
        int newOrdersCount = activeOrders.Count(o => o.Status == OrderStatus.Pending || o.Status == OrderStatus.Confirmed);
        int preparingOrdersCount = activeOrders.Count(o => o.Status == OrderStatus.Preparing);
        int readyOrdersCount = activeOrders.Count(o => o.Status == OrderStatus.Ready);
        int servedOrdersCount = activeOrders.Count(o => o.Status == OrderStatus.Completed);

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

        var orderItems = await _unitOfWork.OrderItems.GetAllAsync();
        var topSelling = orderItems.Where(i => !i.IsDeleted)
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
        var weeklyOrders = activeOrders.Where(o => o.CreatedAt.Date >= sevenDaysAgo && o.CreatedAt.Date <= today).ToList();
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

        int dineInCount = activeOrders.Count(o => o.OrderType == OrderType.DineIn && o.CreatedAt.Date == today);
        int takeawayCount = activeOrders.Count(o => o.OrderType == OrderType.Takeaway && o.CreatedAt.Date == today);
        int deliveryCount = activeOrders.Count(o => o.OrderType == OrderType.Delivery && o.CreatedAt.Date == today);
        if (dineInCount == 0 && takeawayCount == 0 && deliveryCount == 0 && activeOrders.Any())
        {
            dineInCount = activeOrders.Count(o => o.OrderType == OrderType.DineIn);
            takeawayCount = activeOrders.Count(o => o.OrderType == OrderType.Takeaway);
            deliveryCount = activeOrders.Count(o => o.OrderType == OrderType.Delivery);
        }

        int totalOrdersCalculated = dineInCount + takeawayCount + deliveryCount;

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

        var recentOrders = activeOrders
            .OrderByDescending(o => o.CreatedAt)
            .Take(6)
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
            TodayOrdersCount = totalOrdersCalculated > 0 ? totalOrdersCalculated : (activeOrders.Count(o => o.CreatedAt.Date == today)),
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
}

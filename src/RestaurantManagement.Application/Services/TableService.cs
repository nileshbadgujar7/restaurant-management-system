using RestaurantManagement.Application.DTOs;
using RestaurantManagement.Application.Interfaces;
using RestaurantManagement.Domain.Entities;
using RestaurantManagement.Domain.Enums;

namespace RestaurantManagement.Application.Services;

public class TableService : ITableService
{
    private readonly IUnitOfWork _unitOfWork;

    public TableService(IUnitOfWork unitOfWork)
    {
        _unitOfWork = unitOfWork;
    }

    public async Task<IEnumerable<FloorDto>> GetFloorsWithTablesAsync()
    {
        var floors = await _unitOfWork.Floors.GetAllAsync();
        var tables = await _unitOfWork.Tables.GetAllAsync();
        var orders = await _unitOfWork.Orders.FindAsync(o => o.Status != OrderStatus.Completed && o.Status != OrderStatus.Cancelled);
        var users = await _unitOfWork.Users.GetAllAsync();

        var tableList = tables.Where(t => !t.IsDeleted).ToList();
        var orderDict = orders.ToDictionary(o => o.Id);
        var userDict = users.ToDictionary(u => u.Id);

        return floors.Where(f => !f.IsDeleted).OrderBy(f => f.DisplayOrder).Select(f => new FloorDto
        {
            Id = f.Id,
            Name = f.Name,
            Description = f.Description,
            DisplayOrder = f.DisplayOrder,
            Tables = tableList.Where(t => t.FloorId == f.Id).Select(t => MapToTableDto(t, f.Name, orderDict, userDict)).ToList()
        });
    }

    public async Task<IEnumerable<TableDto>> GetAllTablesAsync()
    {
        var tables = await _unitOfWork.Tables.GetAllAsync();
        var floors = (await _unitOfWork.Floors.GetAllAsync()).ToDictionary(f => f.Id);
        var orders = (await _unitOfWork.Orders.FindAsync(o => o.Status != OrderStatus.Completed && o.Status != OrderStatus.Cancelled)).ToDictionary(o => o.Id);
        var users = (await _unitOfWork.Users.GetAllAsync()).ToDictionary(u => u.Id);

        return tables.Where(t => !t.IsDeleted).Select(t => MapToTableDto(t, floors.TryGetValue(t.FloorId, out var f) ? f.Name : "", orderDict: orders, userDict: users));
    }

    public async Task<TableDto?> GetTableByIdAsync(int id)
    {
        var table = await _unitOfWork.Tables.GetByIdAsync(id);
        if (table == null || table.IsDeleted) return null;

        var floor = await _unitOfWork.Floors.GetByIdAsync(table.FloorId);
        return MapToTableDto(table, floor?.Name ?? "");
    }

    public async Task<TableDto> CreateTableAsync(TableCreateUpdateDto dto)
    {
        var existing = (await _unitOfWork.Tables.FindAsync(t => !t.IsDeleted && t.TableNumber.Trim().ToLower() == dto.TableNumber.Trim().ToLower())).FirstOrDefault();
        if (existing != null) throw new InvalidOperationException($"Table number '{dto.TableNumber}' already exists.");

        var table = new Table
        {
            TableNumber = dto.TableNumber.Trim(),
            Capacity = dto.Capacity,
            TableType = string.IsNullOrWhiteSpace(dto.TableType) ? "Dine-In" : dto.TableType,
            Description = dto.Description ?? string.Empty,
            IsActive = dto.IsActive,
            FloorId = dto.FloorId,
            Status = TableStatus.Available
        };

        await _unitOfWork.Tables.AddAsync(table);
        await _unitOfWork.SaveChangesAsync();

        var floor = await _unitOfWork.Floors.GetByIdAsync(dto.FloorId);
        return MapToTableDto(table, floor?.Name ?? "");
    }

    public async Task<TableDto> UpdateTableAsync(int id, TableCreateUpdateDto dto)
    {
        var table = await _unitOfWork.Tables.GetByIdAsync(id);
        if (table == null) throw new KeyNotFoundException($"Table {id} not found");

        var duplicate = (await _unitOfWork.Tables.FindAsync(t => !t.IsDeleted && t.Id != id && t.TableNumber.Trim().ToLower() == dto.TableNumber.Trim().ToLower())).FirstOrDefault();
        if (duplicate != null) throw new InvalidOperationException($"Table number '{dto.TableNumber}' already exists.");

        table.TableNumber = dto.TableNumber.Trim();
        table.Capacity = dto.Capacity;
        table.TableType = string.IsNullOrWhiteSpace(dto.TableType) ? "Dine-In" : dto.TableType;
        table.Description = dto.Description ?? string.Empty;
        table.IsActive = dto.IsActive;
        table.FloorId = dto.FloorId;
        table.UpdatedAt = DateTime.UtcNow;

        await _unitOfWork.Tables.UpdateAsync(table);
        await _unitOfWork.SaveChangesAsync();

        var floor = await _unitOfWork.Floors.GetByIdAsync(dto.FloorId);
        return MapToTableDto(table, floor?.Name ?? "");
    }

    public async Task<bool> DeleteTableAsync(int id)
    {
        var table = await _unitOfWork.Tables.GetByIdAsync(id);
        if (table == null) return false;

        table.IsDeleted = true;
        await _unitOfWork.Tables.UpdateAsync(table);
        await _unitOfWork.SaveChangesAsync();
        return true;
    }

    public async Task<bool> UpdateTableStatusAsync(int id, TableStatus status)
    {
        var table = await _unitOfWork.Tables.GetByIdAsync(id);
        if (table == null) return false;

        table.Status = status;
        if (status == TableStatus.Available)
        {
            table.CurrentOrderId = null;
        }
        table.UpdatedAt = DateTime.UtcNow;

        await _unitOfWork.Tables.UpdateAsync(table);
        await _unitOfWork.SaveChangesAsync();
        return true;
    }

    public async Task<bool> TransferTableAsync(int sourceTableId, int targetTableId)
    {
        var sourceTable = await _unitOfWork.Tables.GetByIdAsync(sourceTableId);
        var targetTable = await _unitOfWork.Tables.GetByIdAsync(targetTableId);

        if (sourceTable == null || targetTable == null) return false;
        if (targetTable.Status != TableStatus.Available) return false; // Rule: cannot transfer to occupied table

        var orderId = sourceTable.CurrentOrderId;
        if (orderId.HasValue)
        {
            var order = await _unitOfWork.Orders.GetByIdAsync(orderId.Value);
            if (order != null)
            {
                order.TableId = targetTableId;
                await _unitOfWork.Orders.UpdateAsync(order);
            }
        }

        targetTable.Status = TableStatus.Occupied;
        targetTable.CurrentOrderId = orderId;
        targetTable.WaiterId = sourceTable.WaiterId;

        sourceTable.Status = TableStatus.Available;
        sourceTable.CurrentOrderId = null;

        await _unitOfWork.Tables.UpdateAsync(sourceTable);
        await _unitOfWork.Tables.UpdateAsync(targetTable);
        await _unitOfWork.SaveChangesAsync();
        return true;
    }

    public async Task<bool> MergeTablesAsync(int sourceTableId, int targetTableId)
    {
        var sourceTable = await _unitOfWork.Tables.GetByIdAsync(sourceTableId);
        var targetTable = await _unitOfWork.Tables.GetByIdAsync(targetTableId);

        if (sourceTable == null || targetTable == null) return false;

        if (sourceTable.CurrentOrderId.HasValue && targetTable.CurrentOrderId.HasValue)
        {
            var sourceItems = await _unitOfWork.OrderItems.FindAsync(i => i.OrderId == sourceTable.CurrentOrderId.Value);
            foreach (var item in sourceItems)
            {
                item.OrderId = targetTable.CurrentOrderId.Value;
                await _unitOfWork.OrderItems.UpdateAsync(item);
            }

            var sourceOrder = await _unitOfWork.Orders.GetByIdAsync(sourceTable.CurrentOrderId.Value);
            if (sourceOrder != null)
            {
                sourceOrder.Status = OrderStatus.Cancelled;
                sourceOrder.SpecialInstructions += " (Merged into Table " + targetTable.TableNumber + ")";
                await _unitOfWork.Orders.UpdateAsync(sourceOrder);
            }

            sourceTable.Status = TableStatus.Available;
            sourceTable.CurrentOrderId = null;

            await _unitOfWork.Tables.UpdateAsync(sourceTable);
            await _unitOfWork.SaveChangesAsync();
            return true;
        }

        return false;
    }

    public async Task<FloorDto> CreateFloorAsync(FloorCreateUpdateDto dto)
    {
        var floor = new Floor
        {
            Name = dto.Name.Trim(),
            Description = dto.Description ?? string.Empty,
            DisplayOrder = dto.DisplayOrder,
            IsActive = dto.IsActive
        };

        await _unitOfWork.Floors.AddAsync(floor);
        await _unitOfWork.SaveChangesAsync();

        return new FloorDto
        {
            Id = floor.Id,
            Name = floor.Name,
            Description = floor.Description,
            DisplayOrder = floor.DisplayOrder,
            IsActive = floor.IsActive
        };
    }

    public async Task<FloorDto?> GetFloorByIdAsync(int id)
    {
        var floor = await _unitOfWork.Floors.GetByIdAsync(id);
        if (floor == null || floor.IsDeleted) return null;

        var tables = await _unitOfWork.Tables.FindAsync(t => t.FloorId == id && !t.IsDeleted);

        return new FloorDto
        {
            Id = floor.Id,
            Name = floor.Name,
            Description = floor.Description,
            DisplayOrder = floor.DisplayOrder,
            IsActive = floor.IsActive,
            Tables = tables.Select(t => MapToTableDto(t, floor.Name)).ToList()
        };
    }

    public async Task<FloorDto> UpdateFloorAsync(int id, FloorCreateUpdateDto dto)
    {
        var floor = await _unitOfWork.Floors.GetByIdAsync(id);
        if (floor == null || floor.IsDeleted) throw new KeyNotFoundException($"Floor with id {id} not found.");

        floor.Name = dto.Name.Trim();
        floor.Description = dto.Description ?? string.Empty;
        floor.DisplayOrder = dto.DisplayOrder;
        floor.IsActive = dto.IsActive;
        floor.UpdatedAt = DateTime.UtcNow;

        await _unitOfWork.Floors.UpdateAsync(floor);
        await _unitOfWork.SaveChangesAsync();

        return new FloorDto
        {
            Id = floor.Id,
            Name = floor.Name,
            Description = floor.Description,
            DisplayOrder = floor.DisplayOrder,
            IsActive = floor.IsActive
        };
    }

    public async Task<bool> DeleteFloorAsync(int id)
    {
        var floor = await _unitOfWork.Floors.GetByIdAsync(id);
        if (floor == null || floor.IsDeleted) return false;

        // Constraint check: Cannot delete floor if active tables are associated with it
        var associatedTables = await _unitOfWork.Tables.FindAsync(t => t.FloorId == id && !t.IsDeleted);
        if (associatedTables.Any())
        {
            throw new InvalidOperationException($"Cannot delete Floor '{floor.Name}' because it has {associatedTables.Count()} associated table(s). Please move or delete the tables first.");
        }

        floor.IsDeleted = true;
        floor.UpdatedAt = DateTime.UtcNow;
        await _unitOfWork.Floors.UpdateAsync(floor);
        await _unitOfWork.SaveChangesAsync();
        return true;
    }

    private static TableDto MapToTableDto(Table t, string floorName, Dictionary<int, Order>? orderDict = null, Dictionary<int, User>? userDict = null)
    {
        decimal amount = 0m;
        string? orderNo = null;
        DateTime? occupiedAt = null;
        if (t.CurrentOrderId.HasValue && orderDict != null && orderDict.TryGetValue(t.CurrentOrderId.Value, out var order))
        {
            amount = order.GrandTotal;
            orderNo = order.OrderNumber;
            occupiedAt = order.CreatedAt;
        }

        string? waiterName = null;
        if (t.WaiterId.HasValue && userDict != null && userDict.TryGetValue(t.WaiterId.Value, out var waiter))
        {
            waiterName = waiter.FullName;
        }

        return new TableDto
        {
            Id = t.Id,
            TableNumber = t.TableNumber,
            Capacity = t.Capacity,
            TableType = t.TableType ?? "Dine-In",
            Description = t.Description ?? string.Empty,
            IsActive = t.IsActive,
            FloorId = t.FloorId,
            FloorName = floorName,
            Status = t.Status,
            CurrentOrderId = t.CurrentOrderId,
            CurrentOrderNumber = orderNo,
            CurrentOrderAmount = amount,
            WaiterId = t.WaiterId,
            WaiterName = waiterName,
            HasActiveDraft = t.HasActiveDraft,
            ActiveDraftUserName = t.ActiveDraftUserName,
            ActiveDraftMinutesAgo = t.ActiveDraftTime.HasValue ? (int?)(DateTime.UtcNow - t.ActiveDraftTime.Value).TotalMinutes : null,
            OccupiedAt = occupiedAt
        };
    }
}

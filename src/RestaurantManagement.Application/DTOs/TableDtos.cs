using RestaurantManagement.Domain.Enums;

namespace RestaurantManagement.Application.DTOs;

public class FloorDto
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public int DisplayOrder { get; set; }
    public bool IsActive { get; set; } = true;
    public List<TableDto> Tables { get; set; } = new();
}

public class FloorCreateUpdateDto
{
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public int DisplayOrder { get; set; } = 1;
    public bool IsActive { get; set; } = true;
}

public class TableDto
{
    public int Id { get; set; }
    public string TableNumber { get; set; } = string.Empty;
    public int Capacity { get; set; }
    public string TableType { get; set; } = "Dine-In";
    public string Description { get; set; } = string.Empty;
    public bool IsActive { get; set; } = true;
    public int FloorId { get; set; }
    public string FloorName { get; set; } = string.Empty;
    public TableStatus Status { get; set; }
    public string StatusName => Status.ToString();
    public int? CurrentOrderId { get; set; }
    public string? CurrentOrderNumber { get; set; }
    public decimal CurrentOrderAmount { get; set; }
    public int? WaiterId { get; set; }
    public string? WaiterName { get; set; }
    public bool HasActiveDraft { get; set; }
    public string? ActiveDraftUserName { get; set; }
    public int? ActiveDraftMinutesAgo { get; set; }
    public DateTime? OccupiedAt { get; set; }
    public int OccupiedMinutes => OccupiedAt.HasValue ? Math.Max(0, (int)(DateTime.UtcNow - OccupiedAt.Value).TotalMinutes) : 0;
}

public class TableCreateUpdateDto
{
    public string TableNumber { get; set; } = string.Empty;
    public int Capacity { get; set; } = 4;
    public string TableType { get; set; } = "Dine-In";
    public string Description { get; set; } = string.Empty;
    public bool IsActive { get; set; } = true;
    public int FloorId { get; set; }
}

public class TableTransferDto
{
    public int SourceTableId { get; set; }
    public int TargetTableId { get; set; }
}

public class TableMergeDto
{
    public int SourceTableId { get; set; }
    public int TargetTableId { get; set; }
}

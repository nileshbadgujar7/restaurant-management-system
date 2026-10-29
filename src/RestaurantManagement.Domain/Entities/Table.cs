using RestaurantManagement.Domain.Enums;

namespace RestaurantManagement.Domain.Entities;

public class Table : BaseEntity
{
    public string TableNumber { get; set; } = string.Empty;
    public int Capacity { get; set; } = 4;
    public string TableType { get; set; } = "Dine-In";
    public string Description { get; set; } = string.Empty;
    public int FloorId { get; set; }
    public Floor? Floor { get; set; }
    public TableStatus Status { get; set; } = TableStatus.Available;
    public int? CurrentOrderId { get; set; }
    public Order? CurrentOrder { get; set; }
    public int? WaiterId { get; set; }
    public User? Waiter { get; set; }
    
    // Order Management Specifications (OM-A03, OM-A04, OM-E03)
    public bool HasActiveDraft { get; set; } = false;
    public int? ActiveDraftUserId { get; set; }
    public string ActiveDraftUserName { get; set; } = string.Empty;
    public DateTime? ActiveDraftTime { get; set; }
    public int? GroupTableId { get; set; } // For tables pushed together (OM-E03)
}

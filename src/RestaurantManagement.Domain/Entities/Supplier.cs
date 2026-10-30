namespace RestaurantManagement.Domain.Entities;

public class Supplier : BaseEntity
{
    public string Name { get; set; } = string.Empty;
    public string ContactPerson { get; set; } = string.Empty;
    public string Phone { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string Address { get; set; } = string.Empty;
    public string GSTNumber { get; set; } = string.Empty;
    public decimal OpeningBalance { get; set; } = 0m;
    
    public ICollection<PurchaseOrder> PurchaseOrders { get; set; } = new List<PurchaseOrder>();
    public ICollection<InventoryItem> InventoryItems { get; set; } = new List<InventoryItem>();
}

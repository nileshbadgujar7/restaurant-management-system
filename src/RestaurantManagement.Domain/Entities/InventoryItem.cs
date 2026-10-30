namespace RestaurantManagement.Domain.Entities;

public class InventoryItem : BaseEntity
{
    public string Name { get; set; } = string.Empty;
    public string Category { get; set; } = string.Empty;
    public string Unit { get; set; } = "KG"; // KG, L, Pcs, Gm, Ml
    public decimal CurrentStock { get; set; }
    public decimal MinimumStock { get; set; }
    public decimal UnitCost { get; set; }
    public decimal ReorderLevel { get; set; }
    public int? SupplierId { get; set; }
    public Supplier? Supplier { get; set; }
    
    public ICollection<StockTransaction> StockTransactions { get; set; } = new List<StockTransaction>();
}

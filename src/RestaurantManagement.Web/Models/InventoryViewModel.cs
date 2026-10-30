using RestaurantManagement.Application.DTOs;

namespace RestaurantManagement.Web.Models;

public class InventoryViewModel
{
    public string ActiveTab { get; set; } = "stock"; // stock, history
    public List<InventoryItemDto> Items { get; set; } = new();
    public List<StockMovementLogDto> MovementLogs { get; set; } = new();
    public List<SupplierDto> Suppliers { get; set; } = new();
    public decimal TotalInventoryValue { get; set; }
    public int TotalItemsCount { get; set; }
    public int LowStockCount { get; set; }
    public int CriticalStockCount { get; set; }
    public int MovementsTodayCount { get; set; }
}


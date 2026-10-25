using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using RestaurantManagement.Application.DTOs;
using RestaurantManagement.Application.Interfaces;
using RestaurantManagement.Web.Models;

namespace RestaurantManagement.Web.Controllers;

[Authorize(Roles = "Admin,Manager,InventoryStaff")]
public class InventoryController : Controller
{
    private readonly IInventoryService _inventoryService;

    public InventoryController(IInventoryService inventoryService)
    {
        _inventoryService = inventoryService;
    }

    public async Task<IActionResult> Index(string tab = "stock")
    {
        var items = (await _inventoryService.GetInventoryItemsAsync()).ToList();
        var logs = (await _inventoryService.GetStockMovementLogsAsync()).ToList();

        decimal totalValue = items.Sum(i => i.CurrentStock * i.UnitCost);
        int lowCount = items.Count(i => i.IsLowStock);
        int critCount = items.Count(i => i.CurrentStock <= (i.MinimumStock / 2m));
        int todayLogs = logs.Count(l => l.CreatedAt.Date == DateTime.UtcNow.Date);

        var viewModel = new InventoryViewModel
        {
            ActiveTab = tab.ToLower(),
            Items = items,
            MovementLogs = logs,
            TotalInventoryValue = totalValue,
            TotalItemsCount = items.Count,
            LowStockCount = lowCount,
            CriticalStockCount = critCount,
            MovementsTodayCount = todayLogs
        };

        return View(viewModel);
    }

    [HttpPost]
    public async Task<IActionResult> AdjustStock(StockAdjustmentDto dto)
    {
        try
        {
            await _inventoryService.AdjustStockAsync(dto);
            TempData["Success"] = "Stock adjustment recorded successfully.";
        }
        catch (Exception ex)
        {
            TempData["Error"] = ex.Message;
        }
        return RedirectToAction(nameof(Index), new { tab = "stock" });
    }

    [HttpPost]
    public async Task<IActionResult> StockIn(int inventoryItemId, decimal quantity, string? supplier, string? invoiceNo, string? notes)
    {
        try
        {
            if (quantity <= 0) throw new ArgumentException("Quantity must be greater than zero.");
            string logNote = "Stock In / Purchase";
            if (!string.IsNullOrWhiteSpace(supplier)) logNote += $" from {supplier.Trim()}";
            if (!string.IsNullOrWhiteSpace(invoiceNo)) logNote += $" (Inv #{invoiceNo.Trim()})";
            if (!string.IsNullOrWhiteSpace(notes)) logNote += $" - {notes.Trim()}";

            await _inventoryService.AdjustStockAsync(new StockAdjustmentDto
            {
                InventoryItemId = inventoryItemId,
                Quantity = quantity,
                TransactionType = "IN",
                Notes = logNote
            });

            TempData["Success"] = $"Stock In recorded successfully! Added {quantity} units.";
        }
        catch (Exception ex)
        {
            TempData["Error"] = ex.Message;
        }

        return RedirectToAction(nameof(Index), new { tab = "stock" });
    }

    [HttpPost]
    public async Task<IActionResult> StockOut(int inventoryItemId, decimal quantity, string reason, string? notes)
    {
        try
        {
            if (quantity <= 0) throw new ArgumentException("Quantity must be greater than zero.");
            string txnType = reason == "Wastage" || reason == "Spoilage" || reason == "Expired" ? "WASTAGE" : "OUT";
            string logNote = $"Stock Out ({reason})";
            if (!string.IsNullOrWhiteSpace(notes)) logNote += $": {notes.Trim()}";

            await _inventoryService.AdjustStockAsync(new StockAdjustmentDto
            {
                InventoryItemId = inventoryItemId,
                Quantity = quantity,
                TransactionType = txnType,
                Notes = logNote
            });

            TempData["Success"] = $"Stock Out recorded successfully! Deducted {quantity} units.";
        }
        catch (Exception ex)
        {
            TempData["Error"] = ex.Message;
        }

        return RedirectToAction(nameof(Index), new { tab = "stock" });
    }

    [HttpPost]
    public async Task<IActionResult> CreateItem(InventoryItemDto dto)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(dto.Name)) throw new ArgumentException("Item name is required.");
            await _inventoryService.CreateInventoryItemAsync(dto);
            TempData["Success"] = $"Inventory item '{dto.Name}' added successfully!";
        }
        catch (Exception ex)
        {
            TempData["Error"] = ex.Message;
        }

        return RedirectToAction(nameof(Index), new { tab = "stock" });
    }
}

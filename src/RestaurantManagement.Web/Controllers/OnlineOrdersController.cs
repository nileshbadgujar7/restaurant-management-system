using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using RestaurantManagement.Application.DTOs;
using RestaurantManagement.Application.Interfaces;
using RestaurantManagement.Domain.Entities;
using RestaurantManagement.Domain.Enums;

namespace RestaurantManagement.Web.Controllers;

[Authorize(Roles = "Admin,Manager,Waiter,Cashier,KitchenStaff,Cook")]
public class OnlineOrdersController : Controller
{
    private readonly IOrderService _orderService;
    private readonly IMenuService _menuService;
    private readonly IUnitOfWork _unitOfWork;

    public OnlineOrdersController(
        IOrderService orderService,
        IMenuService menuService,
        IUnitOfWork unitOfWork)
    {
        _orderService = orderService;
        _menuService = menuService;
        _unitOfWork = unitOfWork;
    }

    public async Task<IActionResult> Index(string platform = "all", string status = "all")
    {
        var allOrders = await _orderService.GetOrdersAsync();

        // Online orders: OrderType == Online OR has an OnlinePlatform specified OR Delivery with online reference
        var onlineOrders = allOrders
            .Where(o => o.OrderType == OrderType.Online || !string.IsNullOrWhiteSpace(o.OnlinePlatform))
            .OrderByDescending(o => o.CreatedAt)
            .ToList();

        // Calculate Today's KPIs
        var today = DateTime.UtcNow.Date;
        var todayOrders = onlineOrders.Where(o => o.CreatedAt.Date == today).ToList();

        ViewBag.TotalOrdersToday = todayOrders.Count;
        ViewBag.TotalRevenueToday = todayOrders.Where(o => o.Status == OrderStatus.Completed || o.IsSettled).Sum(o => o.GrandTotal);
        ViewBag.NewCount = onlineOrders.Count(o => o.Status == OrderStatus.Pending || o.Status == OrderStatus.Confirmed);
        ViewBag.PreparingCount = onlineOrders.Count(o => o.Status == OrderStatus.Preparing);
        ViewBag.ReadyCount = onlineOrders.Count(o => o.Status == OrderStatus.Ready);
        ViewBag.CompletedCount = onlineOrders.Count(o => o.Status == OrderStatus.Completed);
        ViewBag.CancelledCount = onlineOrders.Count(o => o.Status == OrderStatus.Cancelled);

        // Filter by platform
        if (!string.Equals(platform, "all", StringComparison.OrdinalIgnoreCase))
        {
            onlineOrders = onlineOrders
                .Where(o => o.OnlinePlatform.Equals(platform, StringComparison.OrdinalIgnoreCase))
                .ToList();
        }

        // Filter by status
        if (!string.Equals(status, "all", StringComparison.OrdinalIgnoreCase))
        {
            if (Enum.TryParse<OrderStatus>(status, true, out var orderStatus))
            {
                onlineOrders = onlineOrders.Where(o => o.Status == orderStatus).ToList();
            }
        }

        ViewBag.CurrentPlatform = platform;
        ViewBag.CurrentStatus = status;

        // Categories & Menu Items for the "New Online Order" modal
        ViewBag.Categories = await _menuService.GetCategoriesAsync();
        ViewBag.MenuItems = await _menuService.GetMenuItemsAsync();

        var settings = await _unitOfWork.RestaurantSettings.GetAllAsync();
        ViewBag.Setting = settings.FirstOrDefault() ?? new RestaurantSetting();

        return View(onlineOrders);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(
        string customerName,
        string customerPhone,
        string deliveryAddress,
        string onlinePlatform,
        string? onlineOrderReference,
        string? deliveryPartnerName,
        decimal packagingCharge,
        List<int> itemIds,
        List<int> quantities,
        List<string>? itemNotes)
    {
        try
        {
            if (itemIds == null || !itemIds.Any())
            {
                TempData["Error"] = "Please select at least one menu item for the online order.";
                return RedirectToAction(nameof(Index));
            }

            var allMenuItems = await _menuService.GetMenuItemsAsync();
            var menuDict = allMenuItems.ToDictionary(m => m.Id);

            var items = new List<CreateOrderItemDto>();
            for (int i = 0; i < itemIds.Count; i++)
            {
                int mId = itemIds[i];
                int qty = (quantities != null && i < quantities.Count) ? quantities[i] : 1;
                if (qty <= 0) continue;

                if (menuDict.TryGetValue(mId, out var mi))
                {
                    string note = (itemNotes != null && i < itemNotes.Count) ? (itemNotes[i] ?? "") : "";
                    items.Add(new CreateOrderItemDto
                    {
                        MenuItemId = mi.Id,
                        MenuItemName = mi.Name,
                        UnitPrice = mi.Price,
                        Quantity = qty,
                        SpecialInstructions = note
                    });
                }
            }

            if (!items.Any())
            {
                TempData["Error"] = "Order must have at least one valid item.";
                return RedirectToAction(nameof(Index));
            }

            var dto = new CreateOrderDto
            {
                OrderType = OrderType.Online,
                CustomerName = string.IsNullOrWhiteSpace(customerName) ? $"{onlinePlatform} Customer" : customerName.Trim(),
                CustomerPhone = customerPhone?.Trim() ?? string.Empty,
                DeliveryAddress = deliveryAddress?.Trim() ?? string.Empty,
                OnlinePlatform = string.IsNullOrWhiteSpace(onlinePlatform) ? "Direct Online" : onlinePlatform.Trim(),
                OnlineOrderReference = onlineOrderReference?.Trim() ?? $"ONL-{DateTime.UtcNow:MMddHHmm}",
                DeliveryPartnerName = deliveryPartnerName?.Trim() ?? string.Empty,
                PackagingCharge = packagingCharge > 0 ? packagingCharge : 0m,
                Items = items
            };

            var createdOrder = await _orderService.CreateOrderAsync(dto);
            TempData["Success"] = $"Online Order #{createdOrder.OrderNumber} ({dto.OnlinePlatform} - {dto.OnlineOrderReference}) placed successfully!";
            return RedirectToAction(nameof(Index));
        }
        catch (Exception ex)
        {
            TempData["Error"] = $"Failed to create online order: {ex.Message}";
            return RedirectToAction(nameof(Index));
        }
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> UpdateStatus(int id, OrderStatus status, string? deliveryPartner = null)
    {
        try
        {
            var order = await _unitOfWork.Orders.GetByIdAsync(id);
            if (order == null)
            {
                TempData["Error"] = "Order not found.";
                return RedirectToAction(nameof(Index));
            }

            if (!string.IsNullOrWhiteSpace(deliveryPartner))
            {
                order.DeliveryPartnerName = deliveryPartner.Trim();
                await _unitOfWork.Orders.UpdateAsync(order);
                await _unitOfWork.SaveChangesAsync();
            }

            await _orderService.UpdateOrderStatusAsync(id, status);

            TempData["Success"] = $"Order #{order.OrderNumber} updated to {status}!";
            return RedirectToAction(nameof(Index));
        }
        catch (Exception ex)
        {
            TempData["Error"] = $"Failed to update status: {ex.Message}";
            return RedirectToAction(nameof(Index));
        }
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> AssignRider(int id, string riderName, string? riderPhone = null)
    {
        try
        {
            var order = await _unitOfWork.Orders.GetByIdAsync(id);
            if (order == null)
            {
                TempData["Error"] = "Order not found.";
                return RedirectToAction(nameof(Index));
            }

            string info = string.IsNullOrWhiteSpace(riderPhone) ? riderName.Trim() : $"{riderName.Trim()} ({riderPhone.Trim()})";
            order.DeliveryPartnerName = info;

            await _unitOfWork.Orders.UpdateAsync(order);
            await _unitOfWork.SaveChangesAsync();

            TempData["Success"] = $"Delivery partner assigned to Order #{order.OrderNumber}!";
            return RedirectToAction(nameof(Index));
        }
        catch (Exception ex)
        {
            TempData["Error"] = $"Failed to assign rider: {ex.Message}";
            return RedirectToAction(nameof(Index));
        }
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Cancel(int id, string? reason = null)
    {
        try
        {
            var success = await _orderService.CancelOrderAsync(id, reason ?? "Cancelled by online platform / customer");
            if (success)
            {
                TempData["Success"] = "Online order cancelled successfully.";
            }
            else
            {
                TempData["Error"] = "Could not cancel order.";
            }
            return RedirectToAction(nameof(Index));
        }
        catch (Exception ex)
        {
            TempData["Error"] = $"Error cancelling order: {ex.Message}";
            return RedirectToAction(nameof(Index));
        }
    }
}

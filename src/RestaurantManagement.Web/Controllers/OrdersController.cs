using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using RestaurantManagement.Application.DTOs;
using RestaurantManagement.Application.Interfaces;
using RestaurantManagement.Domain.Entities;
using RestaurantManagement.Domain.Enums;

namespace RestaurantManagement.Web.Controllers;

[Authorize(Roles = "Admin,Manager,Waiter,Cashier,KitchenStaff,Cook")]
public class OrdersController : Controller
{
    private readonly IOrderService _orderService;
    private readonly IMenuService _menuService;
    private readonly ITableService _tableService;
    private readonly IStaffService _staffService;
    private readonly IUnitOfWork _unitOfWork;

    public OrdersController(
        IOrderService orderService, 
        IMenuService menuService, 
        ITableService tableService,
        IStaffService staffService,
        IUnitOfWork unitOfWork)
    {
        _orderService = orderService;
        _menuService = menuService;
        _tableService = tableService;
        _staffService = staffService;
        _unitOfWork = unitOfWork;
    }

    public async Task<IActionResult> Index(OrderStatus? status, OrderType? type)
    {
        var orders = await _orderService.GetOrdersAsync(status, type);
        var settings = await _unitOfWork.RestaurantSettings.GetAllAsync();
        ViewBag.Setting = settings.FirstOrDefault() ?? new RestaurantSetting();
        return View(orders);
    }

    public async Task<IActionResult> POS(int? tableId, int? orderId = null, string? returnUrl = null)
    {
        ViewBag.Categories = await _menuService.GetCategoriesAsync();
        ViewBag.MenuItems = await _menuService.GetMenuItemsAsync();
        ViewBag.Tables = await _tableService.GetAllTablesAsync();
        ViewBag.SelectedTableId = tableId;
        ViewBag.ReturnUrl = string.IsNullOrWhiteSpace(returnUrl) ? "/Dashboard" : returnUrl;

        var employees = await _staffService.GetEmployeesAsync();
        ViewBag.Waiters = employees.Where(e => e.Role == UserRole.Waiter || e.Role == UserRole.Manager || e.Role == UserRole.Cashier).ToList();

        var settings = await _unitOfWork.RestaurantSettings.GetAllAsync();
        var setting = settings.FirstOrDefault() ?? new RestaurantSetting();
        ViewBag.Setting = setting;

        TableDto? selectedTable = null;
        if (tableId.HasValue)
        {
            selectedTable = await _tableService.GetTableByIdAsync(tableId.Value);
        }
        ViewBag.SelectedTable = selectedTable;

        OrderDto? activeOrder = null;
        if (orderId.HasValue)
        {
            activeOrder = await _orderService.GetOrderByIdAsync(orderId.Value);
            if (activeOrder != null && (activeOrder.IsSettled || activeOrder.Status == OrderStatus.Completed || activeOrder.Status == OrderStatus.Cancelled))
            {
                activeOrder = null;
            }
        }
        else if (tableId.HasValue)
        {
            if (selectedTable?.CurrentOrderId.HasValue == true)
            {
                activeOrder = await _orderService.GetOrderByIdAsync(selectedTable.CurrentOrderId.Value);
                if (activeOrder != null && (activeOrder.IsSettled || activeOrder.Status == OrderStatus.Completed || activeOrder.Status == OrderStatus.Cancelled))
                {
                    activeOrder = null;
                }
            }
            if (activeOrder == null)
            {
                var allOrders = await _orderService.GetOrdersAsync();
                activeOrder = allOrders.FirstOrDefault(o => o.TableId == tableId.Value && !o.IsSettled && o.Status != OrderStatus.Completed && o.Status != OrderStatus.Cancelled);
            }
        }
        ViewBag.ActiveOrder = activeOrder;

        OrderDto? activeDraft = null;
        if (activeOrder == null && tableId.HasValue)
        {
            activeDraft = await _orderService.GetDraftAsync(tableId);
        }
        ViewBag.ActiveDraft = activeDraft;

        return View();
    }

    [HttpGet]
    public async Task<IActionResult> GetOrderDetails(int id)
    {
        var order = await _orderService.GetOrderByIdAsync(id);
        if (order == null) return NotFound(new { success = false, message = "Order not found" });
        return Json(new { success = true, order });
    }

    [HttpPost]
    public async Task<IActionResult> CreatePOSOrder([FromBody] CreateOrderDto dto)
    {
        try
        {
            if (dto.OrderType == OrderType.DineIn && (!dto.TableId.HasValue || dto.TableId.Value <= 0))
            {
                return Json(new { success = false, message = "Table must be selected for Dine-in orders." });
            }
            if (dto.Items == null || !dto.Items.Any())
            {
                return Json(new { success = false, message = "At least one item must be added to the order." });
            }
            if (dto.Items.Any(i => i.Quantity <= 0))
            {
                return Json(new { success = false, message = "All item quantities must be at least 1." });
            }
            if (dto.OrderType == OrderType.Takeaway && string.IsNullOrWhiteSpace(dto.CustomerName))
            {
                return Json(new { success = false, message = "Customer name is required for parcel orders." });
            }

            var order = await _orderService.CreateOrderAsync(dto);
            return Json(new { success = true, orderId = order.Id, orderNumber = order.OrderNumber, order });
        }
        catch (Exception ex)
        {
            return Json(new { success = false, message = ex.Message });
        }
    }

    [HttpPost]
    public async Task<IActionResult> SaveDraft([FromBody] SaveDraftOrderDto dto)
    {
        try
        {
            var draft = await _orderService.SaveDraftAsync(dto);
            return Json(new { success = true, draftId = draft.Id, draft });
        }
        catch (Exception ex)
        {
            return Json(new { success = false, message = ex.Message });
        }
    }

    [HttpGet]
    public async Task<IActionResult> GetDraft(int? tableId, int? userId)
    {
        var draft = await _orderService.GetDraftAsync(tableId, userId);
        return Json(new { success = draft != null, draft });
    }

    [HttpPost]
    public async Task<IActionResult> DiscardDraft(int? tableId, int? userId, string? managerPin)
    {
        bool discarded = await _orderService.DiscardDraftAsync(tableId, userId, managerPin);
        return Json(new { success = discarded });
    }

    [HttpPost]
    public async Task<IActionResult> SendKOT([FromBody] SendKOTRequestDto dto)
    {
        try
        {
            var itemsToSend = dto.NewItems ?? dto.Items ?? new List<CreateOrderItemDto>();
            int orderId = dto.OrderId;

            // If orderId is not provided or <= 0, check if this table ALREADY has an active open order
            if (orderId <= 0 && dto.TableId.HasValue)
            {
                var table = await _tableService.GetTableByIdAsync(dto.TableId.Value);
                if (table?.CurrentOrderId.HasValue == true)
                {
                    var existing = await _orderService.GetOrderByIdAsync(table.CurrentOrderId.Value);
                    if (existing != null && !existing.IsSettled && existing.Status != OrderStatus.Completed && existing.Status != OrderStatus.Cancelled)
                    {
                        orderId = existing.Id;
                    }
                }
                
                if (orderId <= 0)
                {
                    var allOrders = await _orderService.GetOrdersAsync();
                    var existingOpenOrder = allOrders.FirstOrDefault(o => o.TableId == dto.TableId.Value && !o.IsSettled && o.Status != OrderStatus.Completed && o.Status != OrderStatus.Cancelled);
                    if (existingOpenOrder != null)
                    {
                        orderId = existingOpenOrder.Id;
                    }
                }
            }

            // If orderId was found or passed, check if it's a draft order
            if (orderId > 0)
            {
                var existingOrder = await _orderService.GetOrderByIdAsync(orderId);
                if (existingOrder != null && existingOrder.IsDraft)
                {
                    // For a draft order, items in the draft have already been saved to the database via SaveDraft.
                    // Passing itemsToSend again to SendKOTAsync causes them to be added a second time!
                    itemsToSend = null;
                }
            }

            // If still no order exists for this table, check for draft or create initial order
            if (orderId <= 0 && dto.TableId.HasValue)
            {
                var draft = await _orderService.GetDraftAsync(dto.TableId.Value);
                if (draft != null)
                {
                    orderId = draft.Id;
                    itemsToSend = null; // Already added as draft items
                }
                else
                {
                    var newDraft = await _orderService.SaveDraftAsync(new SaveDraftOrderDto
                    {
                        TableId = dto.TableId.Value,
                        Covers = dto.Covers > 0 ? dto.Covers : 1,
                        OrderNote = dto.OrderNote ?? dto.Notes,
                        UserName = dto.WaiterName ?? "Staff",
                        Items = itemsToSend ?? new List<CreateOrderItemDto>()
                    });
                    orderId = newDraft.Id;
                    itemsToSend = null; // Already added as draft items
                }
            }

            var kot = await _orderService.SendKOTAsync(orderId, itemsToSend, dto.Notes, dto.IsRush);
            var updatedOrder = await _orderService.GetOrderByIdAsync(orderId);
            return Json(new { success = true, kotNumber = kot.KOTNumber, kot, order = updatedOrder, orderId = orderId });
        }
        catch (Exception ex)
        {
            return Json(new { success = false, message = ex.Message });
        }
    }

    [HttpPost]
    public async Task<IActionResult> ReduceItem([FromBody] ReduceItemDto dto)
    {
        try
        {
            var order = await _orderService.ReduceItemQuantityAsync(dto);
            return Json(new { success = true, order });
        }
        catch (Exception ex)
        {
            return Json(new { success = false, message = ex.Message });
        }
    }

    [HttpPost]
    public async Task<IActionResult> ReplaceItem([FromBody] ReplaceItemDto dto)
    {
        try
        {
            var order = await _orderService.ReplaceItemAsync(dto);
            return Json(new { success = true, order });
        }
        catch (Exception ex)
        {
            return Json(new { success = false, message = ex.Message });
        }
    }

    [HttpPost]
    public async Task<IActionResult> ChangeItemNote([FromBody] ChangeItemNoteDto dto)
    {
        try
        {
            var order = await _orderService.ChangeItemNoteAsync(dto);
            return Json(new { success = true, order });
        }
        catch (Exception ex)
        {
            return Json(new { success = false, message = ex.Message });
        }
    }

    [HttpPost]
    public async Task<IActionResult> UpdateItemStatus([FromBody] MarkItemStatusDto dto)
    {
        try
        {
            var order = await _orderService.UpdateItemStatusAsync(dto.OrderId, dto.OrderItemId, dto.Status);
            return Json(new { success = true, order });
        }
        catch (Exception ex)
        {
            return Json(new { success = false, message = ex.Message });
        }
    }

    [HttpPost]
    public async Task<IActionResult> MarkAllServed(int orderId)
    {
        try
        {
            var order = await _orderService.MarkAllServedAsync(orderId);
            return Json(new { success = true, order });
        }
        catch (Exception ex)
        {
            return Json(new { success = false, message = ex.Message });
        }
    }

    [HttpPost]
    public async Task<IActionResult> MakeItemComplimentary([FromBody] ComplimentaryItemDto dto)
    {
        try
        {
            var order = await _orderService.MakeItemComplimentaryAsync(dto);
            return Json(new { success = true, order });
        }
        catch (Exception ex)
        {
            return Json(new { success = false, message = ex.Message });
        }
    }

    [HttpPost]
    public async Task<IActionResult> ReturnItem([FromBody] ReturnItemDto dto)
    {
        try
        {
            var order = await _orderService.ReturnItemAsync(dto);
            return Json(new { success = true, order });
        }
        catch (Exception ex)
        {
            return Json(new { success = false, message = ex.Message });
        }
    }

    [HttpPost]
    public async Task<IActionResult> TransferOrMergeTable([FromBody] TableMoveOrMergeDto dto)
    {
        try
        {
            bool success = dto.IsMerge 
                ? await _orderService.MergeTablesAsync(dto.SourceTableId, dto.TargetTableId)
                : await _orderService.TransferTableAsync(dto.SourceTableId, dto.TargetTableId);

            return Json(new { success });
        }
        catch (Exception ex)
        {
            return Json(new { success = false, message = ex.Message });
        }
    }

    [HttpPost]
    public async Task<IActionResult> CancelOrder(int orderId, string reason = "", string? managerPin = null)
    {
        try
        {
            bool success = await _orderService.CancelOrderAsync(orderId, reason, managerPin);
            return Json(new { success });
        }
        catch (Exception ex)
        {
            return Json(new { success = false, message = ex.Message });
        }
    }

    [HttpPost]
    public async Task<IActionResult> ReopenOrder(int orderId, string reason, string? managerPin = null)
    {
        try
        {
            var order = await _orderService.ReopenOrderAsync(orderId, reason, managerPin);
            return Json(new { success = true, order });
        }
        catch (Exception ex)
        {
            return Json(new { success = false, message = ex.Message });
        }
    }

    [HttpPost]
    public async Task<IActionResult> GenerateBillFromPOS([FromBody] BillFromPOSRequestDto dto)
    {
        try
        {
            var bill = await _orderService.GenerateBillFromPOSAsync(dto.OrderId, dto.DiscountAmount, dto.DiscountReason, dto.CustomerGstin);
            return Json(new { success = true, bill });
        }
        catch (InvalidOperationException ex) when (ex.Message.Contains("Some items have not been marked Served"))
        {
            // Auto-advance items to Served for POS billing so cashier can finalize settlement
            var items = await _unitOfWork.OrderItems.FindAsync(i => i.OrderId == dto.OrderId && !i.IsDeleted && i.Status != OrderItemStatus.Served && i.Status != OrderItemStatus.Cancelled);
            foreach (var item in items)
            {
                item.Status = OrderItemStatus.Served;
                item.ServedAt = DateTime.UtcNow;
                await _unitOfWork.OrderItems.UpdateAsync(item);
            }
            await _unitOfWork.SaveChangesAsync();

            var bill = await _orderService.GenerateBillFromPOSAsync(dto.OrderId, dto.DiscountAmount, dto.DiscountReason, dto.CustomerGstin);
            return Json(new { success = true, bill });
        }
        catch (Exception ex)
        {
            return Json(new { success = false, message = ex.Message });
        }
    }

    [HttpGet]
    public async Task<IActionResult> GetParcelOrders()
    {
        try
        {
            var todayUtc = DateTime.UtcNow.Date;
            var todayLocal = DateTime.Today;
            var parcelOrders = (await _unitOfWork.Orders.FindAsync(o => 
                !o.IsDeleted && 
                !o.IsDraft && 
                o.OrderType == OrderType.Takeaway && 
                (o.CreatedAt.Date == todayUtc || o.CreatedAt.Date == todayLocal) &&
                o.Status != OrderStatus.Completed && 
                o.Status != OrderStatus.Cancelled))
                .OrderByDescending(o => o.CreatedAt)
                .ToList();

            var orderIds = parcelOrders.Select(o => o.Id).ToList();
            var allItems = (await _unitOfWork.OrderItems.FindAsync(i => !i.IsDeleted && orderIds.Contains(i.OrderId))).ToList();
            var allKots = (await _unitOfWork.KOTs.FindAsync(k => !k.IsDeleted && orderIds.Contains(k.OrderId))).ToList();

            var result = parcelOrders.Select(o =>
            {
                var items = allItems.Where(i => i.OrderId == o.Id).ToList();
                int totalQty = items.Sum(i => i.Quantity);
                string itemsSummary = string.Join(", ", items.Take(3).Select(i => $"{i.Quantity}x {i.ItemName}"));
                if (items.Count > 3) itemsSummary += $" +{items.Count - 3} more";

                var kot = allKots.Where(k => k.OrderId == o.Id).OrderByDescending(k => k.CreatedAt).FirstOrDefault();

                return new
                {
                    id = o.Id,
                    orderNumber = o.OrderNumber,
                    kotNumber = kot?.KOTNumber ?? "-",
                    customerName = string.IsNullOrWhiteSpace(o.CustomerName) ? "Walk-in Guest" : o.CustomerName,
                    customerPhone = string.IsNullOrWhiteSpace(o.CustomerPhone) ? "-" : o.CustomerPhone,
                    orderNote = o.OrderNote,
                    totalItems = totalQty > 0 ? totalQty : items.Count,
                    itemsSummary = string.IsNullOrWhiteSpace(itemsSummary) ? "Items ordered" : itemsSummary,
                    grandTotal = o.GrandTotal,
                    status = o.Status.ToString(),
                    createdAt = o.CreatedAt
                };
            }).ToList();

            return Json(new { success = true, orders = result });
        }
        catch (Exception ex)
        {
            return Json(new { success = false, message = ex.Message });
        }
    }

    [HttpPost]
    public async Task<IActionResult> CreateParcelOrder([FromBody] CreateParcelOrderDto dto)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(dto.CustomerName))
            {
                return Json(new { success = false, message = "Customer name is required for Parcel/Takeaway order." });
            }
            if (string.IsNullOrWhiteSpace(dto.CustomerPhone) || !System.Text.RegularExpressions.Regex.IsMatch(dto.CustomerPhone.Trim(), @"^[6-9]\d{9}$"))
            {
                return Json(new { success = false, message = "Valid 10-digit customer mobile number starting with 6-9 is required." });
            }
            if (dto.Items == null || !dto.Items.Any())
            {
                return Json(new { success = false, message = "Please select at least one item for the parcel order." });
            }
            if (dto.Items.Any(i => i.Quantity <= 0))
            {
                return Json(new { success = false, message = "Quantity must be greater than zero for all items." });
            }

            int count = await _unitOfWork.Orders.CountAsync();
            string orderNo = $"ORD-{(count + 1001)}";
            string tokenNo = $"T-{(count % 1000 + 1):D3}";
            var settings = (await _unitOfWork.RestaurantSettings.GetAllAsync()).FirstOrDefault() ?? new RestaurantSetting();

            var order = new Order
            {
                OrderNumber = orderNo,
                OrderType = OrderType.Takeaway,
                Status = OrderStatus.Confirmed,
                CustomerName = dto.CustomerName.Trim(),
                CustomerPhone = dto.CustomerPhone.Trim(),
                OrderNote = dto.OrderNote?.Trim() ?? string.Empty,
                TokenNumber = tokenNo,
                Covers = 1,
                PackagingCharge = settings.PackagingCharge,
                CreatedAt = DateTime.UtcNow,
                Version = 1,
                IsDraft = false
            };

            await _unitOfWork.Orders.AddAsync(order);
            await _unitOfWork.SaveChangesAsync(); // Generates order.Id

            decimal subTotal = 0m;
            var orderItems = new List<OrderItem>();

            foreach (var itemDto in dto.Items)
            {
                var menuItem = await _unitOfWork.MenuItems.GetByIdAsync(itemDto.MenuItemId);
                if (menuItem == null || menuItem.IsDeleted || !menuItem.IsAvailable)
                {
                    throw new InvalidOperationException($"Menu item '{menuItem?.Name ?? itemDto.MenuItemId.ToString()}' is currently unavailable.");
                }

                decimal price = menuItem.Price;
                if (!string.IsNullOrEmpty(itemDto.SelectedVariantName))
                {
                    var variant = (await _unitOfWork.MenuItemVariants.FindAsync(v => v.MenuItemId == menuItem.Id && v.VariantName == itemDto.SelectedVariantName)).FirstOrDefault();
                    if (variant != null) price = variant.Price;
                }

                decimal lineTotal = price * itemDto.Quantity;
                subTotal += lineTotal;

                var orderItem = new OrderItem
                {
                    OrderId = order.Id,
                    MenuItemId = menuItem.Id,
                    ItemName = menuItem.Name,
                    UnitPrice = price,
                    Quantity = itemDto.Quantity,
                    TotalPrice = lineTotal,
                    SpecialInstructions = itemDto.SpecialInstructions?.Trim() ?? string.Empty,
                    SelectedVariantName = itemDto.SelectedVariantName ?? string.Empty,
                    Status = OrderItemStatus.Sent,
                    Course = string.IsNullOrWhiteSpace(itemDto.Course) ? "Main" : itemDto.Course,
                    Station = "Main"
                };
                await _unitOfWork.OrderItems.AddAsync(orderItem);
                orderItems.Add(orderItem);
            }

            // Recalculate totals
            decimal cgstPercent = settings.CGSTPercentage > 0 ? settings.CGSTPercentage : 2.5m;
            decimal sgstPercent = settings.SGSTPercentage > 0 ? settings.SGSTPercentage : 2.5m;
            decimal taxable = Math.Max(0, subTotal);
            decimal cgst = Math.Round(taxable * (cgstPercent / 100m), 2);
            decimal sgst = Math.Round(taxable * (sgstPercent / 100m), 2);
            decimal tax = cgst + sgst;
            decimal packaging = settings.PackagingCharge;
            decimal unroundedTotal = taxable + tax + packaging;
            decimal grandTotal = Math.Round(unroundedTotal, MidpointRounding.AwayFromZero);
            decimal roundOff = grandTotal - unroundedTotal;

            order.SubTotal = subTotal;
            order.CGST = cgst;
            order.SGST = sgst;
            order.TotalTax = tax;
            order.PackagingCharge = packaging;
            order.GrandTotal = grandTotal;
            order.RoundOff = roundOff;
            order.UpdatedAt = DateTime.UtcNow;

            await _unitOfWork.Orders.UpdateAsync(order);
            await _unitOfWork.SaveChangesAsync();

            // Create KOT for Kitchen
            int kotCount = await _unitOfWork.KOTs.CountAsync();
            string kotNo = $"KOT-{(kotCount + 1001)}";
            var kot = new KOT
            {
                OrderId = order.Id,
                KOTNumber = kotNo,
                Status = KOTStatus.New,
                Notes = dto.OrderNote ?? string.Empty,
                CreatedAt = DateTime.UtcNow
            };
            await _unitOfWork.KOTs.AddAsync(kot);
            await _unitOfWork.SaveChangesAsync();

            foreach (var oi in orderItems)
            {
                var kotItem = new KOTItem
                {
                    KOTId = kot.Id,
                    OrderItemId = oi.Id,
                    MenuItemName = oi.ItemName,
                    Quantity = oi.Quantity,
                    SpecialInstructions = oi.SpecialInstructions,
                    Status = KOTStatus.New
                };
                await _unitOfWork.KOTItems.AddAsync(kotItem);
            }
            await _unitOfWork.SaveChangesAsync();

            return Json(new
            {
                success = true,
                orderId = order.Id,
                orderNumber = order.OrderNumber,
                kotNumber = kot.KOTNumber,
                customerName = order.CustomerName,
                grandTotal = order.GrandTotal
            });
        }
        catch (Exception ex)
        {
            return Json(new { success = false, message = ex.Message });
        }
    }

    [HttpPost]
    public async Task<IActionResult> UpdateOrderItem([FromBody] UpdateOrderItemRequest req)
    {
        try
        {
            if (req.OrderItemId <= 0) return Json(new { success = false, message = "Invalid Item ID." });
            var item = await _unitOfWork.OrderItems.GetByIdAsync(req.OrderItemId);
            if (item == null || item.IsDeleted) return Json(new { success = false, message = "Item not found." });

            if (req.Quantity.HasValue && req.Quantity.Value > 0)
            {
                item.Quantity = req.Quantity.Value;
                item.TotalPrice = item.UnitPrice * item.Quantity;

                var kotItems = await _unitOfWork.KOTItems.FindAsync(k => k.OrderItemId == item.Id && !k.IsDeleted);
                foreach (var ki in kotItems)
                {
                    ki.Quantity = item.Quantity;
                    await _unitOfWork.KOTItems.UpdateAsync(ki);
                }
            }

            if (req.SpecialInstructions != null)
            {
                item.SpecialInstructions = req.SpecialInstructions;
                var kotItems = await _unitOfWork.KOTItems.FindAsync(k => k.OrderItemId == item.Id && !k.IsDeleted);
                foreach (var ki in kotItems)
                {
                    ki.SpecialInstructions = item.SpecialInstructions;
                    await _unitOfWork.KOTItems.UpdateAsync(ki);
                }
            }

            await _unitOfWork.OrderItems.UpdateAsync(item);
            await _unitOfWork.SaveChangesAsync();

            var order = await _unitOfWork.Orders.GetByIdAsync(item.OrderId);
            if (order != null)
            {
                var allItems = await _unitOfWork.OrderItems.FindAsync(i => i.OrderId == order.Id && !i.IsDeleted && i.Status != OrderItemStatus.Cancelled);
                order.SubTotal = allItems.Sum(i => i.TotalPrice);
                order.TotalTax = Math.Round(order.SubTotal * 0.05m, 2);
                order.CGST = Math.Round(order.TotalTax / 2m, 2);
                order.SGST = order.TotalTax - order.CGST;
                order.GrandTotal = Math.Round(order.SubTotal + order.TotalTax - order.DiscountAmount + order.PackagingCharge + order.DeliveryCharge, 0);
                order.UpdatedAt = DateTime.UtcNow;
                await _unitOfWork.Orders.UpdateAsync(order);
                await _unitOfWork.SaveChangesAsync();
            }

            return Json(new { success = true });
        }
        catch (Exception ex)
        {
            return Json(new { success = false, message = ex.Message });
        }
    }
}

public class UpdateOrderItemRequest
{
    public int OrderItemId { get; set; }
    public int? Quantity { get; set; }
    public string? SpecialInstructions { get; set; }
}

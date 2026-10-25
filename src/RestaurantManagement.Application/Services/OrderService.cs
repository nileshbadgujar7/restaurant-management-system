using System.Text.RegularExpressions;
using RestaurantManagement.Application.DTOs;
using RestaurantManagement.Application.Interfaces;
using RestaurantManagement.Domain.Entities;
using RestaurantManagement.Domain.Enums;

namespace RestaurantManagement.Application.Services;

public class OrderService : IOrderService
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly IBillingService? _billingService;

    public OrderService(IUnitOfWork unitOfWork, IBillingService? billingService = null)
    {
        _unitOfWork = unitOfWork;
        _billingService = billingService;
    }

    public async Task<OrderDto> CreateOrderAsync(CreateOrderDto dto)
    {
        ValidateOrderDto(dto);

        Table? table = null;
        if (dto.OrderType == OrderType.DineIn && dto.TableId.HasValue)
        {
            table = await _unitOfWork.Tables.GetByIdAsync(dto.TableId.Value);
            if (table == null || table.IsDeleted || table.Status == TableStatus.OutOfService)
            {
                throw new InvalidOperationException("Selected table is unavailable for new orders.");
            }

            // Customer adding items to existing table order
            if (table.CurrentOrderId.HasValue && (table.Status == TableStatus.Occupied || table.Status == TableStatus.Billing))
            {
                var existingOrder = await _unitOfWork.Orders.GetByIdAsync(table.CurrentOrderId.Value);
                if (existingOrder != null && existingOrder.Status != OrderStatus.Completed && existingOrder.Status != OrderStatus.Cancelled)
                {
                    foreach (var itemDto in dto.Items)
                    {
                        await AddItemToOrderInternalAsync(existingOrder, itemDto);
                    }
                    RecalculateOrderTotals(existingOrder);
                    await _unitOfWork.Orders.UpdateAsync(existingOrder);
                    await _unitOfWork.SaveChangesAsync();
                    return (await GetOrderByIdAsync(existingOrder.Id))!;
                }
            }
        }

        int count = await _unitOfWork.Orders.CountAsync();
        string orderNo = $"ORD-{(count + 1001)}";
        string tokenNo = dto.OrderType != OrderType.DineIn ? $"T-{(count % 1000 + 1):D3}" : string.Empty;

        var settings = await GetRestaurantSettingAsync();

        var order = new Order
        {
            OrderNumber = orderNo,
            OrderType = dto.OrderType,
            Status = OrderStatus.Pending,
            TableId = dto.TableId,
            WaiterId = dto.WaiterId,
            CustomerId = dto.CustomerId,
            CustomerName = dto.CustomerName,
            CustomerPhone = dto.CustomerPhone,
            SpecialInstructions = CleanText(dto.SpecialInstructions),
            OrderNote = CleanText(dto.OrderNote),
            TokenNumber = tokenNo,
            Covers = Math.Max(1, dto.Covers),
            DeliveryAddress = CleanText(dto.DeliveryAddress),
            CustomerGSTIN = dto.CustomerGSTIN?.Trim().ToUpperInvariant() ?? string.Empty,
            PackagingCharge = dto.OrderType == OrderType.Takeaway ? settings.PackagingCharge : (dto.OrderType == OrderType.Delivery ? settings.DeliveryCharge : 0m),
            DeliveryCharge = dto.OrderType == OrderType.Delivery ? settings.DeliveryCharge : 0m,
            CreatedAt = DateTime.UtcNow,
            Version = 1
        };

        foreach (var itemDto in dto.Items)
        {
            await AddItemToOrderInternalAsync(order, itemDto);
        }

        RecalculateOrderTotals(order);

        await _unitOfWork.Orders.AddAsync(order);
        await _unitOfWork.SaveChangesAsync();

        if (table != null)
        {
            table.CurrentOrderId = order.Id;
            table.WaiterId = dto.WaiterId;
            table.HasActiveDraft = false;
            table.ActiveDraftUserId = null;
            table.ActiveDraftUserName = string.Empty;
            await _unitOfWork.Tables.UpdateAsync(table);
            await _unitOfWork.SaveChangesAsync();
        }

        return (await GetOrderByIdAsync(order.Id))!;
    }

    public async Task<OrderDto> SaveDraftAsync(SaveDraftOrderDto dto)
    {
        var settings = await GetRestaurantSettingAsync();
        Table? table = null;
        if (dto.TableId.HasValue)
        {
            table = await _unitOfWork.Tables.GetByIdAsync(dto.TableId.Value);
            if (table != null && dto.Covers > table.Capacity)
            {
                throw new ArgumentException($"Covers ({dto.Covers}) cannot exceed table capacity ({table.Capacity}) (OM-A10).");
            }
        }

        // Check for existing draft for this table or user
        Order? draftOrder = null;
        if (dto.TableId.HasValue)
        {
            var drafts = await _unitOfWork.Orders.FindAsync(o => o.TableId == dto.TableId.Value && o.IsDraft && !o.IsDeleted);
            draftOrder = drafts.FirstOrDefault();
        }

        if (draftOrder == null)
        {
            int count = await _unitOfWork.Orders.CountAsync();
            draftOrder = new Order
            {
                OrderNumber = $"DRAFT-{(count + 1001)}",
                OrderType = dto.OrderType,
                Status = OrderStatus.Pending,
                TableId = dto.TableId,
                CustomerName = dto.CustomerName,
                CustomerPhone = dto.CustomerPhone,
                OrderNote = CleanText(dto.OrderNote),
                Covers = Math.Max(1, dto.Covers),
                IsDraft = true,
                DraftUserId = dto.UserId,
                DraftUserName = dto.UserName,
                DraftExpiresAt = DateTime.UtcNow.AddHours(settings.DraftExpiryHours),
                CreatedAt = DateTime.UtcNow,
                Version = 1
            };
            await _unitOfWork.Orders.AddAsync(draftOrder);
            await _unitOfWork.SaveChangesAsync();
        }
        else
        {
            draftOrder.CustomerName = dto.CustomerName;
            draftOrder.CustomerPhone = dto.CustomerPhone;
            draftOrder.OrderNote = CleanText(dto.OrderNote);
            draftOrder.Covers = Math.Max(1, dto.Covers);
            draftOrder.DraftUserId = dto.UserId;
            draftOrder.DraftUserName = dto.UserName;
            draftOrder.DraftExpiresAt = DateTime.UtcNow.AddHours(settings.DraftExpiryHours);
            draftOrder.UpdatedAt = DateTime.UtcNow;
            draftOrder.Version++;

            // Clear existing draft items
            var existingItems = await _unitOfWork.OrderItems.FindAsync(i => i.OrderId == draftOrder.Id && !i.IsDeleted);
            foreach (var item in existingItems)
            {
                item.IsDeleted = true;
                await _unitOfWork.OrderItems.UpdateAsync(item);
            }
            await _unitOfWork.SaveChangesAsync();
        }

        draftOrder.Items.Clear();
        foreach (var itemDto in dto.Items)
        {
            await AddItemToOrderInternalAsync(draftOrder, itemDto);
        }

        await _unitOfWork.SaveChangesAsync();
        await RecalculateOrderTotalsAsync(draftOrder);
        await _unitOfWork.Orders.UpdateAsync(draftOrder);
        await _unitOfWork.SaveChangesAsync();

        if (table != null)
        {
            table.HasActiveDraft = true;
            table.ActiveDraftUserId = dto.UserId;
            table.ActiveDraftUserName = dto.UserName;
            table.ActiveDraftTime = DateTime.UtcNow;
            await _unitOfWork.Tables.UpdateAsync(table);
            await _unitOfWork.SaveChangesAsync();
        }

        return (await GetOrderByIdAsync(draftOrder.Id))!;
    }

    public async Task<OrderDto?> GetDraftAsync(int? tableId, int? userId = null)
    {
        if (!tableId.HasValue && !userId.HasValue) return null;

        var drafts = await _unitOfWork.Orders.FindAsync(o => 
            o.IsDraft && !o.IsDeleted && 
            ((tableId.HasValue && o.TableId == tableId.Value) || (userId.HasValue && o.DraftUserId == userId.Value)));

        var draft = drafts.OrderByDescending(o => o.UpdatedAt ?? o.CreatedAt).FirstOrDefault();
        if (draft == null) return null;

        // Auto-expire check (OM-A03)
        if (draft.DraftExpiresAt.HasValue && draft.DraftExpiresAt.Value < DateTime.UtcNow)
        {
            draft.IsDeleted = true;
            await _unitOfWork.Orders.UpdateAsync(draft);

            if (draft.TableId.HasValue)
            {
                var table = await _unitOfWork.Tables.GetByIdAsync(draft.TableId.Value);
                if (table != null)
                {
                    table.HasActiveDraft = false;
                    table.ActiveDraftUserId = null;
                    table.ActiveDraftUserName = string.Empty;
                    await _unitOfWork.Tables.UpdateAsync(table);
                }
            }
            await _unitOfWork.SaveChangesAsync();
            return null;
        }

        return await GetOrderByIdAsync(draft.Id);
    }

    public async Task<bool> DiscardDraftAsync(int? tableId, int? userId = null, string? managerPin = null)
    {
        var draft = await GetDraftAsync(tableId, userId);
        if (draft == null) return false;

        var order = await _unitOfWork.Orders.GetByIdAsync(draft.Id);
        if (order != null)
        {
            order.IsDeleted = true;
            await _unitOfWork.Orders.UpdateAsync(order);
        }

        if (tableId.HasValue)
        {
            var table = await _unitOfWork.Tables.GetByIdAsync(tableId.Value);
            if (table != null)
            {
                table.HasActiveDraft = false;
                table.ActiveDraftUserId = null;
                table.ActiveDraftUserName = string.Empty;
                await _unitOfWork.Tables.UpdateAsync(table);
            }
        }

        await _unitOfWork.SaveChangesAsync();
        return true;
    }

    public async Task<OrderDto?> GetOrderByIdAsync(int id)
    {
        var order = await _unitOfWork.Orders.GetByIdAsync(id);
        if (order == null || order.IsDeleted) return null;

        var items = await _unitOfWork.OrderItems.FindAsync(i => i.OrderId == id && !i.IsDeleted);
        var table = order.TableId.HasValue ? await _unitOfWork.Tables.GetByIdAsync(order.TableId.Value) : null;
        Floor? floor = table != null ? await _unitOfWork.Floors.GetByIdAsync(table.FloorId) : null;
        var waiter = order.WaiterId.HasValue ? await _unitOfWork.Users.GetByIdAsync(order.WaiterId.Value) : null;
        var kots = await _unitOfWork.KOTs.FindAsync(k => k.OrderId == id && !k.IsDeleted);

        var dto = MapToOrderDto(order, table?.TableNumber, floor?.Name, waiter?.FullName);
        dto.Items = items.OrderBy(i => i.Id).Select(MapToOrderItemDto).ToList();
        dto.KOTs = kots.OrderBy(k => k.CreatedAt).Select(MapToKOTDto).ToList();
        return dto;
    }

    public async Task<OrderDto?> GetOrderByNumberAsync(string orderNumber)
    {
        var orders = await _unitOfWork.Orders.FindAsync(o => o.OrderNumber == orderNumber && !o.IsDeleted);
        var order = orders.FirstOrDefault();
        return order != null ? await GetOrderByIdAsync(order.Id) : null;
    }

    public async Task<IEnumerable<OrderDto>> GetActiveOrdersAsync()
    {
        return await GetOrdersAsync(status: null);
    }

    public async Task<IEnumerable<OrderDto>> GetOrdersAsync(OrderStatus? status = null, OrderType? type = null)
    {
        var orders = await _unitOfWork.Orders.GetAllAsync();
        var tables = (await _unitOfWork.Tables.GetAllAsync()).ToDictionary(t => t.Id);
        var floors = (await _unitOfWork.Floors.GetAllAsync()).ToDictionary(f => f.Id);
        var users = (await _unitOfWork.Users.GetAllAsync()).ToDictionary(u => u.Id);

        var query = orders.Where(o => !o.IsDeleted && !o.IsDraft);

        if (status.HasValue) query = query.Where(o => o.Status == status.Value);
        if (type.HasValue) query = query.Where(o => o.OrderType == type.Value);

        return query.OrderByDescending(o => o.CreatedAt).Select(o => {
            string? tableNo = null;
            string? floorName = null;
            if (o.TableId.HasValue && tables.TryGetValue(o.TableId.Value, out var t))
            {
                tableNo = t.TableNumber;
                if (floors.TryGetValue(t.FloorId, out var f)) floorName = f.Name;
            }
            string? waiterName = o.WaiterId.HasValue && users.TryGetValue(o.WaiterId.Value, out var u) ? u.FullName : null;
            return MapToOrderDto(o, tableNo, floorName, waiterName);
        });
    }

    public async Task<OrderDto> AddItemToOrderAsync(int orderId, CreateOrderItemDto dto)
    {
        var order = await _unitOfWork.Orders.GetByIdAsync(orderId);
        if (order == null || order.Status == OrderStatus.Completed || order.Status == OrderStatus.Cancelled || order.IsSettled)
        {
            throw new InvalidOperationException("Cannot modify completed or settled order (OM-B15).");
        }

        await AddItemToOrderInternalAsync(order, dto);
        await _unitOfWork.SaveChangesAsync();
        await RecalculateOrderTotalsAsync(order);
        order.UpdatedAt = DateTime.UtcNow;
        order.Version++;

        await _unitOfWork.Orders.UpdateAsync(order);
        await _unitOfWork.SaveChangesAsync();

        return (await GetOrderByIdAsync(orderId))!;
    }

    public async Task<OrderDto> RemoveItemFromOrderAsync(int orderId, int orderItemId)
    {
        var order = await _unitOfWork.Orders.GetByIdAsync(orderId);
        if (order == null || order.Status == OrderStatus.Completed || order.Status == OrderStatus.Cancelled || order.IsSettled)
        {
            throw new InvalidOperationException("Cannot modify completed or settled order.");
        }

        var item = await _unitOfWork.OrderItems.GetByIdAsync(orderItemId);
        if (item != null)
        {
            item.IsDeleted = true;
            await _unitOfWork.OrderItems.UpdateAsync(item);
            await _unitOfWork.SaveChangesAsync();

            await RecalculateOrderTotalsAsync(order);
            order.UpdatedAt = DateTime.UtcNow;
            order.Version++;

            await _unitOfWork.Orders.UpdateAsync(order);
            await _unitOfWork.SaveChangesAsync();
        }

        return (await GetOrderByIdAsync(orderId))!;
    }

    public async Task<OrderDto> UpdateOrderStatusAsync(int orderId, OrderStatus status)
    {
        var order = await _unitOfWork.Orders.GetByIdAsync(orderId);
        if (order == null) throw new KeyNotFoundException($"Order {orderId} not found");

        order.Status = status;
        order.UpdatedAt = DateTime.UtcNow;
        order.Version++;
        await _unitOfWork.Orders.UpdateAsync(order);
        await _unitOfWork.SaveChangesAsync();

        return (await GetOrderByIdAsync(orderId))!;
    }

    public async Task<KOTDto> GenerateKOTAsync(int orderId, string notes = "")
    {
        return await SendKOTAsync(orderId, null, notes, false);
    }

    public async Task<KOTDto> SendKOTAsync(int orderId, List<CreateOrderItemDto>? newItems = null, string notes = "", bool isRush = false)
    {
        var order = await _unitOfWork.Orders.GetByIdAsync(orderId);
        if (order == null) throw new KeyNotFoundException($"Order {orderId} not found");

        if (order.IsSettled || order.Status == OrderStatus.Completed || order.Status == OrderStatus.Cancelled)
        {
            throw new InvalidOperationException("Cannot send KOT for settled or cancelled order.");
        }

        // Add any new items provided in Section B
        if (newItems != null && newItems.Any())
        {
            var existingPending = await _unitOfWork.OrderItems.FindAsync(i => i.OrderId == orderId && !i.IsDeleted && i.Status == OrderItemStatus.Pending);
            // If this is a draft and pending items are already saved in the order, don't duplicate
            if (!order.IsDraft || !existingPending.Any())
            {
                foreach (var itemDto in newItems)
                {
                    await AddItemToOrderInternalAsync(order, itemDto);
                }
                await _unitOfWork.SaveChangesAsync();
                await RecalculateOrderTotalsAsync(order);
            }
        }

        // If order was a draft, convert it to active confirmed order!
        if (order.IsDraft)
        {
            int orderCount = await _unitOfWork.Orders.CountAsync();
            order.OrderNumber = $"ORD-{(orderCount + 1001)}";
            order.IsDraft = false;
        }

        var pendingItems = (await _unitOfWork.OrderItems.FindAsync(i => i.OrderId == orderId && !i.IsDeleted && i.Status == OrderItemStatus.Pending)).ToList();
        if (!pendingItems.Any())
        {
            pendingItems = order.Items.Where(i => !i.IsDeleted && i.Status == OrderItemStatus.Pending).ToList();
        }
        if (!pendingItems.Any())
        {
            throw new InvalidOperationException("Send KOT is disabled when Section B (new items) is empty (FR-ORD-11, OM-A16).");
        }

        var table = order.TableId.HasValue ? await _unitOfWork.Tables.GetByIdAsync(order.TableId.Value) : null;
        var waiter = order.WaiterId.HasValue ? await _unitOfWork.Users.GetByIdAsync(order.WaiterId.Value) : null;

        var existingKots = await _unitOfWork.KOTs.FindAsync(k => k.OrderId == orderId && !k.IsDeleted);
        bool isAddition = existingKots.Any();
        var kotType = isAddition ? KOTType.Addition : KOTType.Initial;

        int totalKots = await _unitOfWork.KOTs.CountAsync();
        string kotNo = $"KOT-{(totalKots + 1):D4}";

        var kot = new KOT
        {
            KOTNumber = kotNo,
            OrderId = orderId,
            TableNumber = table?.TableNumber ?? "NA",
            WaiterName = waiter?.FullName ?? "Staff",
            Status = KOTStatus.New,
            Notes = CleanText(notes),
            Type = kotType,
            IsRush = isRush,
            CreatedAt = DateTime.UtcNow
        };

        foreach (var item in pendingItems)
        {
            item.Status = OrderItemStatus.Sent;
            item.SentAt = DateTime.UtcNow;
            item.KOTNumber = kotNo;

            kot.Items.Add(new KOTItem
            {
                MenuItemName = item.ItemName + (!string.IsNullOrEmpty(item.SelectedVariantName) ? $" ({item.SelectedVariantName})" : ""),
                Quantity = item.Quantity,
                SpecialInstructions = item.SpecialInstructions,
                Status = KOTStatus.New,
                OrderItemId = item.Id,
                Course = item.Course,
                Station = item.Station
            });

            await _unitOfWork.OrderItems.UpdateAsync(item);
        }

        await _unitOfWork.KOTs.AddAsync(kot);

        order.Status = OrderStatus.Confirmed;
        order.UpdatedAt = DateTime.UtcNow;
        order.Version++;
        await _unitOfWork.Orders.UpdateAsync(order);

        // Turn table to Occupied (OM-A01, Step 4 of Worked Example)
        if (table != null)
        {
            table.Status = TableStatus.Occupied;
            table.CurrentOrderId = order.Id;
            table.HasActiveDraft = false;
            table.ActiveDraftUserId = null;
            table.ActiveDraftUserName = string.Empty;
            await _unitOfWork.Tables.UpdateAsync(table);
        }

        await _unitOfWork.SaveChangesAsync();

        // Assign KOTId to items now that kot.Id is saved
        foreach (var item in pendingItems)
        {
            item.KOTId = kot.Id;
            await _unitOfWork.OrderItems.UpdateAsync(item);
        }
        await _unitOfWork.SaveChangesAsync();

        return MapToKOTDto(kot);
    }

    public async Task<OrderDto> ReduceItemQuantityAsync(ReduceItemDto dto)
    {
        var order = await _unitOfWork.Orders.GetByIdAsync(dto.OrderId);
        if (order == null) throw new KeyNotFoundException($"Order {dto.OrderId} not found");

        var item = await _unitOfWork.OrderItems.GetByIdAsync(dto.OrderItemId);
        if (item == null || item.IsDeleted) throw new KeyNotFoundException($"Item {dto.OrderItemId} not found");

        var settings = await GetRestaurantSettingAsync();

        // Validate approval and state per Section 3.3
        if (item.Status == OrderItemStatus.Served)
        {
            // Manager only, bill adjustment (OM-B06)
            ValidateManagerPin(settings, dto.ManagerPin);
            item.IsBillAdjusted = true;
            item.Status = OrderItemStatus.Cancelled;
            item.CancelReason = dto.Reason;
            item.CancelledAt = DateTime.UtcNow;
            await _unitOfWork.OrderItems.UpdateAsync(item);
        }
        else if (item.Status == OrderItemStatus.Preparing || item.Status == OrderItemStatus.Ready)
        {
            // Manager only, marked as wastage (OM-B04, OM-B05)
            ValidateManagerPin(settings, dto.ManagerPin);
            item.IsWastage = true;
            item.WastageCost = item.TotalPrice;
            item.Status = OrderItemStatus.Cancelled;
            item.CancelReason = dto.Reason;
            item.CancelledAt = DateTime.UtcNow;
            await _unitOfWork.OrderItems.UpdateAsync(item);
        }
        else
        {
            // Still Sent (not started) (OM-B03)
            if (settings.KitchenMode == KitchenMode.NoDevice && !dto.InformedKitchen)
            {
                throw new InvalidOperationException("You must tick 'I have informed the kitchen' before the cancellation is accepted (FR-OM-05, OM-D05).");
            }

            int reduceQty = dto.ReduceByQuantity > 0 ? dto.ReduceByQuantity : (item.Quantity - dto.RemainingQuantity);
            if (reduceQty <= 0) reduceQty = 1;

            if (item.Quantity <= reduceQty)
            {
                // Full cancel
                item.Status = OrderItemStatus.Cancelled;
                item.CancelReason = dto.Reason;
                item.CancelledAt = DateTime.UtcNow;
                await _unitOfWork.OrderItems.UpdateAsync(item);
            }
            else
            {
                // Partial reduction (e.g. 4 Naan to 3 Naan, Worked Example Step 7)
                item.Quantity -= reduceQty;
                item.TotalPrice = item.UnitPrice * item.Quantity;
                await _unitOfWork.OrderItems.UpdateAsync(item);
            }

            // Generate Cancel Slip (KOTType.Cancellation)
            var table = order.TableId.HasValue ? await _unitOfWork.Tables.GetByIdAsync(order.TableId.Value) : null;
            int totalKots = await _unitOfWork.KOTs.CountAsync();
            var cancelKot = new KOT
            {
                KOTNumber = $"KOT-{(totalKots + 1):D4}",
                OrderId = order.Id,
                TableNumber = table?.TableNumber ?? "NA",
                WaiterName = "Staff",
                Status = KOTStatus.New,
                Type = KOTType.Cancellation,
                Reason = dto.Reason,
                Notes = $"CANCELLED {reduceQty} × {item.ItemName} (Table {table?.TableNumber ?? "NA"})",
                InformedKitchenConfirmed = dto.InformedKitchen,
                CreatedAt = DateTime.UtcNow
            };
            cancelKot.Items.Add(new KOTItem
            {
                MenuItemName = item.ItemName,
                Quantity = reduceQty,
                SpecialInstructions = $"Cancelled: {dto.Reason}",
                Status = KOTStatus.New,
                IsCancelled = true,
                OrderItemId = item.Id
            });
            await _unitOfWork.KOTs.AddAsync(cancelKot);
        }

        await _unitOfWork.SaveChangesAsync();
        await RecalculateOrderTotalsAsync(order);
        order.UpdatedAt = DateTime.UtcNow;
        order.Version++;
        await _unitOfWork.Orders.UpdateAsync(order);
        await _unitOfWork.SaveChangesAsync();

        return (await GetOrderByIdAsync(order.Id))!;
    }

    public async Task<OrderDto> ReplaceItemAsync(ReplaceItemDto dto)
    {
        var order = await _unitOfWork.Orders.GetByIdAsync(dto.OrderId);
        if (order == null) throw new KeyNotFoundException($"Order {dto.OrderId} not found");

        var oldItem = await _unitOfWork.OrderItems.GetByIdAsync(dto.OldOrderItemId);
        if (oldItem == null) throw new KeyNotFoundException($"Item {dto.OldOrderItemId} not found");

        var settings = await GetRestaurantSettingAsync();
        if (settings.KitchenMode == KitchenMode.NoDevice && !dto.InformedKitchen)
        {
            throw new InvalidOperationException("You must inform the kitchen before replacing a sent item (FR-OM-05).");
        }

        // Cancel old item
        oldItem.Status = OrderItemStatus.Cancelled;
        oldItem.CancelReason = $"Replaced by {dto.NewItem.MenuItemName}: {dto.Reason}";
        oldItem.CancelledAt = DateTime.UtcNow;
        await _unitOfWork.OrderItems.UpdateAsync(oldItem);

        // Add new item as Sent immediately with ADDITION KOT
        var table = order.TableId.HasValue ? await _unitOfWork.Tables.GetByIdAsync(order.TableId.Value) : null;
        var menuItem = await _unitOfWork.MenuItems.GetByIdAsync(dto.NewItem.MenuItemId);
        if (menuItem == null) throw new InvalidOperationException("New menu item not found.");

        decimal price = menuItem.Price;
        if (!string.IsNullOrEmpty(dto.NewItem.SelectedVariantName))
        {
            var variant = (await _unitOfWork.MenuItemVariants.FindAsync(v => v.MenuItemId == menuItem.Id && v.VariantName == dto.NewItem.SelectedVariantName)).FirstOrDefault();
            if (variant != null) price = variant.Price;
        }

        int totalKots = await _unitOfWork.KOTs.CountAsync();
        string kotNo = $"KOT-{(totalKots + 1):D4}";

        var newItem = new OrderItem
        {
            OrderId = order.Id,
            MenuItemId = menuItem.Id,
            ItemName = menuItem.Name,
            UnitPrice = price,
            Quantity = dto.NewItem.Quantity,
            TotalPrice = price * dto.NewItem.Quantity,
            SpecialInstructions = CleanText(dto.NewItem.SpecialInstructions),
            SelectedVariantName = dto.NewItem.SelectedVariantName,
            Status = OrderItemStatus.Sent,
            SentAt = DateTime.UtcNow,
            KOTNumber = kotNo,
            Course = dto.NewItem.Course
        };
        await _unitOfWork.OrderItems.AddAsync(newItem);

        // Generate combined Replace KOT slip
        var kot = new KOT
        {
            KOTNumber = kotNo,
            OrderId = order.Id,
            TableNumber = table?.TableNumber ?? "NA",
            WaiterName = "Staff",
            Status = KOTStatus.New,
            Type = KOTType.Addition,
            Notes = $"REPLACE: Cancelled {oldItem.ItemName}, Added {newItem.ItemName} ({dto.Reason})",
            InformedKitchenConfirmed = dto.InformedKitchen,
            CreatedAt = DateTime.UtcNow
        };
        kot.Items.Add(new KOTItem
        {
            MenuItemName = oldItem.ItemName,
            Quantity = oldItem.Quantity,
            SpecialInstructions = $"CANCELLED: Replaced ({dto.Reason})",
            Status = KOTStatus.New,
            IsCancelled = true
        });
        kot.Items.Add(new KOTItem
        {
            MenuItemName = newItem.ItemName + (!string.IsNullOrEmpty(newItem.SelectedVariantName) ? $" ({newItem.SelectedVariantName})" : ""),
            Quantity = newItem.Quantity,
            SpecialInstructions = newItem.SpecialInstructions,
            Status = KOTStatus.New
        });
        await _unitOfWork.KOTs.AddAsync(kot);
        await _unitOfWork.SaveChangesAsync();

        await RecalculateOrderTotalsAsync(order);
        order.UpdatedAt = DateTime.UtcNow;
        order.Version++;
        await _unitOfWork.Orders.UpdateAsync(order);
        await _unitOfWork.SaveChangesAsync();

        return (await GetOrderByIdAsync(order.Id))!;
    }

    public async Task<OrderDto> ChangeItemNoteAsync(ChangeItemNoteDto dto)
    {
        var order = await _unitOfWork.Orders.GetByIdAsync(dto.OrderId);
        if (order == null) throw new KeyNotFoundException($"Order {dto.OrderId} not found");

        var item = await _unitOfWork.OrderItems.GetByIdAsync(dto.OrderItemId);
        if (item == null) throw new KeyNotFoundException($"Item {dto.OrderItemId} not found");

        if (item.Status == OrderItemStatus.Preparing)
        {
            throw new InvalidOperationException("Kitchen already started cooking. Tell the chef directly (OM-B10).");
        }

        string cleanNote = CleanText(dto.NewNote);
        if (cleanNote.Length > 100)
        {
            throw new ArgumentException("Maximum 100 characters allowed for item note (OM-C03).");
        }

        item.SpecialInstructions = cleanNote;
        await _unitOfWork.OrderItems.UpdateAsync(item);

        // Generate NOTE CHANGE slip (OM-B09)
        if (item.Status == OrderItemStatus.Sent)
        {
            var table = order.TableId.HasValue ? await _unitOfWork.Tables.GetByIdAsync(order.TableId.Value) : null;
            int totalKots = await _unitOfWork.KOTs.CountAsync();
            var kot = new KOT
            {
                KOTNumber = $"KOT-{(totalKots + 1):D4}",
                OrderId = order.Id,
                TableNumber = table?.TableNumber ?? "NA",
                WaiterName = "Staff",
                Status = KOTStatus.New,
                Type = KOTType.NoteChange,
                Notes = $"NOTE CHANGE: {item.ItemName} -> {cleanNote}",
                CreatedAt = DateTime.UtcNow
            };
            kot.Items.Add(new KOTItem
            {
                MenuItemName = item.ItemName,
                Quantity = item.Quantity,
                SpecialInstructions = $"NOTE CHANGE: {cleanNote}",
                Status = KOTStatus.New,
                OrderItemId = item.Id
            });
            await _unitOfWork.KOTs.AddAsync(kot);
        }

        await _unitOfWork.SaveChangesAsync();
        return (await GetOrderByIdAsync(order.Id))!;
    }

    public async Task<OrderDto> UpdateItemStatusAsync(int orderId, int orderItemId, OrderItemStatus status)
    {
        var order = await _unitOfWork.Orders.GetByIdAsync(orderId);
        if (order == null) throw new KeyNotFoundException($"Order {orderId} not found");

        var item = await _unitOfWork.OrderItems.GetByIdAsync(orderItemId);
        if (item == null) throw new KeyNotFoundException($"Item {orderItemId} not found");

        var settings = await GetRestaurantSettingAsync();

        // Kitchen Mode Rules (Section 2.1)
        if (settings.KitchenMode == KitchenMode.NoDevice && (status == OrderItemStatus.Preparing || status == OrderItemStatus.Ready))
        {
            throw new InvalidOperationException("Mode 3 (No kitchen device) does not use Preparing or Ready status. Waiter taps Served directly (Section 2, 2.1).");
        }
        if (settings.KitchenMode == KitchenMode.PrinterOnly && status == OrderItemStatus.Preparing)
        {
            throw new InvalidOperationException("Mode 2 (Printer only) does not use Preparing status (Section 2, 2.1).");
        }

        item.Status = status;
        if (status == OrderItemStatus.Preparing) item.PreparingAt = DateTime.UtcNow;
        else if (status == OrderItemStatus.Ready) item.ReadyAt = DateTime.UtcNow;
        else if (status == OrderItemStatus.Served) item.ServedAt = DateTime.UtcNow;

        await _unitOfWork.OrderItems.UpdateAsync(item);

        // Check if all active items are served
        var allItems = await _unitOfWork.OrderItems.FindAsync(i => i.OrderId == orderId && !i.IsDeleted && i.Status != OrderItemStatus.Cancelled);
        if (allItems.All(i => i.Status == OrderItemStatus.Served))
        {
            order.Status = OrderStatus.Preparing; // In trans state before billing
        }

        await _unitOfWork.SaveChangesAsync();
        return (await GetOrderByIdAsync(order.Id))!;
    }

    public async Task<OrderDto> MarkAllServedAsync(int orderId)
    {
        var order = await _unitOfWork.Orders.GetByIdAsync(orderId);
        if (order == null) throw new KeyNotFoundException($"Order {orderId} not found");

        var items = await _unitOfWork.OrderItems.FindAsync(i => i.OrderId == orderId && !i.IsDeleted && i.Status != OrderItemStatus.Cancelled);
        foreach (var item in items)
        {
            item.Status = OrderItemStatus.Served;
            item.ServedAt = DateTime.UtcNow;
            await _unitOfWork.OrderItems.UpdateAsync(item);
        }

        await _unitOfWork.SaveChangesAsync();
        return (await GetOrderByIdAsync(order.Id))!;
    }

    public async Task<OrderDto> MakeItemComplimentaryAsync(ComplimentaryItemDto dto)
    {
        var order = await _unitOfWork.Orders.GetByIdAsync(dto.OrderId);
        if (order == null) throw new KeyNotFoundException($"Order {dto.OrderId} not found");

        var item = await _unitOfWork.OrderItems.GetByIdAsync(dto.OrderItemId);
        if (item == null) throw new KeyNotFoundException($"Item {dto.OrderItemId} not found");

        var settings = await GetRestaurantSettingAsync();
        ValidateManagerPin(settings, dto.ManagerPin);

        item.IsComplimentary = true;
        item.ComplimentaryReason = dto.Reason;
        item.UnitPrice = 0m;
        item.TotalPrice = 0m;

        await _unitOfWork.OrderItems.UpdateAsync(item);
        await _unitOfWork.SaveChangesAsync();

        await RecalculateOrderTotalsAsync(order);
        order.UpdatedAt = DateTime.UtcNow;
        order.Version++;
        await _unitOfWork.Orders.UpdateAsync(order);
        await _unitOfWork.SaveChangesAsync();

        return (await GetOrderByIdAsync(order.Id))!;
    }

    public async Task<OrderDto> ReturnItemAsync(ReturnItemDto dto)
    {
        var order = await _unitOfWork.Orders.GetByIdAsync(dto.OrderId);
        if (order == null) throw new KeyNotFoundException($"Order {dto.OrderId} not found");

        var item = await _unitOfWork.OrderItems.GetByIdAsync(dto.OrderItemId);
        if (item == null) throw new KeyNotFoundException($"Item {dto.OrderItemId} not found");

        var settings = await GetRestaurantSettingAsync();
        ValidateManagerPin(settings, dto.ManagerPin);

        if (dto.IsRemake)
        {
            // Remake KOT (OM-F05)
            var table = order.TableId.HasValue ? await _unitOfWork.Tables.GetByIdAsync(order.TableId.Value) : null;
            int totalKots = await _unitOfWork.KOTs.CountAsync();
            var remakeKot = new KOT
            {
                KOTNumber = $"KOT-{(totalKots + 1):D4}",
                OrderId = order.Id,
                TableNumber = table?.TableNumber ?? "NA",
                WaiterName = "Staff",
                Status = KOTStatus.New,
                Type = KOTType.Remake,
                Notes = $"REMAKE: {item.ItemName} ({dto.Reason})",
                CreatedAt = DateTime.UtcNow
            };
            remakeKot.Items.Add(new KOTItem
            {
                MenuItemName = $"{item.ItemName} [REMAKE]",
                Quantity = item.Quantity,
                SpecialInstructions = $"REMAKE (No Charge): {dto.Reason}",
                Status = KOTStatus.New,
                OrderItemId = item.Id
            });
            await _unitOfWork.KOTs.AddAsync(remakeKot);
            await _unitOfWork.SaveChangesAsync();
        }
        else
        {
            // Remove / reduce bill
            await ReduceItemQuantityAsync(new ReduceItemDto
            {
                OrderId = dto.OrderId,
                OrderItemId = dto.OrderItemId,
                ReduceByQuantity = item.Quantity,
                Reason = $"Returned ({dto.Reason})",
                InformedKitchen = true,
                ManagerPin = dto.ManagerPin
            });
        }

        return (await GetOrderByIdAsync(order.Id))!;
    }

    public async Task<bool> TransferTableAsync(int sourceTableId, int targetTableId)
    {
        var sourceTable = await _unitOfWork.Tables.GetByIdAsync(sourceTableId);
        var targetTable = await _unitOfWork.Tables.GetByIdAsync(targetTableId);

        if (sourceTable == null || targetTable == null) return false;
        if (!sourceTable.CurrentOrderId.HasValue) return false;
        if (targetTable.Status != TableStatus.Available)
        {
            throw new InvalidOperationException($"Target table {targetTable.TableNumber} must be Available to transfer (OM-E01).");
        }

        var order = await _unitOfWork.Orders.GetByIdAsync(sourceTable.CurrentOrderId.Value);
        if (order == null) return false;

        string transferNote = $"Moved from Table {sourceTable.TableNumber} to Table {targetTable.TableNumber}";
        order.TableId = targetTable.Id;
        order.TableTransferNotes += (string.IsNullOrEmpty(order.TableTransferNotes) ? "" : "; ") + transferNote;
        order.UpdatedAt = DateTime.UtcNow;

        targetTable.Status = TableStatus.Occupied;
        targetTable.CurrentOrderId = order.Id;
        targetTable.WaiterId = sourceTable.WaiterId;

        sourceTable.Status = TableStatus.Available;
        sourceTable.CurrentOrderId = null;
        sourceTable.WaiterId = null;

        await _unitOfWork.Orders.UpdateAsync(order);
        await _unitOfWork.Tables.UpdateAsync(sourceTable);
        await _unitOfWork.Tables.UpdateAsync(targetTable);

        // Generate Transfer KOT notification slip
        int totalKots = await _unitOfWork.KOTs.CountAsync();
        var kot = new KOT
        {
            KOTNumber = $"KOT-{(totalKots + 1):D4}",
            OrderId = order.Id,
            TableNumber = targetTable.TableNumber,
            WaiterName = "Staff",
            Status = KOTStatus.New,
            Type = KOTType.NoteChange,
            Notes = transferNote,
            CreatedAt = DateTime.UtcNow
        };
        await _unitOfWork.KOTs.AddAsync(kot);

        await _unitOfWork.SaveChangesAsync();
        return true;
    }

    public async Task<bool> MergeTablesAsync(int sourceTableId, int targetTableId)
    {
        var sourceTable = await _unitOfWork.Tables.GetByIdAsync(sourceTableId);
        var targetTable = await _unitOfWork.Tables.GetByIdAsync(targetTableId);

        if (sourceTable == null || targetTable == null) return false;
        if (!sourceTable.CurrentOrderId.HasValue || !targetTable.CurrentOrderId.HasValue) return false;

        var sourceOrder = await _unitOfWork.Orders.GetByIdAsync(sourceTable.CurrentOrderId.Value);
        var targetOrder = await _unitOfWork.Orders.GetByIdAsync(targetTable.CurrentOrderId.Value);

        if (sourceOrder == null || targetOrder == null) return false;

        // Move all items from source order to target order (OM-E02)
        var sourceItems = await _unitOfWork.OrderItems.FindAsync(i => i.OrderId == sourceOrder.Id && !i.IsDeleted);
        foreach (var item in sourceItems)
        {
            item.OrderId = targetOrder.Id;
            await _unitOfWork.OrderItems.UpdateAsync(item);
        }

        targetOrder.Covers += sourceOrder.Covers;
        await _unitOfWork.SaveChangesAsync();
        await RecalculateOrderTotalsAsync(targetOrder);
        targetOrder.UpdatedAt = DateTime.UtcNow;
        targetOrder.Version++;
        await _unitOfWork.Orders.UpdateAsync(targetOrder);

        // Mark source order cancelled / merged
        sourceOrder.Status = OrderStatus.Cancelled;
        sourceOrder.SpecialInstructions += $" [Merged into Order {targetOrder.OrderNumber} / Table {targetTable.TableNumber}]";
        await _unitOfWork.Orders.UpdateAsync(sourceOrder);

        // Release source table
        sourceTable.Status = TableStatus.Available;
        sourceTable.CurrentOrderId = null;
        sourceTable.WaiterId = null;
        await _unitOfWork.Tables.UpdateAsync(sourceTable);

        await _unitOfWork.SaveChangesAsync();
        return true;
    }

    public async Task<bool> MarkWalkoutAsync(WalkoutOrderDto dto)
    {
        var order = await _unitOfWork.Orders.GetByIdAsync(dto.OrderId);
        if (order == null) return false;

        var settings = await GetRestaurantSettingAsync();
        ValidateManagerPin(settings, dto.ManagerPin);

        order.IsWalkout = true;
        order.WalkoutReason = dto.Reason;
        order.Status = OrderStatus.Cancelled;
        order.SpecialInstructions += $" [Walkout / Unpaid: {dto.Reason}]";
        order.UpdatedAt = DateTime.UtcNow;

        if (order.TableId.HasValue)
        {
            var table = await _unitOfWork.Tables.GetByIdAsync(order.TableId.Value);
            if (table != null)
            {
                table.Status = TableStatus.Available;
                table.CurrentOrderId = null;
                await _unitOfWork.Tables.UpdateAsync(table);
            }
        }

        await _unitOfWork.Orders.UpdateAsync(order);
        await _unitOfWork.SaveChangesAsync();
        return true;
    }

    public async Task<bool> CancelOrderAsync(int orderId, string reason = "", string? managerPin = null)
    {
        var order = await _unitOfWork.Orders.GetByIdAsync(orderId);
        if (order == null) return false;

        var items = await _unitOfWork.OrderItems.FindAsync(i => i.OrderId == orderId && !i.IsDeleted);
        
        // Rule FR-ALO / OM-F04: Cannot cancel order if any items are Served!
        if (items.Any(i => i.Status == OrderItemStatus.Served))
        {
            throw new InvalidOperationException("Cannot cancel an order after items have been Served. Bill what was served or remove with bill adjustment (Rule FR-ALO, OM-F04).");
        }

        var kots = await _unitOfWork.KOTs.FindAsync(k => k.OrderId == orderId && !k.IsDeleted);
        var settings = await GetRestaurantSettingAsync();

        if (kots.Any())
        {
            // After KOT requires Manager approval (OM-F02, OM-F03)
            ValidateManagerPin(settings, managerPin);

            // If food is preparing or ready, mark wastage (OM-F03)
            foreach (var item in items)
            {
                if (item.Status == OrderItemStatus.Preparing || item.Status == OrderItemStatus.Ready)
                {
                    item.IsWastage = true;
                    item.WastageCost = item.TotalPrice;
                }
                item.Status = OrderItemStatus.Cancelled;
                item.CancelReason = reason;
                item.CancelledAt = DateTime.UtcNow;
                await _unitOfWork.OrderItems.UpdateAsync(item);
            }

            // Print full cancel slip
            var table = order.TableId.HasValue ? await _unitOfWork.Tables.GetByIdAsync(order.TableId.Value) : null;
            int totalKots = await _unitOfWork.KOTs.CountAsync();
            var cancelKot = new KOT
            {
                KOTNumber = $"KOT-{(totalKots + 1):D4}",
                OrderId = order.Id,
                TableNumber = table?.TableNumber ?? "NA",
                WaiterName = "Staff",
                Status = KOTStatus.New,
                Type = KOTType.Cancellation,
                Reason = reason,
                Notes = $"FULL ORDER CANCELLED: {reason}",
                CreatedAt = DateTime.UtcNow
            };
            await _unitOfWork.KOTs.AddAsync(cancelKot);
        }
        else
        {
            // Before KOT: draft can be freely cancelled (OM-F01)
            foreach (var item in items)
            {
                item.IsDeleted = true;
                await _unitOfWork.OrderItems.UpdateAsync(item);
            }
        }

        order.Status = OrderStatus.Cancelled;
        order.SpecialInstructions += $" [Cancelled: {reason}]";
        order.UpdatedAt = DateTime.UtcNow;

        if (order.TableId.HasValue)
        {
            var table = await _unitOfWork.Tables.GetByIdAsync(order.TableId.Value);
            if (table != null)
            {
                table.Status = TableStatus.Available;
                table.CurrentOrderId = null;
                table.HasActiveDraft = false;
                table.ActiveDraftUserId = null;
                table.ActiveDraftUserName = string.Empty;
                await _unitOfWork.Tables.UpdateAsync(table);
            }
        }

        await _unitOfWork.Orders.UpdateAsync(order);
        await _unitOfWork.SaveChangesAsync();
        return true;
    }

    public async Task<OrderDto> ReopenOrderAsync(int orderId, string reason, string? managerPin = null)
    {
        var order = await _unitOfWork.Orders.GetByIdAsync(orderId);
        if (order == null) throw new KeyNotFoundException($"Order {orderId} not found");

        var settings = await GetRestaurantSettingAsync();
        ValidateManagerPin(settings, managerPin);

        // Voids previous bill (OM-B15, OM-J12)
        var bills = await _unitOfWork.Bills.FindAsync(b => b.OrderId == orderId && !b.IsDeleted);
        foreach (var b in bills)
        {
            b.IsDeleted = true;
            await _unitOfWork.Bills.UpdateAsync(b);
        }

        order.IsSettled = false;
        order.Status = OrderStatus.Confirmed;
        order.SpecialInstructions += $" [Reopened: {reason}]";
        order.UpdatedAt = DateTime.UtcNow;
        order.Version++;

        if (order.TableId.HasValue)
        {
            var table = await _unitOfWork.Tables.GetByIdAsync(order.TableId.Value);
            if (table != null)
            {
                table.Status = TableStatus.Occupied;
                table.CurrentOrderId = order.Id;
                await _unitOfWork.Tables.UpdateAsync(table);
            }
        }

        await _unitOfWork.Orders.UpdateAsync(order);
        await _unitOfWork.SaveChangesAsync();

        return (await GetOrderByIdAsync(order.Id))!;
    }

    public async Task<BillDto> GenerateBillFromPOSAsync(int orderId, decimal discountAmount = 0m, string? discountReason = null, string? customerGstin = null)
    {
        var order = await _unitOfWork.Orders.GetByIdAsync(orderId);
        if (order == null) throw new KeyNotFoundException($"Order {orderId} not found");

        if (order.Status == OrderStatus.Completed || order.Status == OrderStatus.Cancelled || order.IsSettled)
        {
            throw new InvalidOperationException("Order is already billed or cancelled.");
        }

        var settings = await GetRestaurantSettingAsync();

        // Check unserved items rule (FR-BIL-02, OM-D04)
        var items = (await _unitOfWork.OrderItems.FindAsync(i => i.OrderId == orderId && !i.IsDeleted && i.Status != OrderItemStatus.Cancelled)).ToList();
        var unservedItems = items.Where(i => i.Status != OrderItemStatus.Served).ToList();

        if (unservedItems.Any())
        {
            if (settings.AllowBillingWithUnservedItems == BillingUnservedItemsSetting.No)
            {
                throw new InvalidOperationException("Cannot generate bill: Some items have not been marked Served (FR-BIL-02).");
            }
            else
            {
                // Auto-mark all remaining rows Served
                foreach (var item in unservedItems)
                {
                    item.Status = OrderItemStatus.Served;
                    item.ServedAt = DateTime.UtcNow;
                    await _unitOfWork.OrderItems.UpdateAsync(item);
                }
            }
        }

        // Validate GSTIN if provided (OM-H04)
        if (!string.IsNullOrWhiteSpace(customerGstin))
        {
            string gstin = customerGstin.Trim().ToUpperInvariant();
            if (!Regex.IsMatch(gstin, "^[0-9]{2}[A-Z]{5}[0-9]{4}[A-Z]{1}[1-9A-Z]{1}Z[0-9A-Z]{1}$"))
            {
                throw new ArgumentException("Invalid GSTIN format. Must be a 15-character valid Indian GSTIN (OM-H04).");
            }
            order.CustomerGSTIN = gstin;
        }

        // Apply discount if provided (OM-H03, OM-J08)
        if (discountAmount > order.SubTotal)
        {
            throw new ArgumentException("Discount cannot exceed order subtotal (OM-J08).");
        }
        order.DiscountAmount = discountAmount;
        await _unitOfWork.SaveChangesAsync();
        await RecalculateOrderTotalsAsync(order);
        order.UpdatedAt = DateTime.UtcNow;
        order.Version++;
        await _unitOfWork.Orders.UpdateAsync(order);

        // Turn table to Billing status (Worked Example Step 9)
        if (order.TableId.HasValue)
        {
            var table = await _unitOfWork.Tables.GetByIdAsync(order.TableId.Value);
            if (table != null)
            {
                table.Status = TableStatus.Billing;
                await _unitOfWork.Tables.UpdateAsync(table);
            }
        }

        await _unitOfWork.SaveChangesAsync();

        if (_billingService != null)
        {
            return await _billingService.GenerateBillAsync(new CreateBillDto
            {
                OrderId = order.Id,
                DiscountAmount = discountAmount,
                ServiceCharge = 0m
            });
        }

        // Direct fallback when _billingService is null (e.g. unit tests)
        int billCount = await _unitOfWork.Bills.CountAsync();
        var bill = new Bill
        {
            InvoiceNumber = $"INV-{(billCount + 10001)}",
            OrderId = order.Id,
            BillDate = DateTime.UtcNow,
            SubTotal = order.SubTotal,
            DiscountAmount = discountAmount,
            ServiceCharge = 0m,
            CGST = order.CGST,
            SGST = order.SGST,
            GrandTotal = order.GrandTotal,
            IsPaid = false
        };
        await _unitOfWork.Bills.AddAsync(bill);
        await _unitOfWork.SaveChangesAsync();

        return new BillDto
        {
            Id = bill.Id,
            InvoiceNumber = bill.InvoiceNumber,
            OrderId = order.Id,
            OrderNumber = order.OrderNumber,
            SubTotal = bill.SubTotal,
            DiscountAmount = bill.DiscountAmount,
            CGST = bill.CGST,
            SGST = bill.SGST,
            GrandTotal = bill.GrandTotal,
            IsPaid = false,
            BillDate = bill.BillDate
        };
    }

    // ================= HELPER METHODS =================

    private async Task AddItemToOrderInternalAsync(Order order, CreateOrderItemDto itemDto)
    {
        if (itemDto.Quantity <= 0) throw new ArgumentException("Quantity must be greater than zero.");
        if (itemDto.Quantity > 99) throw new ArgumentException("Quantity must be between 1 and 99 (OM-C09).");

        var menuItem = await _unitOfWork.MenuItems.GetByIdAsync(itemDto.MenuItemId);
        if (menuItem == null || menuItem.IsDeleted || !menuItem.IsAvailable)
        {
            throw new InvalidOperationException($"Menu item '{menuItem?.Name ?? itemDto.MenuItemId.ToString()}' is currently sold out or unavailable (OM-A12).");
        }

        decimal price = menuItem.Price;
        string variantName = itemDto.SelectedVariantName ?? string.Empty;

        if (!string.IsNullOrEmpty(variantName))
        {
            var variant = (await _unitOfWork.MenuItemVariants.FindAsync(v => v.MenuItemId == menuItem.Id && v.VariantName == variantName)).FirstOrDefault();
            if (variant != null) price = variant.Price;
        }

        decimal itemTotal = price * itemDto.Quantity;

        var orderItemAddOns = new List<OrderItemAddOn>();
        if (itemDto.SelectedAddOnIds != null)
        {
            foreach (var addOnId in itemDto.SelectedAddOnIds)
            {
                var addOn = await _unitOfWork.AddOns.GetByIdAsync(addOnId);
                if (addOn != null && addOn.IsAvailable)
                {
                    itemTotal += addOn.Price * itemDto.Quantity;
                    orderItemAddOns.Add(new OrderItemAddOn
                    {
                        AddOnId = addOn.Id,
                        AddOnName = addOn.Name,
                        Price = addOn.Price
                    });
                }
            }
        }

        var orderItem = new OrderItem
        {
            OrderId = order.Id,
            Order = order,
            MenuItemId = menuItem.Id,
            ItemName = menuItem.Name,
            UnitPrice = price,
            Quantity = itemDto.Quantity,
            TotalPrice = itemTotal,
            SpecialInstructions = CleanText(itemDto.SpecialInstructions),
            SelectedVariantName = variantName,
            Status = OrderItemStatus.Pending,
            Course = string.IsNullOrWhiteSpace(itemDto.Course) ? "Main" : itemDto.Course,
            IsHeld = itemDto.IsHeld,
            Station = string.IsNullOrWhiteSpace(itemDto.Station) ? "Main" : itemDto.Station,
            OrderItemAddOns = orderItemAddOns
        };

        order.Items.Add(orderItem);
        if (order.Id > 0)
        {
            await _unitOfWork.OrderItems.AddAsync(orderItem);
        }
    }

    private async Task RecalculateOrderTotalsAsync(Order order)
    {
        List<OrderItem> activeItems;
        if (order.Id > 0)
        {
            var dbItems = await _unitOfWork.OrderItems.FindAsync(i => i.OrderId == order.Id && !i.IsDeleted && i.Status != OrderItemStatus.Cancelled);
            activeItems = dbItems.ToList();
            if (!activeItems.Any() && order.Items.Any())
            {
                activeItems = order.Items.Where(i => !i.IsDeleted && i.Status != OrderItemStatus.Cancelled).ToList();
            }
        }
        else
        {
            activeItems = order.Items.Where(i => !i.IsDeleted && i.Status != OrderItemStatus.Cancelled).ToList();
        }

        decimal subtotal = activeItems.Sum(i => i.TotalPrice);

        // 5% GST (2.5% CGST + 2.5% SGST)
        // Ensure total tax matches exactly: 735 * 0.05 = 36.75 (Worked Example Step 7)
        decimal totalTax = Math.Round(subtotal * 0.05m, 2);
        decimal cgst = Math.Round(subtotal * 0.025m, 2);
        decimal sgst = totalTax - cgst; // Split cleanly so cgst + sgst == totalTax

        decimal grossTotal = subtotal + totalTax + order.PackagingCharge + order.DeliveryCharge - order.DiscountAmount;
        if (grossTotal < 0) grossTotal = 0;

        // Round-off to nearest rupee (GL-02, Step 7 of Worked Example: 771.75 + 0.25 = 772.00)
        decimal grandTotal = Math.Round(grossTotal, MidpointRounding.AwayFromZero);
        decimal roundOff = grandTotal - grossTotal;

        order.SubTotal = subtotal;
        order.CGST = cgst;
        order.SGST = sgst;
        order.TotalTax = totalTax;
        order.RoundOff = roundOff;
        order.GrandTotal = grandTotal;
    }

    private void RecalculateOrderTotals(Order order)
    {
        var activeItems = order.Items.Where(i => !i.IsDeleted && i.Status != OrderItemStatus.Cancelled).ToList();
        decimal subtotal = activeItems.Sum(i => i.TotalPrice);

        decimal totalTax = Math.Round(subtotal * 0.05m, 2);
        decimal cgst = Math.Round(subtotal * 0.025m, 2);
        decimal sgst = totalTax - cgst;

        decimal grossTotal = subtotal + totalTax + order.PackagingCharge + order.DeliveryCharge - order.DiscountAmount;
        if (grossTotal < 0) grossTotal = 0;

        decimal grandTotal = Math.Round(grossTotal, MidpointRounding.AwayFromZero);
        decimal roundOff = grandTotal - grossTotal;

        order.SubTotal = subtotal;
        order.CGST = cgst;
        order.SGST = sgst;
        order.TotalTax = totalTax;
        order.RoundOff = roundOff;
        order.GrandTotal = grandTotal;
    }

    private async Task<RestaurantSetting> GetRestaurantSettingAsync()
    {
        var settings = await _unitOfWork.RestaurantSettings.GetAllAsync();
        return settings.FirstOrDefault() ?? new RestaurantSetting();
    }

    private void ValidateManagerPin(RestaurantSetting setting, string? pin)
    {
        if (setting.SinglePersonMode) return; // Single-person mode skips approvals (OM-D09)
        if (setting.VoidApproval == VoidApprovalSetting.Off) return;
        if (setting.VoidApproval == VoidApprovalSetting.AnySignedInUser) return;

        string expectedPin = string.IsNullOrEmpty(setting.ManagerPIN) ? "1234" : setting.ManagerPIN;
        if (string.IsNullOrWhiteSpace(pin) || pin.Trim() != expectedPin)
        {
            throw new UnauthorizedAccessException("Manager PIN required for this operation (Section 2.2, 3.3).");
        }
    }

    private static void ValidateOrderDto(CreateOrderDto dto)
    {
        if (dto.Items != null && dto.Items.Count > 100)
        {
            throw new ArgumentException("Orders cannot exceed 100 items (OM-A15).");
        }
    }

    private static string CleanText(string? input)
    {
        if (string.IsNullOrWhiteSpace(input)) return string.Empty;
        // Strip emojis and non-standard control characters for thermal printer safety (OM-C04)
        return Regex.Replace(input, @"[^\u0020-\u007E\u00A0-\u00FF\n\r\t]", "").Trim();
    }

    private static OrderDto MapToOrderDto(Order o, string? tableNumber, string? floorName, string? waiterName)
    {
        return new OrderDto
        {
            Id = o.Id,
            OrderNumber = o.OrderNumber,
            OrderType = o.OrderType,
            Status = o.Status,
            TableId = o.TableId,
            TableNumber = tableNumber,
            FloorName = floorName,
            WaiterId = o.WaiterId,
            WaiterName = waiterName,
            CustomerId = o.CustomerId,
            CustomerName = o.CustomerName,
            CustomerPhone = o.CustomerPhone,
            SpecialInstructions = o.SpecialInstructions,
            OrderNote = o.OrderNote,
            TokenNumber = o.TokenNumber,
            Covers = o.Covers,
            DeliveryAddress = o.DeliveryAddress,
            CustomerGSTIN = o.CustomerGSTIN,
            SubTotal = o.SubTotal,
            CGST = o.CGST,
            SGST = o.SGST,
            TotalTax = o.TotalTax,
            DiscountAmount = o.DiscountAmount,
            PackagingCharge = o.PackagingCharge,
            DeliveryCharge = o.DeliveryCharge,
            RoundOff = o.RoundOff,
            GrandTotal = o.GrandTotal,
            Version = o.Version,
            IsDraft = o.IsDraft,
            DraftUserId = o.DraftUserId,
            DraftUserName = o.DraftUserName,
            DraftExpiresAt = o.DraftExpiresAt,
            IsSettled = o.IsSettled,
            SettledAt = o.SettledAt,
            IsWalkout = o.IsWalkout,
            WalkoutReason = o.WalkoutReason,
            TableTransferNotes = o.TableTransferNotes,
            CreatedAt = o.CreatedAt
        };
    }

    private static OrderItemDto MapToOrderItemDto(OrderItem i)
    {
        return new OrderItemDto
        {
            Id = i.Id,
            OrderId = i.OrderId,
            MenuItemId = i.MenuItemId,
            ItemName = i.ItemName,
            UnitPrice = i.UnitPrice,
            Quantity = i.Quantity,
            TotalPrice = i.TotalPrice,
            SpecialInstructions = i.SpecialInstructions,
            SelectedVariantName = i.SelectedVariantName,
            Status = i.Status,
            KOTId = i.KOTId,
            KOTNumber = i.KOTNumber,
            Course = i.Course,
            IsHeld = i.IsHeld,
            Station = i.Station,
            SentAt = i.SentAt,
            PreparingAt = i.PreparingAt,
            ReadyAt = i.ReadyAt,
            ServedAt = i.ServedAt,
            CancelledAt = i.CancelledAt,
            CancelReason = i.CancelReason,
            IsWastage = i.IsWastage,
            WastageCost = i.WastageCost,
            IsComplimentary = i.IsComplimentary,
            ComplimentaryReason = i.ComplimentaryReason,
            IsBillAdjusted = i.IsBillAdjusted,
            AddOns = i.OrderItemAddOns?.Select(a => new OrderItemAddOnDto
            {
                Id = a.Id,
                AddOnId = a.AddOnId,
                AddOnName = a.AddOnName,
                Price = a.Price
            }).ToList() ?? new List<OrderItemAddOnDto>()
        };
    }

    private static KOTDto MapToKOTDto(KOT k)
    {
        return new KOTDto
        {
            Id = k.Id,
            KOTNumber = k.KOTNumber,
            OrderId = k.OrderId,
            TableNumber = k.TableNumber,
            WaiterName = k.WaiterName,
            Status = k.Status,
            Notes = k.Notes,
            Type = k.Type,
            Station = k.Station,
            IsRush = k.IsRush,
            IsPrinted = k.IsPrinted,
            PrintFailed = k.PrintFailed,
            AcknowledgedByChef = k.AcknowledgedByChef,
            Reason = k.Reason,
            CreatedAt = k.CreatedAt,
            Items = k.Items.Select(i => new KOTItemDto
            {
                Id = i.Id,
                MenuItemName = i.MenuItemName,
                Quantity = i.Quantity,
                SpecialInstructions = i.SpecialInstructions,
                Status = i.Status,
                OrderItemId = i.OrderItemId,
                Course = i.Course,
                IsCancelled = i.IsCancelled,
                Station = i.Station
            }).ToList()
        };
    }
}

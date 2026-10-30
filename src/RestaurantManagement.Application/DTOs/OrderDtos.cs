using RestaurantManagement.Domain.Enums;

namespace RestaurantManagement.Application.DTOs;

public class OrderDto
{
    public int Id { get; set; }
    public string OrderNumber { get; set; } = string.Empty;
    public OrderType OrderType { get; set; }
    public string OrderTypeName => OrderType.ToString();
    public OrderStatus Status { get; set; }
    public string StatusName => Status.ToString();
    
    public int? TableId { get; set; }
    public string? TableNumber { get; set; }
    public string? FloorName { get; set; }
    
    public int? WaiterId { get; set; }
    public string? WaiterName { get; set; }
    
    public int? CustomerId { get; set; }
    public string CustomerName { get; set; } = string.Empty;
    public string CustomerPhone { get; set; } = string.Empty;
    public string SpecialInstructions { get; set; } = string.Empty;
    
    public decimal SubTotal { get; set; }
    public decimal CGST { get; set; }
    public decimal SGST { get; set; }
    public decimal TotalTax { get; set; }
    public decimal DiscountAmount { get; set; }
    public decimal PackagingCharge { get; set; }
    public decimal DeliveryCharge { get; set; }
    public decimal RoundOff { get; set; }
    public decimal GrandTotal { get; set; }
    public DateTime CreatedAt { get; set; }

    // Order Management Specifications (FR-OM-01 to FR-OM-10)
    public int Covers { get; set; } = 1;
    public string OrderNote { get; set; } = string.Empty;
    public string TokenNumber { get; set; } = string.Empty;
    public string DeliveryAddress { get; set; } = string.Empty;
    public string OnlinePlatform { get; set; } = string.Empty;
    public string OnlineOrderReference { get; set; } = string.Empty;
    public string DeliveryPartnerName { get; set; } = string.Empty;
    public string CustomerGSTIN { get; set; } = string.Empty;
    public int Version { get; set; } = 1;
    public bool IsDraft { get; set; } = false;
    public int? DraftUserId { get; set; }
    public string DraftUserName { get; set; } = string.Empty;
    public DateTime? DraftExpiresAt { get; set; }
    public bool IsSettled { get; set; } = false;
    public DateTime? SettledAt { get; set; }
    public bool IsWalkout { get; set; } = false;
    public string WalkoutReason { get; set; } = string.Empty;
    public string TableTransferNotes { get; set; } = string.Empty;
    
    public List<OrderItemDto> Items { get; set; } = new();
    public List<KOTDto> KOTs { get; set; } = new();
}

public class OrderItemDto
{
    public int Id { get; set; }
    public int OrderId { get; set; }
    public int MenuItemId { get; set; }
    public string ItemName { get; set; } = string.Empty;
    public decimal UnitPrice { get; set; }
    public int Quantity { get; set; }
    public decimal TotalPrice { get; set; }
    public string SpecialInstructions { get; set; } = string.Empty;
    public string SelectedVariantName { get; set; } = string.Empty;

    // Order Management Specifications (Section 3 & 5)
    public OrderItemStatus Status { get; set; } = OrderItemStatus.Pending;
    public string StatusName => Status.ToString();
    public int? KOTId { get; set; }
    public string KOTNumber { get; set; } = string.Empty;
    public string Course { get; set; } = "Main";
    public bool IsHeld { get; set; } = false;
    public string Station { get; set; } = "Main";
    public DateTime? SentAt { get; set; }
    public DateTime? PreparingAt { get; set; }
    public DateTime? ReadyAt { get; set; }
    public DateTime? ServedAt { get; set; }
    public DateTime? CancelledAt { get; set; }
    public string CancelReason { get; set; } = string.Empty;
    public bool IsWastage { get; set; } = false;
    public decimal WastageCost { get; set; } = 0m;
    public bool IsComplimentary { get; set; } = false;
    public string ComplimentaryReason { get; set; } = string.Empty;
    public bool IsBillAdjusted { get; set; } = false;

    public List<OrderItemAddOnDto> AddOns { get; set; } = new();
}

public class OrderItemAddOnDto
{
    public int Id { get; set; }
    public int AddOnId { get; set; }
    public string AddOnName { get; set; } = string.Empty;
    public decimal Price { get; set; }
}

public class CreateOrderDto
{
    public OrderType OrderType { get; set; } = OrderType.DineIn;
    public int? TableId { get; set; }
    public int? WaiterId { get; set; }
    public int? CustomerId { get; set; }
    public string CustomerName { get; set; } = string.Empty;
    public string CustomerPhone { get; set; } = string.Empty;
    public string SpecialInstructions { get; set; } = string.Empty;
    public int Covers { get; set; } = 1;
    public string OrderNote { get; set; } = string.Empty;
    public string DeliveryAddress { get; set; } = string.Empty;
    public string OnlinePlatform { get; set; } = string.Empty;
    public string OnlineOrderReference { get; set; } = string.Empty;
    public string DeliveryPartnerName { get; set; } = string.Empty;
    public string CustomerGSTIN { get; set; } = string.Empty;
    public decimal PackagingCharge { get; set; } = 0m;
    public List<CreateOrderItemDto> Items { get; set; } = new();
}

public class CreateOrderItemDto
{
    public int? OrderItemId { get; set; }
    public int MenuItemId { get; set; }
    public string MenuItemName { get; set; } = string.Empty;
    public decimal UnitPrice { get; set; }
    public int Quantity { get; set; } = 1;
    public string SpecialInstructions { get; set; } = string.Empty;
    public string SelectedVariantName { get; set; } = string.Empty;
    public string Course { get; set; } = "Main";
    public bool IsHeld { get; set; } = false;
    public string Station { get; set; } = "Main";
    public List<int> SelectedAddOnIds { get; set; } = new();
}

public class KOTDto
{
    public int Id { get; set; }
    public string KOTNumber { get; set; } = string.Empty;
    public int OrderId { get; set; }
    public string TableNumber { get; set; } = string.Empty;
    public string WaiterName { get; set; } = string.Empty;
    public KOTStatus Status { get; set; }
    public string StatusName => Status.ToString();
    public KOTType Type { get; set; } = KOTType.Initial;
    public string TypeName => Type.ToString();
    public string Station { get; set; } = "All";
    public bool IsRush { get; set; } = false;
    public bool IsPrinted { get; set; } = true;
    public bool PrintFailed { get; set; } = false;
    public bool AcknowledgedByChef { get; set; } = false;
    public string Reason { get; set; } = string.Empty;
    public string Notes { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
    public List<KOTItemDto> Items { get; set; } = new();
}

public class KOTItemDto
{
    public int Id { get; set; }
    public string MenuItemName { get; set; } = string.Empty;
    public int Quantity { get; set; }
    public string SpecialInstructions { get; set; } = string.Empty;
    public KOTStatus Status { get; set; }
    public string StatusName => Status.ToString();
    public int? OrderItemId { get; set; }
    public string Course { get; set; } = "Main";
    public bool IsCancelled { get; set; } = false;
    public string Station { get; set; } = "Main";
}

public class UpdateKOTStatusDto
{
    public int KOTId { get; set; }
    public KOTStatus Status { get; set; }
}

// Action DTOs for OM scenarios (Section 5A - 5J)
public class SaveDraftOrderDto
{
    public int? TableId { get; set; }
    public int? UserId { get; set; }
    public string UserName { get; set; } = string.Empty;
    public int Covers { get; set; } = 1;
    public OrderType OrderType { get; set; } = OrderType.DineIn;
    public string CustomerName { get; set; } = string.Empty;
    public string CustomerPhone { get; set; } = string.Empty;
    public string OrderNote { get; set; } = string.Empty;
    public List<CreateOrderItemDto> Items { get; set; } = new();
}

public class ReduceItemDto
{
    public int OrderId { get; set; }
    public int OrderItemId { get; set; }
    public int ReduceByQuantity { get; set; } // quantity to reduce (e.g. 1)
    public int RemainingQuantity { get; set; } // target quantity (e.g. 3)
    public string Reason { get; set; } = string.Empty;
    public bool InformedKitchen { get; set; } = false;
    public string? ManagerPin { get; set; }
}

public class ReplaceItemDto
{
    public int OrderId { get; set; }
    public int OldOrderItemId { get; set; }
    public CreateOrderItemDto NewItem { get; set; } = new();
    public string Reason { get; set; } = string.Empty;
    public bool InformedKitchen { get; set; } = false;
    public string? ManagerPin { get; set; }
}

public class ChangeItemNoteDto
{
    public int OrderId { get; set; }
    public int OrderItemId { get; set; }
    public string NewNote { get; set; } = string.Empty;
}

public class MarkItemStatusDto
{
    public int OrderId { get; set; }
    public int OrderItemId { get; set; }
    public OrderItemStatus Status { get; set; }
}

public class ReturnItemDto
{
    public int OrderId { get; set; }
    public int OrderItemId { get; set; }
    public string Reason { get; set; } = string.Empty; // Quality, Wrong dish, Too spicy
    public bool IsRemake { get; set; } = false; // Remake vs Remove
    public string? ManagerPin { get; set; }
}

public class ComplimentaryItemDto
{
    public int OrderId { get; set; }
    public int OrderItemId { get; set; }
    public string Reason { get; set; } = "Complimentary";
    public string? ManagerPin { get; set; }
}

public class TableMoveOrMergeDto
{
    public int SourceTableId { get; set; }
    public int TargetTableId { get; set; }
    public bool IsMerge { get; set; } = false; // Transfer if false, Merge if true
    public string? ManagerPin { get; set; }
}

public class SplitBillRequestDto
{
    public int OrderId { get; set; }
    public List<int> ItemIdsToSplit { get; set; } = new();
    public int EqualSplitParts { get; set; } = 1;
}

public class WalkoutOrderDto
{
    public int OrderId { get; set; }
    public string Reason { get; set; } = "Customer left without paying";
    public string? ManagerPin { get; set; }
}

public class SendKOTRequestDto
{
    public int OrderId { get; set; }
    public int? TableId { get; set; }
    public int Covers { get; set; } = 2;
    public string? OrderNote { get; set; }
    public string? WaiterName { get; set; }
    public List<CreateOrderItemDto>? NewItems { get; set; }
    public List<CreateOrderItemDto>? Items { get; set; } // Alias so both NewItems and Items map correctly
    public string Notes { get; set; } = string.Empty;
    public bool IsRush { get; set; } = false;
}

public class BillFromPOSRequestDto
{
    public int OrderId { get; set; }
    public decimal DiscountAmount { get; set; } = 0m;
    public string? DiscountReason { get; set; }
    public string? CustomerGstin { get; set; }
}

public class CreateParcelOrderDto
{
    public string CustomerName { get; set; } = string.Empty;
    public string CustomerPhone { get; set; } = string.Empty;
    public string? OrderNote { get; set; }
    public List<CreateOrderItemDto> Items { get; set; } = new();
}



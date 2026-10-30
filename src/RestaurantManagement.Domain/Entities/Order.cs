using RestaurantManagement.Domain.Enums;

namespace RestaurantManagement.Domain.Entities;

public class Order : BaseEntity
{
    public string OrderNumber { get; set; } = string.Empty;
    public OrderType OrderType { get; set; } = OrderType.DineIn;
    public OrderStatus Status { get; set; } = OrderStatus.Pending;
    
    public int? TableId { get; set; }
    public Table? Table { get; set; }
    
    public int? WaiterId { get; set; }
    public User? Waiter { get; set; }
    
    public int? CustomerId { get; set; }
    public Customer? Customer { get; set; }
    
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
    
    // Order Management Specifications
    public int Covers { get; set; } = 1;
    public string OrderNote { get; set; } = string.Empty; // FR-OM-08 (Max 250 chars)
    public string TokenNumber { get; set; } = string.Empty; // OM-A07 (T-001)
    public string DeliveryAddress { get; set; } = string.Empty; // OM-A08
    public string OnlinePlatform { get; set; } = string.Empty; // Swiggy, Zomato, Direct, MagicPin
    public string OnlineOrderReference { get; set; } = string.Empty; // e.g. SWIG-1234, ZOM-5678
    public string DeliveryPartnerName { get; set; } = string.Empty; // Rider name & phone
    public string CustomerGSTIN { get; set; } = string.Empty; // OM-H04
    public int Version { get; set; } = 1; // GL-09 Optimistic locking
    public bool IsDraft { get; set; } = false; // OM-A03
    public int? DraftUserId { get; set; }
    public string DraftUserName { get; set; } = string.Empty;
    public DateTime? DraftExpiresAt { get; set; }
    public string CreatedByUserName { get; set; } = string.Empty;
    public bool IsSettled { get; set; } = false;
    public DateTime? SettledAt { get; set; }
    public bool IsWalkout { get; set; } = false;
    public string WalkoutReason { get; set; } = string.Empty;
    public string TableTransferNotes { get; set; } = string.Empty;
    
    public ICollection<OrderItem> Items { get; set; } = new List<OrderItem>();
    public ICollection<KOT> KOTs { get; set; } = new List<KOT>();
    public Bill? Bill { get; set; }
}

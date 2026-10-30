using RestaurantManagement.Domain.Enums;

namespace RestaurantManagement.Application.DTOs;

public class BillDto
{
    public int Id { get; set; }
    public string InvoiceNumber { get; set; } = string.Empty;
    public int OrderId { get; set; }
    public string OrderNumber { get; set; } = string.Empty;
    public string? TableNumber { get; set; }
    public DateTime BillDate { get; set; }
    public decimal SubTotal { get; set; }
    public decimal DiscountAmount { get; set; }
    public decimal ServiceCharge { get; set; }
    public decimal CGST { get; set; }
    public decimal SGST { get; set; }
    public decimal GrandTotal { get; set; }
    public bool IsPaid { get; set; }
    public OrderDto? Order { get; set; }
    public List<PaymentDto> Payments { get; set; } = new();
}

public class CreateBillDto
{
    public int OrderId { get; set; }
    public decimal DiscountAmount { get; set; } = 0m;
    public decimal ServiceCharge { get; set; } = 0m;
}

public class PaymentDto
{
    public int Id { get; set; }
    public int BillId { get; set; }
    public PaymentMethod PaymentMethod { get; set; }
    public string PaymentMethodName => PaymentMethod.ToString();
    public decimal Amount { get; set; }
    public PaymentStatus PaymentStatus { get; set; }
    public string PaymentStatusName => PaymentStatus.ToString();
    public string TransactionId { get; set; } = string.Empty;
    public DateTime PaymentDate { get; set; }
    public string Notes { get; set; } = string.Empty;
}

public class CollectPaymentDto
{
    public int BillId { get; set; }
    public PaymentMethod PaymentMethod { get; set; } = PaymentMethod.Cash;
    public decimal Amount { get; set; }
    public string TransactionId { get; set; } = string.Empty;
    public string Notes { get; set; } = string.Empty;
    public int? CustomerId { get; set; }
    public string? NewCustomerName { get; set; }
    public string? NewCustomerPhone { get; set; }
    public decimal SplitCashAmount { get; set; }
    public decimal SplitOnlineAmount { get; set; }
}

public class CustomerDto
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Phone { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public DateTime? DateOfBirth { get; set; }
    public string Address { get; set; } = string.Empty;
    public string GSTIN { get; set; } = string.Empty;
    public string Notes { get; set; } = string.Empty;
    public int TotalVisits { get; set; }
    public decimal TotalSpent { get; set; }
    public int LoyaltyPoints { get; set; }
    public LoyaltyTier Tier { get; set; }
    public string TierName => Tier.ToString();
    public DateTime? LastVisitDate { get; set; }
    public decimal OutstandingCredit { get; set; }
    public bool IsActive { get; set; } = true;
    public DateTime CreatedAt { get; set; }
}

public class CustomerCreateUpdateDto
{
    public string Name { get; set; } = string.Empty;
    public string Phone { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public DateTime? DateOfBirth { get; set; }
    public string Address { get; set; } = string.Empty;
    public string GSTIN { get; set; } = string.Empty;
    public string Notes { get; set; } = string.Empty;
    public bool IsActive { get; set; } = true;
}

public class ReservationDto
{
    public int Id { get; set; }
    public string CustomerName { get; set; } = string.Empty;
    public string CustomerPhone { get; set; } = string.Empty;
    public DateTime ReservationDate { get; set; }
    public int GuestCount { get; set; }
    public int? TableId { get; set; }
    public string? TableNumber { get; set; }
    public ReservationStatus Status { get; set; }
    public string StatusName => Status.ToString();
    public string SpecialRequests { get; set; } = string.Empty;
}

public class CreateReservationDto
{
    public string CustomerName { get; set; } = string.Empty;
    public string CustomerPhone { get; set; } = string.Empty;
    public DateTime ReservationDate { get; set; }
    public int GuestCount { get; set; } = 2;
    public int? TableId { get; set; }
    public string SpecialRequests { get; set; } = string.Empty;
}

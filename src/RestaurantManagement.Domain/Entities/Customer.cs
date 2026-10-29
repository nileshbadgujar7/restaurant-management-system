using RestaurantManagement.Domain.Enums;

namespace RestaurantManagement.Domain.Entities;

public class Customer : BaseEntity
{
    public string Name { get; set; } = string.Empty;
    public string Phone { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public DateTime? DateOfBirth { get; set; }
    public string Address { get; set; } = string.Empty;
    public int TotalVisits { get; set; } = 0;
    public decimal TotalSpent { get; set; } = 0m;
    public int LoyaltyPoints { get; set; } = 0;
    public LoyaltyTier Tier { get; set; } = LoyaltyTier.Silver;
    public DateTime? LastVisitDate { get; set; }
    public decimal OutstandingCredit { get; set; } = 0m;
    
    public LoyaltyAccount? LoyaltyAccount { get; set; }
    public ICollection<CustomerLedger> Ledgers { get; set; } = new List<CustomerLedger>();
}

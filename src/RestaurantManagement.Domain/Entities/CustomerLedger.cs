namespace RestaurantManagement.Domain.Entities;

public class CustomerLedger : BaseEntity
{
    public int CustomerId { get; set; }
    public Customer? Customer { get; set; }
    public DateTime Date { get; set; } = DateTime.UtcNow;
    public string Description { get; set; } = string.Empty;
    public decimal DebitAmount { get; set; } = 0m; // Credit given (Customer owes more)
    public decimal CreditAmount { get; set; } = 0m; // Payment received (Customer owes less)
    public decimal RunningBalance { get; set; } = 0m;
    public string ReferenceInvoiceNumber { get; set; } = string.Empty;
    public string Notes { get; set; } = string.Empty;
    public string RecordedBy { get; set; } = string.Empty;
}

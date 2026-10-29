using RestaurantManagement.Domain.Enums;

namespace RestaurantManagement.Domain.Entities;

public class EmployeeLedger : BaseEntity
{
    public int EmployeeId { get; set; }
    public Employee? Employee { get; set; }
    public DateTime TransactionDate { get; set; } = DateTime.UtcNow;
    public string TransactionType { get; set; } = "Advance"; // Advance, Repayment, SalaryPayment, Deduction, Bonus
    public decimal Amount { get; set; }
    public string PaymentMethod { get; set; } = "Cash"; // Cash, UPI, Bank Transfer, Cheque
    public string ReferenceNumber { get; set; } = string.Empty;
    public string Notes { get; set; } = string.Empty;
    public string RecordedBy { get; set; } = string.Empty;
}


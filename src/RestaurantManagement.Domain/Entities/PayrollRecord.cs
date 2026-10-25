namespace RestaurantManagement.Domain.Entities;

public class PayrollRecord : BaseEntity
{
    public int EmployeeId { get; set; }
    public Employee? Employee { get; set; }
    public int Month { get; set; } // 1 - 12
    public int Year { get; set; }
    public decimal BaseSalary { get; set; }
    public int TotalDaysInMonth { get; set; } = 30;
    public decimal PresentDays { get; set; }
    public decimal HalfDays { get; set; }
    public decimal PaidLeaveDays { get; set; }
    public decimal UnpaidLeaveDays { get; set; }
    public decimal DailyRate { get; set; }
    public decimal GrossSalary { get; set; }
    public decimal AdvanceDeducted { get; set; }
    public decimal OtherDeductions { get; set; }
    public decimal Bonus { get; set; }
    public decimal NetSalary { get; set; }
    public string Status { get; set; } = "Paid"; // Draft, Pending, Paid
    public DateTime? PaymentDate { get; set; }
    public string PaymentMethod { get; set; } = "Cash";
    public string Notes { get; set; } = string.Empty;
    public string ProcessedBy { get; set; } = string.Empty;
}

using RestaurantManagement.Domain.Enums;

namespace RestaurantManagement.Domain.Entities;

public class Employee : BaseEntity
{
    public string Name { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string Phone { get; set; } = string.Empty;
    public UserRole Role { get; set; } = UserRole.Waiter;
    public DateTime JoiningDate { get; set; } = DateTime.UtcNow;
    public decimal BaseSalary { get; set; }
    public string Shift { get; set; } = "Morning"; // Morning, Evening, Night
    
    public ICollection<Attendance> Attendances { get; set; } = new List<Attendance>();
    public ICollection<EmployeeLedger> Ledgers { get; set; } = new List<EmployeeLedger>();
    public ICollection<PayrollRecord> PayrollRecords { get; set; } = new List<PayrollRecord>();
    public ICollection<LeaveRequest> LeaveRequests { get; set; } = new List<LeaveRequest>();
}

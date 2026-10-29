using Microsoft.EntityFrameworkCore;
using RestaurantManagement.Domain.Entities;

namespace RestaurantManagement.Infrastructure.DbContext;

public class RestaurantDbContext : Microsoft.EntityFrameworkCore.DbContext
{
    public RestaurantDbContext(DbContextOptions<RestaurantDbContext> options) : base(options)
    {
    }

    public DbSet<User> Users => Set<User>();
    public DbSet<Floor> Floors => Set<Floor>();
    public DbSet<Table> Tables => Set<Table>();
    public DbSet<Category> Categories => Set<Category>();
    public DbSet<MenuItem> MenuItems => Set<MenuItem>();
    public DbSet<MenuItemVariant> MenuItemVariants => Set<MenuItemVariant>();
    public DbSet<AddOn> AddOns => Set<AddOn>();
    public DbSet<MenuItemAddOn> MenuItemAddOns => Set<MenuItemAddOn>();
    public DbSet<Order> Orders => Set<Order>();
    public DbSet<OrderItem> OrderItems => Set<OrderItem>();
    public DbSet<OrderItemAddOn> OrderItemAddOns => Set<OrderItemAddOn>();
    public DbSet<KOT> KOTs => Set<KOT>();
    public DbSet<KOTItem> KOTItems => Set<KOTItem>();
    public DbSet<Bill> Bills => Set<Bill>();
    public DbSet<Payment> Payments => Set<Payment>();
    public DbSet<Customer> Customers => Set<Customer>();
    public DbSet<LoyaltyAccount> LoyaltyAccounts => Set<LoyaltyAccount>();
    public DbSet<LoyaltyTransaction> LoyaltyTransactions => Set<LoyaltyTransaction>();
    public DbSet<Reservation> Reservations => Set<Reservation>();
    public DbSet<InventoryItem> InventoryItems => Set<InventoryItem>();
    public DbSet<StockTransaction> StockTransactions => Set<StockTransaction>();
    public DbSet<Recipe> Recipes => Set<Recipe>();
    public DbSet<RecipeItem> RecipeItems => Set<RecipeItem>();
    public DbSet<Supplier> Suppliers => Set<Supplier>();
    public DbSet<PurchaseOrder> PurchaseOrders => Set<PurchaseOrder>();
    public DbSet<PurchaseOrderItem> PurchaseOrderItems => Set<PurchaseOrderItem>();
    public DbSet<Employee> Employees => Set<Employee>();
    public DbSet<Attendance> Attendances => Set<Attendance>();
    public DbSet<Expense> Expenses => Set<Expense>();
    public DbSet<AuditLog> AuditLogs => Set<AuditLog>();
    public DbSet<RestaurantSetting> RestaurantSettings => Set<RestaurantSetting>();
    public DbSet<EmployeeLedger> EmployeeLedgers => Set<EmployeeLedger>();
    public DbSet<PayrollRecord> PayrollRecords => Set<PayrollRecord>();
    public DbSet<CustomerLedger> CustomerLedgers => Set<CustomerLedger>();
    public DbSet<LeaveRequest> LeaveRequests => Set<LeaveRequest>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // Junction entity setup
        modelBuilder.Entity<MenuItemAddOn>()
            .HasKey(ma => new { ma.MenuItemId, ma.AddOnId });

        modelBuilder.Entity<MenuItemAddOn>()
            .HasOne(ma => ma.MenuItem)
            .WithMany(m => m.MenuItemAddOns)
            .HasForeignKey(ma => ma.MenuItemId);

        modelBuilder.Entity<MenuItemAddOn>()
            .HasOne(ma => ma.AddOn)
            .WithMany(a => a.MenuItemAddOns)
            .HasForeignKey(ma => ma.AddOnId);

        // Precision configurations for decimal properties
        foreach (var property in modelBuilder.Model.GetEntityTypes()
                     .SelectMany(t => t.GetProperties())
                     .Where(p => p.ClrType == typeof(decimal) || p.ClrType == typeof(decimal?)))
        {
            property.SetPrecision(18);
            property.SetScale(2);
        }

        // Table foreign key behavior
        modelBuilder.Entity<Table>()
            .HasOne(t => t.Floor)
            .WithMany(f => f.Tables)
            .HasForeignKey(t => t.FloorId)
            .OnDelete(DeleteBehavior.Restrict);

        modelBuilder.Entity<Order>()
            .HasOne(o => o.Table)
            .WithMany()
            .HasForeignKey(o => o.TableId)
            .OnDelete(DeleteBehavior.SetNull);

        modelBuilder.Entity<Table>()
            .HasOne(t => t.CurrentOrder)
            .WithMany()
            .HasForeignKey(t => t.CurrentOrderId)
            .OnDelete(DeleteBehavior.SetNull);

        modelBuilder.Entity<Bill>()
            .HasOne(b => b.Order)
            .WithOne(o => o.Bill)
            .HasForeignKey<Bill>(b => b.OrderId)
            .OnDelete(DeleteBehavior.Restrict);

        modelBuilder.Entity<EmployeeLedger>()
            .HasOne(el => el.Employee)
            .WithMany(e => e.Ledgers)
            .HasForeignKey(el => el.EmployeeId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<PayrollRecord>()
            .HasOne(pr => pr.Employee)
            .WithMany(e => e.PayrollRecords)
            .HasForeignKey(pr => pr.EmployeeId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<CustomerLedger>()
            .HasOne(cl => cl.Customer)
            .WithMany(c => c.Ledgers)
            .HasForeignKey(cl => cl.CustomerId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<LeaveRequest>()
            .HasOne(lr => lr.Employee)
            .WithMany(e => e.LeaveRequests)
            .HasForeignKey(lr => lr.EmployeeId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}

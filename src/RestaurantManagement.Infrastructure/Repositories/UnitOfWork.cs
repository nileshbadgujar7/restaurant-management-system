using RestaurantManagement.Application.Interfaces;
using RestaurantManagement.Domain.Entities;
using RestaurantManagement.Infrastructure.DbContext;

namespace RestaurantManagement.Infrastructure.Repositories;

public class UnitOfWork : IUnitOfWork
{
    private readonly RestaurantDbContext _context;

    public UnitOfWork(RestaurantDbContext context)
    {
        _context = context;

        Users = new GenericRepository<User>(_context);
        Floors = new GenericRepository<Floor>(_context);
        Tables = new GenericRepository<Table>(_context);
        Categories = new GenericRepository<Category>(_context);
        MenuItems = new GenericRepository<MenuItem>(_context);
        MenuItemVariants = new GenericRepository<MenuItemVariant>(_context);
        AddOns = new GenericRepository<AddOn>(_context);
        Orders = new GenericRepository<Order>(_context);
        OrderItems = new GenericRepository<OrderItem>(_context);
        KOTs = new GenericRepository<KOT>(_context);
        KOTItems = new GenericRepository<KOTItem>(_context);
        Bills = new GenericRepository<Bill>(_context);
        Payments = new GenericRepository<Payment>(_context);
        Customers = new GenericRepository<Customer>(_context);
        LoyaltyAccounts = new GenericRepository<LoyaltyAccount>(_context);
        LoyaltyTransactions = new GenericRepository<LoyaltyTransaction>(_context);
        Reservations = new GenericRepository<Reservation>(_context);
        InventoryItems = new GenericRepository<InventoryItem>(_context);
        StockTransactions = new GenericRepository<StockTransaction>(_context);
        Recipes = new GenericRepository<Recipe>(_context);
        RecipeItems = new GenericRepository<RecipeItem>(_context);
        Suppliers = new GenericRepository<Supplier>(_context);
        PurchaseOrders = new GenericRepository<PurchaseOrder>(_context);
        Employees = new GenericRepository<Employee>(_context);
        Attendances = new GenericRepository<Attendance>(_context);
        Expenses = new GenericRepository<Expense>(_context);
        AuditLogs = new GenericRepository<AuditLog>(_context);
        RestaurantSettings = new GenericRepository<RestaurantSetting>(_context);
        EmployeeLedgers = new GenericRepository<EmployeeLedger>(_context);
        PayrollRecords = new GenericRepository<PayrollRecord>(_context);
    }

    public IRepository<User> Users { get; }
    public IRepository<Floor> Floors { get; }
    public IRepository<Table> Tables { get; }
    public IRepository<Category> Categories { get; }
    public IRepository<MenuItem> MenuItems { get; }
    public IRepository<MenuItemVariant> MenuItemVariants { get; }
    public IRepository<AddOn> AddOns { get; }
    public IRepository<Order> Orders { get; }
    public IRepository<OrderItem> OrderItems { get; }
    public IRepository<KOT> KOTs { get; }
    public IRepository<KOTItem> KOTItems { get; }
    public IRepository<Bill> Bills { get; }
    public IRepository<Payment> Payments { get; }
    public IRepository<Customer> Customers { get; }
    public IRepository<LoyaltyAccount> LoyaltyAccounts { get; }
    public IRepository<LoyaltyTransaction> LoyaltyTransactions { get; }
    public IRepository<Reservation> Reservations { get; }
    public IRepository<InventoryItem> InventoryItems { get; }
    public IRepository<StockTransaction> StockTransactions { get; }
    public IRepository<Recipe> Recipes { get; }
    public IRepository<RecipeItem> RecipeItems { get; }
    public IRepository<Supplier> Suppliers { get; }
    public IRepository<PurchaseOrder> PurchaseOrders { get; }
    public IRepository<Employee> Employees { get; }
    public IRepository<Attendance> Attendances { get; }
    public IRepository<Expense> Expenses { get; }
    public IRepository<AuditLog> AuditLogs { get; }
    public IRepository<RestaurantSetting> RestaurantSettings { get; }
    public IRepository<EmployeeLedger> EmployeeLedgers { get; }
    public IRepository<PayrollRecord> PayrollRecords { get; }

    public async Task<int> SaveChangesAsync()
    {
        return await _context.SaveChangesAsync();
    }

    public void Dispose()
    {
        _context.Dispose();
    }
}

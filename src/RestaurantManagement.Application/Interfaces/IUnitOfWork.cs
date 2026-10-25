using System.Linq.Expressions;
using RestaurantManagement.Domain.Entities;

namespace RestaurantManagement.Application.Interfaces;

public interface IRepository<T> where T : BaseEntity
{
    Task<T?> GetByIdAsync(int id);
    Task<IEnumerable<T>> GetAllAsync();
    Task<IEnumerable<T>> FindAsync(Expression<Func<T, bool>> predicate);
    Task<T> AddAsync(T entity);
    Task UpdateAsync(T entity);
    Task DeleteAsync(T entity);
    Task<int> CountAsync(Expression<Func<T, bool>>? predicate = null);
    IQueryable<T> Query();
}

public interface IUnitOfWork : IDisposable
{
    IRepository<User> Users { get; }
    IRepository<Floor> Floors { get; }
    IRepository<Table> Tables { get; }
    IRepository<Category> Categories { get; }
    IRepository<MenuItem> MenuItems { get; }
    IRepository<MenuItemVariant> MenuItemVariants { get; }
    IRepository<AddOn> AddOns { get; }
    IRepository<Order> Orders { get; }
    IRepository<OrderItem> OrderItems { get; }
    IRepository<KOT> KOTs { get; }
    IRepository<KOTItem> KOTItems { get; }
    IRepository<Bill> Bills { get; }
    IRepository<Payment> Payments { get; }
    IRepository<Customer> Customers { get; }
    IRepository<LoyaltyAccount> LoyaltyAccounts { get; }
    IRepository<LoyaltyTransaction> LoyaltyTransactions { get; }
    IRepository<Reservation> Reservations { get; }
    IRepository<InventoryItem> InventoryItems { get; }
    IRepository<StockTransaction> StockTransactions { get; }
    IRepository<Recipe> Recipes { get; }
    IRepository<RecipeItem> RecipeItems { get; }
    IRepository<Supplier> Suppliers { get; }
    IRepository<PurchaseOrder> PurchaseOrders { get; }
    IRepository<Employee> Employees { get; }
    IRepository<Attendance> Attendances { get; }
    IRepository<Expense> Expenses { get; }
    IRepository<AuditLog> AuditLogs { get; }
    IRepository<RestaurantSetting> RestaurantSettings { get; }
    IRepository<EmployeeLedger> EmployeeLedgers { get; }
    IRepository<PayrollRecord> PayrollRecords { get; }

    Task<int> SaveChangesAsync();
}

using Microsoft.EntityFrameworkCore;
using RestaurantManagement.Domain.Entities;
using RestaurantManagement.Domain.Enums;
using RestaurantManagement.Infrastructure.DbContext;

namespace RestaurantManagement.Infrastructure.Data;

public static class DbInitializer
{
    public static void SeedData(RestaurantDbContext context)
    {
        // Ensure new tables exist in SQL Server if migrating incrementally
        try
        {
            context.Database.ExecuteSqlRaw(@"
                IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'EmployeeLedgers')
                BEGIN
                    CREATE TABLE [EmployeeLedgers] (
                        [Id] INT IDENTITY(1,1) NOT NULL PRIMARY KEY,
                        [EmployeeId] INT NOT NULL,
                        [TransactionDate] DATETIME2 NOT NULL,
                        [TransactionType] NVARCHAR(50) NOT NULL,
                        [Amount] DECIMAL(18,2) NOT NULL,
                        [PaymentMethod] NVARCHAR(50) NOT NULL,
                        [ReferenceNumber] NVARCHAR(100) NOT NULL,
                        [Notes] NVARCHAR(MAX) NOT NULL,
                        [RecordedBy] NVARCHAR(100) NOT NULL,
                        [CreatedAt] DATETIME2 NOT NULL,
                        [UpdatedAt] DATETIME2 NULL,
                        [IsDeleted] BIT NOT NULL
                    );
                END;

                IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'PayrollRecords')
                BEGIN
                    CREATE TABLE [PayrollRecords] (
                        [Id] INT IDENTITY(1,1) NOT NULL PRIMARY KEY,
                        [EmployeeId] INT NOT NULL,
                        [Month] INT NOT NULL,
                        [Year] INT NOT NULL,
                        [BaseSalary] DECIMAL(18,2) NOT NULL,
                        [TotalDaysInMonth] INT NOT NULL,
                        [PresentDays] DECIMAL(18,2) NOT NULL,
                        [HalfDays] DECIMAL(18,2) NOT NULL,
                        [PaidLeaveDays] DECIMAL(18,2) NOT NULL,
                        [UnpaidLeaveDays] DECIMAL(18,2) NOT NULL,
                        [DailyRate] DECIMAL(18,2) NOT NULL,
                        [GrossSalary] DECIMAL(18,2) NOT NULL,
                        [AdvanceDeducted] DECIMAL(18,2) NOT NULL,
                        [OtherDeductions] DECIMAL(18,2) NOT NULL,
                        [Bonus] DECIMAL(18,2) NOT NULL,
                        [NetSalary] DECIMAL(18,2) NOT NULL,
                        [Status] NVARCHAR(50) NOT NULL,
                        [PaymentDate] DATETIME2 NULL,
                        [PaymentMethod] NVARCHAR(50) NOT NULL,
                        [Notes] NVARCHAR(MAX) NOT NULL,
                        [ProcessedBy] NVARCHAR(100) NOT NULL,
                        [CreatedAt] DATETIME2 NOT NULL,
                        [UpdatedAt] DATETIME2 NULL,
                        [IsDeleted] BIT NOT NULL
                    );
                END;
            ");
        }
        catch
        {
            // Ignore in-memory testing or provider limitations
        }

        // Migrations are applied in Program.cs via dbContext.Database.Migrate()
        if (context.Users.Any())
        {
            SeedSampleLedgersAndAttendance(context);
            return; // Database base data has already been seeded
        }

        // 1. Seed Restaurant Settings
        context.RestaurantSettings.Add(new RestaurantSetting
        {
            RestaurantName = "Girana Garden Fine Dine & Bar",
            Address = "101 Grand Galleria, MG Road, Ahmedabad - 380001",
            Phone = "+91 98765 43210",
            Email = "contact@giranagarden.com",
            GSTIN = "24AAACG1234F1Z9",
            CurrencySymbol = "₹",
            CGSTPercentage = 2.5m,
            SGSTPercentage = 2.5m,
            ServiceChargePercentage = 0m,
            InvoicePrefix = "INV"
        });

        // 2. Seed Users for each role
        var admin = new User { Username = "admin", PasswordHash = "admin123", FullName = "System Administrator", Email = "admin@giranagarden.com", Phone = "9876500001", Role = UserRole.Admin };
        var manager = new User { Username = "manager", PasswordHash = "manager123", FullName = "Rajesh Verma (Manager)", Email = "manager@giranagarden.com", Phone = "9876500002", Role = UserRole.Manager };
        var cashier = new User { Username = "cashier", PasswordHash = "cashier123", FullName = "Suresh Patel (Cashier)", Email = "cashier@giranagarden.com", Phone = "9876500003", Role = UserRole.Cashier };
        var waiter = new User { Username = "waiter", PasswordHash = "waiter123", FullName = "Amit Kumar (Waiter)", Email = "waiter@giranagarden.com", Phone = "9876500004", Role = UserRole.Waiter };
        var kitchen = new User { Username = "kitchen", PasswordHash = "kitchen123", FullName = "Chef Sunita (Kitchen Head)", Email = "kitchen@giranagarden.com", Phone = "9876500005", Role = UserRole.KitchenStaff };
        var inventoryUser = new User { Username = "inventory", PasswordHash = "inventory123", FullName = "Vikram Singh (Inventory)", Email = "inventory@giranagarden.com", Phone = "9876500006", Role = UserRole.InventoryStaff };

        context.Users.AddRange(admin, manager, cashier, waiter, kitchen, inventoryUser);
        context.SaveChanges();

        // 3. Seed Floors & Tables
        var groundFloor = new Floor { Name = "Ground Floor", Description = "Main Dining Area", DisplayOrder = 1 };
        var firstFloor = new Floor { Name = "First Floor", Description = "AC Lounge & Family Area", DisplayOrder = 2 };
        var outdoor = new Floor { Name = "Outdoor Patio", Description = "Open Garden Seating", DisplayOrder = 3 };

        context.Floors.AddRange(groundFloor, firstFloor, outdoor);
        context.SaveChanges();

        var t1 = new Table { TableNumber = "01", Capacity = 2, FloorId = groundFloor.Id, Status = TableStatus.Available };
        var t2 = new Table { TableNumber = "02", Capacity = 4, FloorId = groundFloor.Id, Status = TableStatus.Available };
        var t3 = new Table { TableNumber = "03", Capacity = 4, FloorId = groundFloor.Id, Status = TableStatus.Available };
        var t4 = new Table { TableNumber = "04", Capacity = 6, FloorId = groundFloor.Id, Status = TableStatus.Available };
        var t5 = new Table { TableNumber = "05", Capacity = 4, FloorId = groundFloor.Id, Status = TableStatus.Available };
        
        var t11 = new Table { TableNumber = "11", Capacity = 4, FloorId = firstFloor.Id, Status = TableStatus.Available };
        var t12 = new Table { TableNumber = "12", Capacity = 6, FloorId = firstFloor.Id, Status = TableStatus.Available };
        
        var t21 = new Table { TableNumber = "21", Capacity = 2, FloorId = outdoor.Id, Status = TableStatus.Available };
        var t22 = new Table { TableNumber = "22", Capacity = 4, FloorId = outdoor.Id, Status = TableStatus.Available };

        context.Tables.AddRange(t1, t2, t3, t4, t5, t11, t12, t21, t22);
        context.SaveChanges();

        // 4. Seed Categories & Menu Items
        var starters = new Category { Name = "Starters", Description = "Delicious appetizers and quick bites", DisplayOrder = 1, ImageUrl = "https://images.unsplash.com/photo-1541544741938-0af808871cc0?w=500" };
        var mainCourse = new Category { Name = "Main Course", Description = "Authentic Indian Curries & Gravies", DisplayOrder = 2, ImageUrl = "https://images.unsplash.com/photo-1585937421612-70a008356fbe?w=500" };
        var biryani = new Category { Name = "Biryani & Rice", Description = "Aromatic Hyderabadi & Dum Biryanis", DisplayOrder = 3, ImageUrl = "https://images.unsplash.com/photo-1563379091339-03b21ab4a4f8?w=500" };
        var beverages = new Category { Name = "Beverages", Description = "Refreshing Mocktails & Shakes", DisplayOrder = 4, ImageUrl = "https://images.unsplash.com/photo-1513558161293-cdaf765ed2fd?w=500" };
        var desserts = new Category { Name = "Desserts", Description = "Sweet indulgences", DisplayOrder = 5, ImageUrl = "https://images.unsplash.com/photo-1551024709-8f23befc6f87?w=500" };

        context.Categories.AddRange(starters, mainCourse, biryani, beverages, desserts);
        context.SaveChanges();

        var paneerTikka = new MenuItem { CategoryId = starters.Id, Name = "Paneer Tikka", Description = "Cottage cheese marinated in yogurt and spices", Price = 250m, IsVeg = true, IsAvailable = true, PreparationTimeMinutes = 15 };
        var dalMakhani = new MenuItem { CategoryId = mainCourse.Id, Name = "Dal Makhani", Description = "Slow cooked black lentils with cream and butter", Price = 250m, IsVeg = true, IsAvailable = true, PreparationTimeMinutes = 20 };
        var butterNaan = new MenuItem { CategoryId = mainCourse.Id, Name = "Butter Naan", Description = "Freshly baked clay oven bread with butter", Price = 50m, IsVeg = true, IsAvailable = true, PreparationTimeMinutes = 8 };
        var chickenBiryani = new MenuItem { CategoryId = biryani.Id, Name = "Chicken Biryani", Description = "Flavorful Basmati rice cooked with tender chicken", Price = 320m, IsVeg = false, IsAvailable = true, PreparationTimeMinutes = 25 };
        var coldDrink = new MenuItem { CategoryId = beverages.Id, Name = "Cold Drink / Soda", Description = "Chilled carbonated beverage 300ml", Price = 60m, IsVeg = true, IsAvailable = true, PreparationTimeMinutes = 2 };
        var coldCoffee = new MenuItem { CategoryId = beverages.Id, Name = "Cold Coffee with Ice Cream", Description = "Rich blended espresso with vanilla scoop", Price = 140m, IsVeg = true, IsAvailable = true, PreparationTimeMinutes = 10 };
        var gulabJamun = new MenuItem { CategoryId = desserts.Id, Name = "Gulab Jamun (2 Pcs)", Description = "Warm milk dumplings in rose flavored sugar syrup", Price = 90m, IsVeg = true, IsAvailable = true, PreparationTimeMinutes = 5 };

        context.MenuItems.AddRange(paneerTikka, dalMakhani, butterNaan, chickenBiryani, coldDrink, coldCoffee, gulabJamun);
        context.SaveChanges();

        // 5. Seed Inventory Items (Initial healthy stock for operations)
        var paneerStock = new InventoryItem { Name = "Paneer", Category = "Dairy", Unit = "KG", CurrentStock = 25.0m, MinimumStock = 10.0m, UnitCost = 350m, ReorderLevel = 12m };
        var tomatoStock = new InventoryItem { Name = "Tomato", Category = "Vegetables", Unit = "KG", CurrentStock = 20.0m, MinimumStock = 10.0m, UnitCost = 40m, ReorderLevel = 15m };
        var oilStock = new InventoryItem { Name = "Cooking Oil", Category = "Grocery", Unit = "L", CurrentStock = 20.0m, MinimumStock = 10.0m, UnitCost = 140m, ReorderLevel = 12m };
        var riceStock = new InventoryItem { Name = "Basmati Rice", Category = "Grocery", Unit = "KG", CurrentStock = 50.0m, MinimumStock = 20.0m, UnitCost = 110m, ReorderLevel = 25m };

        context.InventoryItems.AddRange(paneerStock, tomatoStock, oilStock, riceStock);
        context.SaveChanges();

        // 6. Seed Staff Roster Master Data
        context.Employees.AddRange(
            new Employee { Name = "Rajesh Verma", Email = "rajesh.manager@giranagarden.com", Phone = "9876500002", Role = UserRole.Manager, BaseSalary = 45000m, Shift = "General", IsActive = true },
            new Employee { Name = "Chef Sanjeev Kapoor", Email = "cook.head@giranagarden.com", Phone = "9876500010", Role = UserRole.Cook, BaseSalary = 38000m, Shift = "Morning", IsActive = true },
            new Employee { Name = "Sunita Sharma", Email = "sunita@giranagarden.com", Phone = "9876500005", Role = UserRole.Cook, BaseSalary = 28000m, Shift = "Evening", IsActive = true },
            new Employee { Name = "Amit Kumar", Email = "amit@giranagarden.com", Phone = "9876500004", Role = UserRole.Waiter, BaseSalary = 18000m, Shift = "Morning", IsActive = true },
            new Employee { Name = "Ramesh Rathore", Email = "ramesh@giranagarden.com", Phone = "9876500011", Role = UserRole.Waiter, BaseSalary = 18000m, Shift = "Evening", IsActive = true },
            new Employee { Name = "Suresh Patel", Email = "suresh.cashier@giranagarden.com", Phone = "9876500003", Role = UserRole.Cashier, BaseSalary = 22000m, Shift = "Morning", IsActive = true },
            new Employee { Name = "Kishore Solanki", Email = "kishore.hk@giranagarden.com", Phone = "9876500012", Role = UserRole.Housekeeping, BaseSalary = 15000m, Shift = "Morning", IsActive = true },
            new Employee { Name = "Pooja Ben", Email = "pooja.hk@giranagarden.com", Phone = "9876500013", Role = UserRole.Housekeeping, BaseSalary = 15000m, Shift = "Evening", IsActive = true }
        );

        context.SaveChanges();
        SeedSampleLedgersAndAttendance(context);
    }

    private static void SeedSampleLedgersAndAttendance(RestaurantDbContext context)
    {
        try
        {
            var employees = context.Employees.Where(e => !e.IsDeleted).ToList();
            if (!employees.Any()) return;

            // Seed Sample Advances & Repayments if none exist
            if (!context.EmployeeLedgers.Any())
            {
                var amit = employees.FirstOrDefault(e => e.Name.Contains("Amit"));
                var ramesh = employees.FirstOrDefault(e => e.Name.Contains("Ramesh"));
                var suresh = employees.FirstOrDefault(e => e.Name.Contains("Suresh"));
                var sanjeev = employees.FirstOrDefault(e => e.Name.Contains("Sanjeev"));

                if (amit != null)
                {
                    context.EmployeeLedgers.Add(new EmployeeLedger
                    {
                        EmployeeId = amit.Id,
                        TransactionDate = DateTime.UtcNow.AddDays(-15),
                        TransactionType = "Advance",
                        Amount = 4000m,
                        PaymentMethod = "Cash",
                        ReferenceNumber = "ADV-001",
                        Notes = "Festival advance requested by employee",
                        RecordedBy = "Admin",
                        CreatedAt = DateTime.UtcNow.AddDays(-15)
                    });
                    context.EmployeeLedgers.Add(new EmployeeLedger
                    {
                        EmployeeId = amit.Id,
                        TransactionDate = DateTime.UtcNow.AddDays(-5),
                        TransactionType = "Repayment",
                        Amount = 1000m,
                        PaymentMethod = "Cash",
                        ReferenceNumber = "REP-001",
                        Notes = "Cash repayment from tip savings",
                        RecordedBy = "Cashier",
                        CreatedAt = DateTime.UtcNow.AddDays(-5)
                    });
                }

                if (ramesh != null)
                {
                    context.EmployeeLedgers.Add(new EmployeeLedger
                    {
                        EmployeeId = ramesh.Id,
                        TransactionDate = DateTime.UtcNow.AddDays(-10),
                        TransactionType = "Advance",
                        Amount = 3000m,
                        PaymentMethod = "UPI",
                        ReferenceNumber = "UPI-ADV-789",
                        Notes = "Family emergency advance",
                        RecordedBy = "Admin",
                        CreatedAt = DateTime.UtcNow.AddDays(-10)
                    });
                }

                if (suresh != null)
                {
                    context.EmployeeLedgers.Add(new EmployeeLedger
                    {
                        EmployeeId = suresh.Id,
                        TransactionDate = DateTime.UtcNow.AddDays(-20),
                        TransactionType = "Advance",
                        Amount = 5000m,
                        PaymentMethod = "Bank Transfer",
                        ReferenceNumber = "NEFT-9912",
                        Notes = "House rent advance",
                        RecordedBy = "Manager",
                        CreatedAt = DateTime.UtcNow.AddDays(-20)
                    });
                }

                if (sanjeev != null)
                {
                    context.EmployeeLedgers.Add(new EmployeeLedger
                    {
                        EmployeeId = sanjeev.Id,
                        TransactionDate = DateTime.UtcNow.AddDays(-12),
                        TransactionType = "Advance",
                        Amount = 8000m,
                        PaymentMethod = "Bank Transfer",
                        ReferenceNumber = "NEFT-8841",
                        Notes = "Advance against monthly salary",
                        RecordedBy = "Admin",
                        CreatedAt = DateTime.UtcNow.AddDays(-12)
                    });
                }

                context.SaveChanges();
            }

            // Seed Sample Attendance for past 25 days of current month if employee attendances are sparse
            var startOfMonth = new DateTime(DateTime.UtcNow.Year, DateTime.UtcNow.Month, 1);
            int todayDay = DateTime.UtcNow.Day;
            if (context.Attendances.Count(a => a.Date >= startOfMonth) < 10)
            {
                var rand = new Random(42);
                foreach (var emp in employees)
                {
                    for (int day = 1; day < todayDay; day++)
                    {
                        var attDate = startOfMonth.AddDays(day - 1);
                        if (attDate.DayOfWeek == DayOfWeek.Sunday) continue; // Restaurant weekly off or rotation

                        // Check if already exists
                        if (context.Attendances.Any(a => a.EmployeeId == emp.Id && a.Date == attDate)) continue;

                        int roll = rand.Next(100);
                        string status;
                        if (roll < 80) status = "Present";
                        else if (roll < 90) status = "Half day";
                        else if (roll < 95) status = "Leave";
                        else status = "Absent";

                        context.Attendances.Add(new Attendance
                        {
                            EmployeeId = emp.Id,
                            Date = attDate,
                            CheckInTime = attDate.AddHours(9).AddMinutes(rand.Next(0, 30)),
                            CheckOutTime = status == "Half day" ? attDate.AddHours(14) : attDate.AddHours(19),
                            Status = status
                        });
                    }
                }
                context.SaveChanges();
            }
        }
        catch
        {
            // Non-critical seed fallback
        }
    }
}

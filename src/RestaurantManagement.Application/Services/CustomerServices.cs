using RestaurantManagement.Application.DTOs;
using RestaurantManagement.Application.Interfaces;
using RestaurantManagement.Domain.Entities;
using RestaurantManagement.Domain.Enums;

namespace RestaurantManagement.Application.Services;

public class CustomerService : ICustomerService
{
    private readonly IUnitOfWork _unitOfWork;

    public CustomerService(IUnitOfWork unitOfWork)
    {
        _unitOfWork = unitOfWork;
    }

    public async Task<IEnumerable<CustomerDto>> GetAllCustomersAsync()
    {
        var customers = await _unitOfWork.Customers.GetAllAsync();
        return customers.Where(c => !c.IsDeleted).Select(MapToCustomerDto);
    }

    public async Task<CustomerDto?> GetCustomerByIdAsync(int id)
    {
        var customer = await _unitOfWork.Customers.GetByIdAsync(id);
        return customer != null && !customer.IsDeleted ? MapToCustomerDto(customer) : null;
    }

    public async Task<CustomerDto?> GetCustomerByPhoneAsync(string phone)
    {
        var customers = await _unitOfWork.Customers.FindAsync(c => c.Phone == phone && !c.IsDeleted);
        var customer = customers.FirstOrDefault();
        return customer != null ? MapToCustomerDto(customer) : null;
    }

    public async Task<CustomerDto> CreateOrUpdateCustomerAsync(CustomerCreateUpdateDto dto)
    {
        var existing = (await _unitOfWork.Customers.FindAsync(c => c.Phone == dto.Phone && !c.IsDeleted)).FirstOrDefault();

        if (existing == null)
        {
            var customer = new Customer
            {
                Name = dto.Name,
                Phone = dto.Phone,
                Email = dto.Email,
                DateOfBirth = dto.DateOfBirth,
                Address = dto.Address,
                Tier = LoyaltyTier.Silver,
                CreatedAt = DateTime.UtcNow
            };

            await _unitOfWork.Customers.AddAsync(customer);
            await _unitOfWork.SaveChangesAsync();

            var account = new LoyaltyAccount
            {
                CustomerId = customer.Id,
                CurrentPoints = 0,
                TotalEarnedPoints = 0,
                Tier = LoyaltyTier.Silver
            };
            await _unitOfWork.LoyaltyAccounts.AddAsync(account);
            await _unitOfWork.SaveChangesAsync();

            return MapToCustomerDto(customer);
        }
        else
        {
            existing.Name = dto.Name;
            existing.Email = dto.Email;
            existing.DateOfBirth = dto.DateOfBirth;
            existing.Address = dto.Address;
            existing.UpdatedAt = DateTime.UtcNow;

            await _unitOfWork.Customers.UpdateAsync(existing);
            await _unitOfWork.SaveChangesAsync();

            return MapToCustomerDto(existing);
        }
    }

    public async Task<int> AwardLoyaltyPointsAsync(int customerId, decimal orderAmount, string orderNo)
    {
        var customer = await _unitOfWork.Customers.GetByIdAsync(customerId);
        if (customer == null) return 0;

        // Rule: 1 point earned per ₹100 spent
        int earned = (int)(orderAmount / 100m);
        if (earned <= 0) return 0;

        customer.TotalSpent += orderAmount;
        customer.TotalVisits += 1;
        customer.LoyaltyPoints += earned;
        customer.LastVisitDate = DateTime.UtcNow;

        // Tier progression rule
        if (customer.TotalSpent >= 50000m) customer.Tier = LoyaltyTier.Platinum;
        else if (customer.TotalSpent >= 20000m) customer.Tier = LoyaltyTier.Gold;
        else customer.Tier = LoyaltyTier.Silver;

        await _unitOfWork.Customers.UpdateAsync(customer);

        var account = (await _unitOfWork.LoyaltyAccounts.FindAsync(a => a.CustomerId == customerId)).FirstOrDefault();
        if (account != null)
        {
            account.CurrentPoints += earned;
            account.TotalEarnedPoints += earned;
            account.Tier = customer.Tier;
            await _unitOfWork.LoyaltyAccounts.UpdateAsync(account);

            var txn = new LoyaltyTransaction
            {
                LoyaltyAccountId = account.Id,
                Points = earned,
                TransactionType = "EARNED",
                ReferenceOrderNo = orderNo,
                Description = $"Earned {earned} points on bill payment ₹{orderAmount}"
            };
            await _unitOfWork.LoyaltyTransactions.AddAsync(txn);
        }

        await _unitOfWork.SaveChangesAsync();
        return earned;
    }

    private static CustomerDto MapToCustomerDto(Customer c)
    {
        return new CustomerDto
        {
            Id = c.Id,
            Name = c.Name,
            Phone = c.Phone,
            Email = c.Email,
            DateOfBirth = c.DateOfBirth,
            Address = c.Address,
            TotalVisits = c.TotalVisits,
            TotalSpent = c.TotalSpent,
            LoyaltyPoints = c.LoyaltyPoints,
            Tier = c.Tier,
            LastVisitDate = c.LastVisitDate,
            OutstandingCredit = c.OutstandingCredit
        };
    }
}

public class ReservationService : IReservationService
{
    private readonly IUnitOfWork _unitOfWork;

    public ReservationService(IUnitOfWork unitOfWork)
    {
        _unitOfWork = unitOfWork;
    }

    public async Task<IEnumerable<ReservationDto>> GetReservationsAsync(DateTime? date = null, ReservationStatus? status = null)
    {
        var reservations = await _unitOfWork.Reservations.GetAllAsync();
        var tables = (await _unitOfWork.Tables.GetAllAsync()).ToDictionary(t => t.Id);

        var query = reservations.Where(r => !r.IsDeleted);

        if (date.HasValue) query = query.Where(r => r.ReservationDate.Date == date.Value.Date);
        if (status.HasValue) query = query.Where(r => r.Status == status.Value);

        return query.OrderByDescending(r => r.ReservationDate).Select(r => new ReservationDto
        {
            Id = r.Id,
            CustomerName = r.CustomerName,
            CustomerPhone = r.CustomerPhone,
            ReservationDate = r.ReservationDate,
            GuestCount = r.GuestCount,
            TableId = r.TableId,
            TableNumber = r.TableId.HasValue && tables.TryGetValue(r.TableId.Value, out var t) ? t.TableNumber : null,
            Status = r.Status,
            SpecialRequests = r.SpecialRequests
        });
    }

    public async Task<ReservationDto> CreateReservationAsync(CreateReservationDto dto)
    {
        // Business Rule: Check for overlapping reservation on same table at same time slot (+/- 2 hours)
        if (dto.TableId.HasValue)
        {
            var existing = await _unitOfWork.Reservations.FindAsync(r => r.TableId == dto.TableId.Value && r.Status == ReservationStatus.Confirmed && Math.Abs((r.ReservationDate - dto.ReservationDate).TotalHours) < 2.0);
            if (existing.Any())
            {
                throw new InvalidOperationException($"Table is already reserved for that time slot.");
            }
        }

        var reservation = new Reservation
        {
            CustomerName = dto.CustomerName,
            CustomerPhone = dto.CustomerPhone,
            ReservationDate = dto.ReservationDate,
            GuestCount = dto.GuestCount,
            TableId = dto.TableId,
            Status = ReservationStatus.Confirmed,
            SpecialRequests = dto.SpecialRequests,
            CreatedAt = DateTime.UtcNow
        };

        await _unitOfWork.Reservations.AddAsync(reservation);

        if (dto.TableId.HasValue)
        {
            var table = await _unitOfWork.Tables.GetByIdAsync(dto.TableId.Value);
            if (table != null && table.Status == TableStatus.Available)
            {
                table.Status = TableStatus.Reserved;
                await _unitOfWork.Tables.UpdateAsync(table);
            }
        }

        await _unitOfWork.SaveChangesAsync();

        return new ReservationDto
        {
            Id = reservation.Id,
            CustomerName = reservation.CustomerName,
            CustomerPhone = reservation.CustomerPhone,
            ReservationDate = reservation.ReservationDate,
            GuestCount = reservation.GuestCount,
            TableId = reservation.TableId,
            Status = reservation.Status,
            SpecialRequests = reservation.SpecialRequests
        };
    }

    public async Task<ReservationDto> UpdateReservationStatusAsync(int id, ReservationStatus status)
    {
        var res = await _unitOfWork.Reservations.GetByIdAsync(id);
        if (res == null) throw new KeyNotFoundException($"Reservation {id} not found");

        res.Status = status;
        res.UpdatedAt = DateTime.UtcNow;

        if (res.TableId.HasValue)
        {
            var table = await _unitOfWork.Tables.GetByIdAsync(res.TableId.Value);
            if (table != null)
            {
                if (status == ReservationStatus.Seated) table.Status = TableStatus.Occupied;
                else if (status == ReservationStatus.Cancelled || status == ReservationStatus.Completed) table.Status = TableStatus.Available;
                await _unitOfWork.Tables.UpdateAsync(table);
            }
        }

        await _unitOfWork.Reservations.UpdateAsync(res);
        await _unitOfWork.SaveChangesAsync();

        return new ReservationDto
        {
            Id = res.Id,
            CustomerName = res.CustomerName,
            CustomerPhone = res.CustomerPhone,
            ReservationDate = res.ReservationDate,
            GuestCount = res.GuestCount,
            TableId = res.TableId,
            Status = res.Status,
            SpecialRequests = res.SpecialRequests
        };
    }
}

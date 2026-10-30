using RestaurantManagement.Application.DTOs;
using RestaurantManagement.Domain.Enums;

namespace RestaurantManagement.Application.Interfaces;

public interface ICustomerService
{
    Task<IEnumerable<CustomerDto>> GetAllCustomersAsync();
    Task<CustomerDto?> GetCustomerByIdAsync(int id);
    Task<CustomerDto?> GetCustomerByPhoneAsync(string phone);
    Task<CustomerDto> CreateOrUpdateCustomerAsync(CustomerCreateUpdateDto dto);
    Task<CustomerDto> UpdateCustomerAsync(int id, CustomerCreateUpdateDto dto);
    Task<bool> DeleteCustomerAsync(int id);
    Task<bool> ToggleCustomerStatusAsync(int id);
    Task<int> AwardLoyaltyPointsAsync(int customerId, decimal orderAmount, string orderNo);
}

public interface IReservationService
{
    Task<IEnumerable<ReservationDto>> GetReservationsAsync(DateTime? date = null, ReservationStatus? status = null);
    Task<ReservationDto> CreateReservationAsync(CreateReservationDto dto);
    Task<ReservationDto> UpdateReservationStatusAsync(int id, ReservationStatus status);
}

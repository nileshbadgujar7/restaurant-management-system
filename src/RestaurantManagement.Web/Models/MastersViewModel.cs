using RestaurantManagement.Application.DTOs;
using RestaurantManagement.Domain.Entities;

namespace RestaurantManagement.Web.Models;

public class MastersViewModel
{
    public string ActiveTab { get; set; } = "floor";
    public string? ReturnUrl { get; set; }
    public bool OpenAdd { get; set; }
    public IEnumerable<FloorDto> Floors { get; set; } = new List<FloorDto>();
    public IEnumerable<TableDto> Tables { get; set; } = new List<TableDto>();
    public IEnumerable<CategoryDto> Categories { get; set; } = new List<CategoryDto>();
    public IEnumerable<MenuItemDto> MenuItems { get; set; } = new List<MenuItemDto>();
    public IEnumerable<SupplierDto> Suppliers { get; set; } = new List<SupplierDto>();
    public IEnumerable<CustomerDto> Customers { get; set; } = new List<CustomerDto>();
    public IEnumerable<EmployeeDto> Employees { get; set; } = new List<EmployeeDto>();
    public IEnumerable<InventoryItemDto> InventoryItems { get; set; } = new List<InventoryItemDto>();
    public RestaurantSetting? Setting { get; set; }
}

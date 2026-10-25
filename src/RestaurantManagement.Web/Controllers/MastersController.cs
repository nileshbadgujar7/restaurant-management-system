using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using RestaurantManagement.Application.DTOs;
using RestaurantManagement.Application.Interfaces;
using RestaurantManagement.Domain.Entities;
using RestaurantManagement.Web.Models;

namespace RestaurantManagement.Web.Controllers;

[Authorize(Roles = "Admin,Manager")]
public class MastersController : Controller
{
    private readonly ITableService _tableService;
    private readonly IMenuService _menuService;
    private readonly IStaffService _staffService;
    private readonly IInventoryService _inventoryService;
    private readonly IUnitOfWork _unitOfWork;

    public MastersController(
        ITableService tableService, 
        IMenuService menuService, 
        IStaffService staffService,
        IInventoryService inventoryService,
        IUnitOfWork unitOfWork)
    {
        _tableService = tableService;
        _menuService = menuService;
        _staffService = staffService;
        _inventoryService = inventoryService;
        _unitOfWork = unitOfWork;
    }

    public async Task<IActionResult> Index(string tab = "floor", string? returnUrl = null, bool openAdd = false)
    {
        var settings = await _unitOfWork.RestaurantSettings.GetAllAsync();
        var setting = settings.FirstOrDefault() ?? new RestaurantSetting();

        var model = new MastersViewModel
        {
            ActiveTab = tab.ToLower(),
            ReturnUrl = returnUrl,
            OpenAdd = openAdd,
            Floors = await _tableService.GetFloorsWithTablesAsync(),
            Tables = await _tableService.GetAllTablesAsync(),
            Categories = await _menuService.GetCategoriesAsync(),
            MenuItems = await _menuService.GetMenuItemsAsync(),
            Employees = await _staffService.GetEmployeesAsync(),
            InventoryItems = await _inventoryService.GetInventoryItemsAsync(),
            Setting = setting
        };

        return View(model);
    }

    // ================= Floor Operations =================
    [HttpPost]
    public async Task<IActionResult> CreateFloor(FloorCreateUpdateDto dto)
    {
        if (string.IsNullOrWhiteSpace(dto.Name))
        {
            TempData["Error"] = "Floor name is required.";
            return RedirectToAction(nameof(Index), new { tab = "floor" });
        }

        await _tableService.CreateFloorAsync(dto);
        TempData["Success"] = $"Floor '{dto.Name}' created successfully.";
        return RedirectToAction(nameof(Index), new { tab = "floor" });
    }

    [HttpPost]
    public async Task<IActionResult> UpdateFloor(int id, FloorCreateUpdateDto dto)
    {
        if (string.IsNullOrWhiteSpace(dto.Name))
        {
            TempData["Error"] = "Floor name is required.";
            return RedirectToAction(nameof(Index), new { tab = "floor" });
        }

        await _tableService.UpdateFloorAsync(id, dto);
        TempData["Success"] = $"Floor '{dto.Name}' updated successfully.";
        return RedirectToAction(nameof(Index), new { tab = "floor" });
    }

    [HttpPost]
    public async Task<IActionResult> DeleteFloor(int id)
    {
        bool success = await _tableService.DeleteFloorAsync(id);
        if (success) TempData["Success"] = "Floor deleted successfully.";
        else TempData["Error"] = "Could not delete floor.";
        return RedirectToAction(nameof(Index), new { tab = "floor" });
    }

    // ================= Table Operations =================
    [HttpPost]
    public async Task<IActionResult> CreateTable(TableCreateUpdateDto dto)
    {
        if (string.IsNullOrWhiteSpace(dto.TableNumber))
        {
            TempData["Error"] = "Table number is required.";
            return RedirectToAction(nameof(Index), new { tab = "table" });
        }

        await _tableService.CreateTableAsync(dto);
        TempData["Success"] = $"Table '{dto.TableNumber}' created successfully.";
        return RedirectToAction(nameof(Index), new { tab = "table" });
    }

    [HttpPost]
    public async Task<IActionResult> UpdateTable(int id, TableCreateUpdateDto dto)
    {
        if (string.IsNullOrWhiteSpace(dto.TableNumber))
        {
            TempData["Error"] = "Table number is required.";
            return RedirectToAction(nameof(Index), new { tab = "table" });
        }

        await _tableService.UpdateTableAsync(id, dto);
        TempData["Success"] = $"Table '{dto.TableNumber}' updated successfully.";
        return RedirectToAction(nameof(Index), new { tab = "table" });
    }

    [HttpPost]
    public async Task<IActionResult> DeleteTable(int id)
    {
        bool success = await _tableService.DeleteTableAsync(id);
        if (success) TempData["Success"] = "Table deleted successfully.";
        else TempData["Error"] = "Could not delete table.";
        return RedirectToAction(nameof(Index), new { tab = "table" });
    }

    // ================= Menu Item Operations =================
    [HttpPost]
    public async Task<IActionResult> CreateMenuItem(MenuItemCreateUpdateDto dto, string? returnUrl = null)
    {
        if (string.IsNullOrWhiteSpace(dto.Name) || dto.Price <= 0)
        {
            TempData["Error"] = "Valid Item Name and positive price are required.";
            return string.IsNullOrEmpty(returnUrl) 
                ? RedirectToAction(nameof(Index), new { tab = "menu" }) 
                : Redirect(returnUrl);
        }

        await _menuService.CreateMenuItemAsync(dto);
        TempData["Success"] = $"Menu item '{dto.Name}' added successfully.";

        if (!string.IsNullOrEmpty(returnUrl))
        {
            return Redirect(returnUrl);
        }

        return RedirectToAction(nameof(Index), new { tab = "menu" });
    }

    [HttpPost]
    public async Task<IActionResult> UpdateMenuItem(int id, MenuItemCreateUpdateDto dto)
    {
        if (string.IsNullOrWhiteSpace(dto.Name) || dto.Price <= 0)
        {
            TempData["Error"] = "Valid Item Name and positive price are required.";
            return RedirectToAction(nameof(Index), new { tab = "menu" });
        }

        await _menuService.UpdateMenuItemAsync(id, dto);
        TempData["Success"] = $"Menu item '{dto.Name}' updated successfully.";
        return RedirectToAction(nameof(Index), new { tab = "menu" });
    }

    [HttpPost]
    public async Task<IActionResult> DeleteMenuItem(int id)
    {
        bool success = await _menuService.DeleteMenuItemAsync(id);
        if (success) TempData["Success"] = "Menu item deleted successfully.";
        else TempData["Error"] = "Could not delete menu item.";
        return RedirectToAction(nameof(Index), new { tab = "menu" });
    }

    [HttpPost]
    public async Task<IActionResult> ToggleMenuItemAvailability(int id, string? tab = "menu")
    {
        await _menuService.ToggleItemAvailabilityAsync(id);
        return RedirectToAction(nameof(Index), new { tab = tab ?? "menu" });
    }

    // ================= Category Operations =================
    [HttpPost]
    public async Task<IActionResult> CreateCategory(CategoryDto dto)
    {
        if (string.IsNullOrWhiteSpace(dto.Name))
        {
            TempData["Error"] = "Category name is required.";
            return RedirectToAction(nameof(Index), new { tab = "category" });
        }

        await _menuService.CreateCategoryAsync(dto);
        TempData["Success"] = $"Category '{dto.Name}' created successfully.";
        return RedirectToAction(nameof(Index), new { tab = "category" });
    }

    [HttpPost]
    public async Task<IActionResult> UpdateCategory(int id, CategoryDto dto)
    {
        if (string.IsNullOrWhiteSpace(dto.Name))
        {
            TempData["Error"] = "Category name is required.";
            return RedirectToAction(nameof(Index), new { tab = "category" });
        }

        await _menuService.UpdateCategoryAsync(id, dto);
        TempData["Success"] = $"Category '{dto.Name}' updated successfully.";
        return RedirectToAction(nameof(Index), new { tab = "category" });
    }

    [HttpPost]
    public async Task<IActionResult> DeleteCategory(int id)
    {
        bool success = await _menuService.DeleteCategoryAsync(id);
        if (success) TempData["Success"] = "Category deleted successfully.";
        else TempData["Error"] = "Could not delete category.";
        return RedirectToAction(nameof(Index), new { tab = "category" });
    }

    // ================= Employee Operations =================
    [HttpPost]
    public async Task<IActionResult> CreateEmployee(EmployeeDto dto)
    {
        if (string.IsNullOrWhiteSpace(dto.Name) || string.IsNullOrWhiteSpace(dto.Phone))
        {
            TempData["Error"] = "Employee Name and Phone number are required.";
            return RedirectToAction(nameof(Index), new { tab = "employee" });
        }

        await _staffService.CreateEmployeeAsync(dto);
        TempData["Success"] = $"Employee '{dto.Name}' ({dto.Role}) registered successfully.";
        return RedirectToAction(nameof(Index), new { tab = "employee" });
    }

    [HttpPost]
    public async Task<IActionResult> UpdateEmployee(int id, EmployeeDto dto)
    {
        if (string.IsNullOrWhiteSpace(dto.Name) || string.IsNullOrWhiteSpace(dto.Phone))
        {
            TempData["Error"] = "Employee Name and Phone number are required.";
            return RedirectToAction(nameof(Index), new { tab = "employee" });
        }

        await _staffService.UpdateEmployeeAsync(id, dto);
        TempData["Success"] = $"Employee '{dto.Name}' updated successfully.";
        return RedirectToAction(nameof(Index), new { tab = "employee" });
    }

    [HttpPost]
    public async Task<IActionResult> DeleteEmployee(int id)
    {
        bool success = await _staffService.DeleteEmployeeAsync(id);
        if (success) TempData["Success"] = "Employee removed successfully.";
        else TempData["Error"] = "Could not remove employee.";
        return RedirectToAction(nameof(Index), new { tab = "employee" });
    }

    // ================= Restaurant Settings Operations =================
    [HttpPost]
    public async Task<IActionResult> UpdateSettings(RestaurantSetting updated)
    {
        var settings = await _unitOfWork.RestaurantSettings.GetAllAsync();
        var setting = settings.FirstOrDefault();
        if (setting == null)
        {
            setting = updated;
            await _unitOfWork.RestaurantSettings.AddAsync(setting);
        }
        else
        {
            setting.RestaurantName = updated.RestaurantName;
            setting.Address = updated.Address;
            setting.Phone = updated.Phone;
            setting.Email = updated.Email;
            setting.GSTIN = updated.GSTIN;
            setting.CurrencySymbol = updated.CurrencySymbol;
            setting.CGSTPercentage = updated.CGSTPercentage;
            setting.SGSTPercentage = updated.SGSTPercentage;
            setting.ServiceChargePercentage = updated.ServiceChargePercentage;
            setting.KitchenMode = updated.KitchenMode;
            setting.TrackReadyStatus = updated.TrackReadyStatus;
            setting.AutoMarkServedMinutes = updated.AutoMarkServedMinutes;
            setting.VoidApproval = updated.VoidApproval;
            setting.AllowBillingWithUnservedItems = updated.AllowBillingWithUnservedItems;
            setting.KitchenLiteScreen = updated.KitchenLiteScreen;
            setting.SinglePersonMode = updated.SinglePersonMode;
            setting.DraftExpiryHours = updated.DraftExpiryHours;
            setting.PackagingCharge = updated.PackagingCharge;
            setting.DeliveryCharge = updated.DeliveryCharge;
            if (!string.IsNullOrWhiteSpace(updated.ManagerPIN)) setting.ManagerPIN = updated.ManagerPIN;
            setting.UpdatedAt = DateTime.UtcNow;
            await _unitOfWork.RestaurantSettings.UpdateAsync(setting);
        }

        await _unitOfWork.SaveChangesAsync();
        TempData["Success"] = "Restaurant settings & tax configuration saved successfully.";
        return RedirectToAction(nameof(Index), new { tab = "settings" });
    }

    // ================= Inventory Stock Adjustment =================
    [HttpPost]
    public async Task<IActionResult> AdjustStock(StockAdjustmentDto dto)
    {
        try
        {
            await _inventoryService.AdjustStockAsync(dto);
            TempData["Success"] = "Stock adjustment recorded successfully.";
        }
        catch (Exception ex)
        {
            TempData["Error"] = ex.Message;
        }
        return RedirectToAction(nameof(Index), new { tab = "inventory" });
    }

    // ================= JSON API Endpoints for POS / Dashboard =================
    [AllowAnonymous]
    [HttpGet]
    public async Task<IActionResult> GetMenuItems()
    {
        var items = await _menuService.GetMenuItemsAsync();
        var result = items.Select(i => new {
            id = i.Id,
            name = i.Name,
            price = i.Price,
            categoryName = i.CategoryName,
            isAvailable = i.IsAvailable,
            isVeg = i.IsVeg
        });
        return Json(new { success = true, items = result });
    }

    [HttpGet]
    public async Task<IActionResult> GetMenuItemDetails(int id)
    {
        try
        {
            var items = await _menuService.GetMenuItemsAsync();
            var item = items.FirstOrDefault(i => i.Id == id);
            if (item == null) return NotFound(new { success = false });
            return Json(new { success = true, variants = item.Variants ?? new List<MenuItemVariantDto>(), addOns = item.AddOns ?? new List<AddOnDto>() });
        }
        catch
        {
            return Json(new { success = false });
        }
    }
}

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
    private readonly ISupplierService _supplierService;
    private readonly ICustomerService _customerService;
    private readonly IUnitOfWork _unitOfWork;

    public MastersController(
        ITableService tableService, 
        IMenuService menuService, 
        IStaffService staffService,
        IInventoryService inventoryService,
        ISupplierService supplierService,
        ICustomerService customerService,
        IUnitOfWork unitOfWork)
    {
        _tableService = tableService;
        _menuService = menuService;
        _staffService = staffService;
        _inventoryService = inventoryService;
        _supplierService = supplierService;
        _customerService = customerService;
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
            Suppliers = await _supplierService.GetSuppliersAsync(),
            Customers = await _customerService.GetAllCustomersAsync(),
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
        try
        {
            bool success = await _tableService.DeleteFloorAsync(id);
            if (success) TempData["Success"] = "Floor deleted successfully.";
            else TempData["Error"] = "Could not delete floor.";
        }
        catch (Exception ex)
        {
            TempData["Error"] = ex.Message;
        }
        return RedirectToAction(nameof(Index), new { tab = "floor" });
    }

    [HttpPost]
    public async Task<IActionResult> ToggleFloorStatus(int id)
    {
        var floor = await _unitOfWork.Floors.GetByIdAsync(id);
        if (floor != null && !floor.IsDeleted)
        {
            floor.IsActive = !floor.IsActive;
            floor.UpdatedAt = DateTime.UtcNow;
            await _unitOfWork.Floors.UpdateAsync(floor);
            await _unitOfWork.SaveChangesAsync();
            TempData["Success"] = $"Floor '{floor.Name}' is now {(floor.IsActive ? "Active" : "Inactive")}.";
        }
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

        try
        {
            await _tableService.CreateTableAsync(dto);
            TempData["Success"] = $"Table '{dto.TableNumber}' created successfully.";
        }
        catch (Exception ex)
        {
            TempData["Error"] = ex.Message;
        }
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

        try
        {
            await _tableService.UpdateTableAsync(id, dto);
            TempData["Success"] = $"Table '{dto.TableNumber}' updated successfully.";
        }
        catch (Exception ex)
        {
            TempData["Error"] = ex.Message;
        }
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

    [HttpPost]
    public async Task<IActionResult> ToggleTableStatus(int id)
    {
        var table = await _unitOfWork.Tables.GetByIdAsync(id);
        if (table != null && !table.IsDeleted)
        {
            table.IsActive = !table.IsActive;
            table.UpdatedAt = DateTime.UtcNow;
            await _unitOfWork.Tables.UpdateAsync(table);
            await _unitOfWork.SaveChangesAsync();
            TempData["Success"] = $"Table '{table.TableNumber}' is now {(table.IsActive ? "Active" : "Inactive")}.";
        }
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
    public async Task<IActionResult> ToggleMenuItemStatus(int id, string? tab = "menu")
    {
        var item = await _unitOfWork.MenuItems.GetByIdAsync(id);
        if (item != null && !item.IsDeleted)
        {
            item.IsActive = !item.IsActive;
            item.UpdatedAt = DateTime.UtcNow;
            await _unitOfWork.MenuItems.UpdateAsync(item);
            await _unitOfWork.SaveChangesAsync();
            TempData["Success"] = $"Menu item '{item.Name}' is now {(item.IsActive ? "Active" : "Inactive")}.";
        }
        return RedirectToAction(nameof(Index), new { tab = tab ?? "menu" });
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

    [HttpPost]
    public async Task<IActionResult> ToggleCategoryStatus(int id)
    {
        var cat = await _unitOfWork.Categories.GetByIdAsync(id);
        if (cat != null && !cat.IsDeleted)
        {
            cat.IsActive = !cat.IsActive;
            cat.UpdatedAt = DateTime.UtcNow;
            await _unitOfWork.Categories.UpdateAsync(cat);
            await _unitOfWork.SaveChangesAsync();
            TempData["Success"] = $"Category '{cat.Name}' is now {(cat.IsActive ? "Active" : "Inactive")}.";
        }
        return RedirectToAction(nameof(Index), new { tab = "category" });
    }

    // ================= Supplier Operations =================
    [HttpPost]
    public async Task<IActionResult> CreateSupplier(SupplierCreateUpdateDto dto)
    {
        if (string.IsNullOrWhiteSpace(dto.Name) || string.IsNullOrWhiteSpace(dto.Phone))
        {
            TempData["Error"] = "Supplier Name and Phone number are required.";
            return RedirectToAction(nameof(Index), new { tab = "supplier" });
        }

        try
        {
            await _supplierService.CreateSupplierAsync(dto);
            TempData["Success"] = $"Supplier '{dto.Name}' registered successfully.";
        }
        catch (Exception ex)
        {
            TempData["Error"] = ex.Message;
        }

        return RedirectToAction(nameof(Index), new { tab = "supplier" });
    }

    [HttpPost]
    public async Task<IActionResult> UpdateSupplier(int id, SupplierCreateUpdateDto dto)
    {
        if (string.IsNullOrWhiteSpace(dto.Name) || string.IsNullOrWhiteSpace(dto.Phone))
        {
            TempData["Error"] = "Supplier Name and Phone number are required.";
            return RedirectToAction(nameof(Index), new { tab = "supplier" });
        }

        try
        {
            await _supplierService.UpdateSupplierAsync(id, dto);
            TempData["Success"] = $"Supplier '{dto.Name}' updated successfully.";
        }
        catch (Exception ex)
        {
            TempData["Error"] = ex.Message;
        }

        return RedirectToAction(nameof(Index), new { tab = "supplier" });
    }

    [HttpPost]
    public async Task<IActionResult> DeleteSupplier(int id)
    {
        try
        {
            bool success = await _supplierService.DeleteSupplierAsync(id);
            if (success) TempData["Success"] = "Supplier removed successfully.";
            else TempData["Error"] = "Could not remove supplier.";
        }
        catch (Exception ex)
        {
            TempData["Error"] = ex.Message;
        }

        return RedirectToAction(nameof(Index), new { tab = "supplier" });
    }

    [HttpPost]
    public async Task<IActionResult> ToggleSupplierStatus(int id)
    {
        var supplier = await _unitOfWork.Suppliers.GetByIdAsync(id);
        if (supplier != null && !supplier.IsDeleted)
        {
            supplier.IsActive = !supplier.IsActive;
            supplier.UpdatedAt = DateTime.UtcNow;
            await _unitOfWork.Suppliers.UpdateAsync(supplier);
            await _unitOfWork.SaveChangesAsync();
            TempData["Success"] = $"Supplier '{supplier.Name}' is now {(supplier.IsActive ? "Active" : "Inactive")}.";
        }
        return RedirectToAction(nameof(Index), new { tab = "supplier" });
    }

    // ================= Customer Operations =================
    [HttpPost]
    public async Task<IActionResult> CreateCustomer(CustomerCreateUpdateDto dto)
    {
        if (string.IsNullOrWhiteSpace(dto.Name) || string.IsNullOrWhiteSpace(dto.Phone))
        {
            TempData["Error"] = "Customer Name and Mobile Number are required.";
            return RedirectToAction(nameof(Index), new { tab = "customer" });
        }

        try
        {
            await _customerService.CreateOrUpdateCustomerAsync(dto);
            TempData["Success"] = $"Customer '{dto.Name}' registered successfully.";
        }
        catch (Exception ex)
        {
            TempData["Error"] = ex.Message;
        }

        return RedirectToAction(nameof(Index), new { tab = "customer" });
    }

    [HttpPost]
    public async Task<IActionResult> UpdateCustomer(int id, CustomerCreateUpdateDto dto)
    {
        if (string.IsNullOrWhiteSpace(dto.Name) || string.IsNullOrWhiteSpace(dto.Phone))
        {
            TempData["Error"] = "Customer Name and Mobile Number are required.";
            return RedirectToAction(nameof(Index), new { tab = "customer" });
        }

        try
        {
            await _customerService.UpdateCustomerAsync(id, dto);
            TempData["Success"] = $"Customer '{dto.Name}' updated successfully.";
        }
        catch (Exception ex)
        {
            TempData["Error"] = ex.Message;
        }

        return RedirectToAction(nameof(Index), new { tab = "customer" });
    }

    [HttpPost]
    public async Task<IActionResult> DeleteCustomer(int id)
    {
        bool success = await _customerService.DeleteCustomerAsync(id);
        if (success)
            TempData["Success"] = "Customer deleted successfully.";
        else
            TempData["Error"] = "Customer not found or could not be deleted.";

        return RedirectToAction(nameof(Index), new { tab = "customer" });
    }

    [HttpPost]
    public async Task<IActionResult> ToggleCustomerStatus(int id)
    {
        bool success = await _customerService.ToggleCustomerStatusAsync(id);
        if (success)
            TempData["Success"] = "Customer status updated successfully.";
        else
            TempData["Error"] = "Customer not found.";

        return RedirectToAction(nameof(Index), new { tab = "customer" });
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
    public async Task<IActionResult> UpdateSettings(RestaurantSetting updated, IFormFile? logoFile, bool removeLogo = false)
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
            setting.RestaurantNameMarathi = updated.RestaurantNameMarathi ?? string.Empty;
            setting.Address = updated.Address;
            setting.Phone = updated.Phone;
            setting.Email = updated.Email;
            setting.GSTIN = updated.GSTIN;
            setting.InvoicePrefix = string.IsNullOrWhiteSpace(updated.InvoicePrefix) ? "INV" : updated.InvoicePrefix;
            setting.CurrencySymbol = updated.CurrencySymbol;

            // GST Configurations (5.6 C)
            setting.IsGSTEnabled = updated.IsGSTEnabled;
            setting.IsTaxInclusive = updated.IsTaxInclusive;
            setting.CGSTPercentage = updated.CGSTPercentage;
            setting.SGSTPercentage = updated.SGSTPercentage;
            setting.ServiceChargePercentage = updated.ServiceChargePercentage;

            // Kitchen / KOT Workflow (5.6 E)
            setting.IsKOTWorkflowEnabled = updated.IsKOTWorkflowEnabled;
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

            // QR Code Configurations (5.6 D)
            setting.IsQRCodeEnabled = updated.IsQRCodeEnabled;
            if (!string.IsNullOrWhiteSpace(updated.QRCodeData)) setting.QRCodeData = updated.QRCodeData;

            // Printing & Integrations
            setting.ThermalPrinterWidth = updated.ThermalPrinterWidth > 0 ? updated.ThermalPrinterWidth : 80;
            setting.WhatsAppApiKey = updated.WhatsAppApiKey ?? string.Empty;
            setting.DefaultLanguage = updated.DefaultLanguage ?? "en";

            // Logo Upload & Removal (5.6 B)
            if (removeLogo)
            {
                setting.LogoUrl = string.Empty;
            }
            else if (logoFile != null && logoFile.Length > 0)
            {
                try
                {
                    var webRoot = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot");
                    var uploadsFolder = Path.Combine(webRoot, "images");
                    if (!Directory.Exists(uploadsFolder)) Directory.CreateDirectory(uploadsFolder);
                    var ext = Path.GetExtension(logoFile.FileName);
                    var fileName = $"logo_{DateTime.UtcNow.Ticks}{ext}";
                    var filePath = Path.Combine(uploadsFolder, fileName);
                    using (var fs = new FileStream(filePath, FileMode.Create))
                    {
                        await logoFile.CopyToAsync(fs);
                    }
                    setting.LogoUrl = $"/images/{fileName}";
                }
                catch
                {
                    // Fallback to existing or provided url
                }
            }

            setting.UpdatedAt = DateTime.UtcNow;
            await _unitOfWork.RestaurantSettings.UpdateAsync(setting);
        }

        await _unitOfWork.SaveChangesAsync();
        TempData["Success"] = "Application configurations & master settings updated successfully.";
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
            nameMarathi = i.NameMarathi,
            itemCode = i.ItemCode,
            shortCode = !string.IsNullOrWhiteSpace(i.ShortCode) ? i.ShortCode : (!string.IsNullOrWhiteSpace(i.ItemCode) ? i.ItemCode : i.Id.ToString()),
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

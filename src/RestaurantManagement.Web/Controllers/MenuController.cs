using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using RestaurantManagement.Application.DTOs;
using RestaurantManagement.Application.Interfaces;

namespace RestaurantManagement.Web.Controllers;

[Authorize(Roles = "Admin,Manager,Waiter,Cashier")]
public class MenuController : Controller
{
    private readonly IMenuService _menuService;
    private readonly IUnitOfWork _unitOfWork;

    public MenuController(IMenuService menuService, IUnitOfWork unitOfWork)
    {
        _menuService = menuService;
        _unitOfWork = unitOfWork;
    }

    public async Task<IActionResult> Index()
    {
        ViewBag.Categories = await _menuService.GetCategoriesAsync();
        var items = await _menuService.GetMenuItemsAsync();
        return View(items);
    }

    public async Task<IActionResult> PrintMenuCard(string lang = "both")
    {
        var settings = await _unitOfWork.RestaurantSettings.GetAllAsync();
        ViewBag.Settings = settings.FirstOrDefault() ?? new RestaurantManagement.Domain.Entities.RestaurantSetting();
        ViewBag.Categories = (await _menuService.GetCategoriesAsync()).Where(c => c.IsActive).OrderBy(c => c.DisplayOrder).ToList();
        ViewBag.SelectedLang = lang;

        var items = (await _menuService.GetMenuItemsAsync()).Where(i => i.IsActive && i.IsAvailable).ToList();
        return View(items);
    }

    [HttpPost]
    public async Task<IActionResult> ToggleAvailability(int id)
    {
        await _menuService.ToggleItemAvailabilityAsync(id);
        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    public async Task<IActionResult> Create(MenuItemCreateUpdateDto dto, string? returnUrl = null)
    {
        await _menuService.CreateMenuItemAsync(dto);
        TempData["Success"] = "New menu item added successfully!";
        if (!string.IsNullOrEmpty(returnUrl))
        {
            return Redirect(returnUrl);
        }
        return RedirectToAction(nameof(Index));
    }
}

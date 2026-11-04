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
    private readonly IWebHostEnvironment _webHostEnvironment;

    public MenuController(IMenuService menuService, IUnitOfWork unitOfWork, IWebHostEnvironment webHostEnvironment)
    {
        _menuService = menuService;
        _unitOfWork = unitOfWork;
        _webHostEnvironment = webHostEnvironment;
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
    public async Task<IActionResult> Create(MenuItemCreateUpdateDto dto, IFormFile? imageFile = null, string? returnUrl = null)
    {
        if (imageFile != null && imageFile.Length > 0)
        {
            try
            {
                var ext = Path.GetExtension(imageFile.FileName).ToLowerInvariant();
                var allowedExts = new[] { ".jpg", ".jpeg", ".png", ".webp", ".gif" };
                if (allowedExts.Contains(ext))
                {
                    var webRoot = _webHostEnvironment.WebRootPath ?? Path.Combine(Directory.GetCurrentDirectory(), "wwwroot");
                    var folder = Path.Combine(webRoot, "images", "menu");
                    if (!Directory.Exists(folder)) Directory.CreateDirectory(folder);

                    var fileName = $"menu_{DateTime.UtcNow.Ticks}_{Guid.NewGuid().ToString("N")[..6]}{ext}";
                    var filePath = Path.Combine(folder, fileName);

                    using (var stream = new FileStream(filePath, FileMode.Create))
                    {
                        await imageFile.CopyToAsync(stream);
                    }

                    dto.ImageUrl = $"/images/menu/{fileName}";
                }
            }
            catch { }
        }

        await _menuService.CreateMenuItemAsync(dto);
        TempData["Success"] = "New menu item added successfully!";
        if (!string.IsNullOrEmpty(returnUrl))
        {
            return Redirect(returnUrl);
        }
        return RedirectToAction(nameof(Index));
    }
}

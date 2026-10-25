using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using RestaurantManagement.Application.Interfaces;
using RestaurantManagement.Domain.Entities;
using RestaurantManagement.Domain.Enums;

namespace RestaurantManagement.Web.Controllers;

[Authorize(Roles = "Admin,Manager,KitchenStaff,Cook")]
public class KitchenController : Controller
{
    private readonly IKOTService _kotService;
    private readonly IUnitOfWork _unitOfWork;

    public KitchenController(IKOTService kotService, IUnitOfWork unitOfWork)
    {
        _kotService = kotService;
        _unitOfWork = unitOfWork;
    }

    public async Task<IActionResult> Index()
    {
        var kots = await _kotService.GetActiveKOTsAsync();
        var settings = await _unitOfWork.RestaurantSettings.GetAllAsync();
        ViewBag.Setting = settings.FirstOrDefault() ?? new RestaurantSetting();
        return View(kots);
    }

    [HttpPost]
    public async Task<IActionResult> UpdateKOTStatus(int kotId, KOTStatus status)
    {
        await _kotService.UpdateKOTStatusAsync(kotId, status);
        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    public async Task<IActionResult> AcknowledgeKOT(int kotId)
    {
        var kot = await _unitOfWork.KOTs.GetByIdAsync(kotId);
        if (kot != null)
        {
            kot.AcknowledgedByChef = true;
            await _unitOfWork.KOTs.UpdateAsync(kot);
            await _unitOfWork.SaveChangesAsync();
        }
        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    public async Task<IActionResult> UpdateKOTItem([FromBody] UpdateKOTItemRequest req)
    {
        try
        {
            var kotItem = await _unitOfWork.KOTItems.GetByIdAsync(req.KOTItemId);
            if (kotItem == null) return Json(new { success = false, message = "KOT item not found." });

            if (req.Quantity.HasValue && req.Quantity.Value > 0)
                kotItem.Quantity = req.Quantity.Value;
            if (req.SpecialInstructions != null)
                kotItem.SpecialInstructions = req.SpecialInstructions;

            await _unitOfWork.KOTItems.UpdateAsync(kotItem);
            await _unitOfWork.SaveChangesAsync();
            return Json(new { success = true });
        }
        catch (Exception ex)
        {
            return Json(new { success = false, message = ex.Message });
        }
    }
}

public class UpdateKOTItemRequest
{
    public int KOTItemId { get; set; }
    public int? Quantity { get; set; }
    public string? SpecialInstructions { get; set; }
}


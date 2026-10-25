using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using RestaurantManagement.Application.Interfaces;

namespace RestaurantManagement.Web.Controllers;

[Authorize(Roles = "Admin,Manager,Cashier")]
public class ReportsController : Controller
{
    private readonly IReportService _reportService;

    public ReportsController(IReportService reportService)
    {
        _reportService = reportService;
    }

    public async Task<IActionResult> Index(string period = "DayWise", int? year = null, int? month = null, DateTime? startDate = null, DateTime? endDate = null)
    {
        var report = await _reportService.GetComprehensiveReportAsync(period, year, month, startDate, endDate);
        return View(report);
    }
}

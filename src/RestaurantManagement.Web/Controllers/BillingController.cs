using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using RestaurantManagement.Application.DTOs;
using RestaurantManagement.Application.Interfaces;

namespace RestaurantManagement.Web.Controllers;

[Authorize(Roles = "Admin,Manager,Cashier")]
public class BillingController : Controller
{
    private readonly IBillingService _billingService;
    private readonly IOrderService _orderService;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ICustomerService _customerService;

    public BillingController(
        IBillingService billingService, 
        IOrderService orderService, 
        IUnitOfWork unitOfWork, 
        ICustomerService customerService)
    {
        _billingService = billingService;
        _orderService = orderService;
        _unitOfWork = unitOfWork;
        _customerService = customerService;
    }

    public async Task<IActionResult> Index()
    {
        var bills = await _billingService.GetRecentBillsAsync();
        ViewBag.Customers = await _customerService.GetAllCustomersAsync();
        return View(bills);
    }

    public async Task<IActionResult> Generate(int orderId)
    {
        if (orderId <= 0)
        {
            var activeOrders = await _orderService.GetActiveOrdersAsync();
            var firstOrder = activeOrders.FirstOrDefault();
            if (firstOrder == null)
            {
                TempData["Error"] = "No active orders found to generate bill.";
                return RedirectToAction(nameof(Index));
            }
            orderId = firstOrder.Id;
        }

        var bill = await _billingService.GenerateBillAsync(new CreateBillDto { OrderId = orderId });
        return RedirectToAction(nameof(Invoice), new { id = bill.Id });
    }

    public async Task<IActionResult> Invoice(int id)
    {
        var bill = await _billingService.GetBillByIdAsync(id);
        if (bill == null) return NotFound();

        var settings = await _unitOfWork.RestaurantSettings.GetAllAsync();
        ViewBag.Settings = settings.FirstOrDefault() ?? new RestaurantManagement.Domain.Entities.RestaurantSetting();
        ViewBag.Customers = await _customerService.GetAllCustomersAsync();

        return View(bill);
    }

    [HttpPost]
    public async Task<IActionResult> CollectPayment()
    {
        bool isJson = Request.ContentType?.Contains("application/json") == true;
        CollectPaymentDto dto;
        string? returnUrl = null;

        if (isJson)
        {
            using var reader = new StreamReader(Request.Body);
            var json = await reader.ReadToEndAsync();
            var options = new System.Text.Json.JsonSerializerOptions { PropertyNameCaseInsensitive = true };
            dto = System.Text.Json.JsonSerializer.Deserialize<CollectPaymentDto>(json, options) ?? new CollectPaymentDto();
        }
        else
        {
            dto = new CollectPaymentDto
            {
                BillId = int.TryParse(Request.Form["BillId"], out int bid) ? bid : 0,
                Amount = decimal.TryParse(Request.Form["Amount"], out decimal amt) ? amt : 0,
                PaymentMethod = Enum.TryParse<RestaurantManagement.Domain.Enums.PaymentMethod>(Request.Form["PaymentMethod"], out var pm) ? pm : RestaurantManagement.Domain.Enums.PaymentMethod.Cash,
                TransactionId = Request.Form["TransactionId"].ToString(),
                Notes = Request.Form["Notes"].ToString(),
                CustomerId = int.TryParse(Request.Form["CustomerId"], out int cid) && cid > 0 ? cid : null,
                NewCustomerName = Request.Form["NewCustomerName"].ToString(),
                NewCustomerPhone = Request.Form["NewCustomerPhone"].ToString(),
                SplitCashAmount = decimal.TryParse(Request.Form["SplitCashAmount"], out decimal sca) ? sca : 0m,
                SplitOnlineAmount = decimal.TryParse(Request.Form["SplitOnlineAmount"], out decimal soa) ? soa : 0m
            };
            returnUrl = Request.Form["ReturnUrl"].ToString();
        }

        try
        {
            var bill = await _billingService.CollectPaymentAsync(dto);
            if (isJson)
            {
                return Json(new { success = true, isPaid = bill.IsPaid, bill });
            }
            TempData["Success"] = "Payment collected successfully! Table status updated to Available.";
            if (!string.IsNullOrEmpty(returnUrl) && Url.IsLocalUrl(returnUrl))
            {
                return Redirect(returnUrl);
            }
            return RedirectToAction(nameof(Invoice), new { id = dto.BillId });
        }
        catch (Exception ex)
        {
            if (isJson)
            {
                return Json(new { success = false, message = ex.Message });
            }
            TempData["Error"] = ex.Message;
            if (!string.IsNullOrEmpty(returnUrl) && Url.IsLocalUrl(returnUrl))
            {
                return Redirect(returnUrl);
            }
            return RedirectToAction(nameof(Invoice), new { id = dto.BillId });
        }
    }
}

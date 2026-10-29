using System.Text;
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

    [HttpGet]
    public async Task<IActionResult> ExportExcel(string reportType = "Sales", string period = "DayWise", int? year = null, int? month = null, DateTime? startDate = null, DateTime? endDate = null)
    {
        var report = await _reportService.GetComprehensiveReportAsync(period, year, month, startDate, endDate);
        var sb = new StringBuilder();

        string fileName;
        string timestamp = DateTime.UtcNow.ToString("yyyyMMdd_HHmmss");

        if (string.Equals(reportType, "Table", StringComparison.OrdinalIgnoreCase))
        {
            fileName = $"Table_Wise_Collection_Report_{timestamp}.csv";
            sb.AppendLine("TABLE-WISE COLLECTION REPORT");
            sb.AppendLine($"Period: {report.PeriodType}, Date Range: {report.StartDate:dd-MMM-yyyy} to {report.EndDate:dd-MMM-yyyy}");
            sb.AppendLine($"Total Collection: INR {report.TotalRevenue:N2}, Total Orders: {report.TotalOrders}");
            sb.AppendLine();
            sb.AppendLine("Table / Location,Floor,Orders Served,Total Collection (INR),Average Bill (INR),Share (%)");

            foreach (var t in report.TableBreakdown)
            {
                sb.AppendLine($"\"{EscapeCsv(t.TableNumber)}\",\"{EscapeCsv(t.FloorName)}\",{t.OrdersCount},{t.TotalAmount:F2},{t.AverageBill:F2},{t.Percentage:F1}%");
            }

            sb.AppendLine();
            sb.AppendLine($"\"TOTAL\",,{report.TableBreakdown.Sum(t => t.OrdersCount)},{report.TableBreakdown.Sum(t => t.TotalAmount):F2},,100.0%");
        }
        else if (string.Equals(reportType, "MenuItem", StringComparison.OrdinalIgnoreCase))
        {
            fileName = $"Menu_Item_Sales_Report_{timestamp}.csv";
            sb.AppendLine("MENU ITEM-WISE COLLECTION & SALES REPORT");
            sb.AppendLine($"Period: {report.PeriodType}, Date Range: {report.StartDate:dd-MMM-yyyy} to {report.EndDate:dd-MMM-yyyy}");
            sb.AppendLine($"Total Menu Revenue: INR {report.MenuItemBreakdown.Sum(m => m.TotalRevenue):N2}, Total Quantity Sold: {report.MenuItemBreakdown.Sum(m => m.QuantitySold)}");
            sb.AppendLine();
            sb.AppendLine("Item Code,Dish Name,Dish Name (Marathi),Category,Unit Price (INR),Qty Sold,Total Revenue (INR),Share (%)");

            foreach (var m in report.MenuItemBreakdown)
            {
                sb.AppendLine($"\"{EscapeCsv(m.ItemCode)}\",\"{EscapeCsv(m.ItemName)}\",\"{EscapeCsv(m.ItemNameMarathi)}\",\"{EscapeCsv(m.CategoryName)}\",{m.UnitPrice:F2},{m.QuantitySold},{m.TotalRevenue:F2},{m.Percentage:F1}%");
            }

            sb.AppendLine();
            sb.AppendLine($"\"TOTAL\",,,,{report.MenuItemBreakdown.Sum(m => m.QuantitySold)},{report.MenuItemBreakdown.Sum(m => m.TotalRevenue):F2},100.0%");
        }
        else
        {
            // Sales & Revenue Overview
            fileName = $"Sales_Revenue_Report_{report.PeriodType}_{timestamp}.csv";
            sb.AppendLine("SALES & REVENUE COMPREHENSIVE REPORT");
            sb.AppendLine($"Period Type: {report.PeriodType}, Date Range: {report.StartDate:dd-MMM-yyyy} to {report.EndDate:dd-MMM-yyyy}");
            sb.AppendLine($"Total Revenue: INR {report.TotalRevenue:N2}, Total Orders: {report.TotalOrders}, Dine-in: INR {report.DineInSales:N2}, Takeaway: INR {report.TakeawaySales:N2}");
            sb.AppendLine($"Total GST: INR {report.TotalTax:N2} (CGST: {report.TotalCGST:N2}, SGST: {report.TotalSGST:N2}), Total Discounts: INR {report.TotalDiscount:N2}");
            sb.AppendLine();
            sb.AppendLine("Period,Orders,Dine-in,Takeaway,Gross Sales (INR),GST Tax (INR),Discount (INR),Net Revenue (INR)");

            foreach (var row in report.BreakdownList)
            {
                sb.AppendLine($"\"{EscapeCsv(row.PeriodLabel)}\",{row.OrdersCount},{row.DineInCount},{row.TakeawayCount},{row.GrossSales:F2},{row.TaxCollected:F2},{row.DiscountGiven:F2},{row.NetRevenue:F2}");
            }

            sb.AppendLine();
            sb.AppendLine($"\"TOTALS\",{report.TotalOrders},{report.BreakdownList.Sum(b => b.DineInCount)},{report.BreakdownList.Sum(b => b.TakeawayCount)},{report.BreakdownList.Sum(b => b.GrossSales):F2},{report.TotalTax:F2},{report.TotalDiscount:F2},{report.TotalRevenue:F2}");
        }

        byte[] preamble = Encoding.UTF8.GetPreamble();
        byte[] content = Encoding.UTF8.GetBytes(sb.ToString());
        byte[] fileBytes = new byte[preamble.Length + content.Length];
        Buffer.BlockCopy(preamble, 0, fileBytes, 0, preamble.Length);
        Buffer.BlockCopy(content, 0, fileBytes, preamble.Length, content.Length);

        return File(fileBytes, "text/csv; charset=utf-8", fileName);
    }

    private static string EscapeCsv(string? text)
    {
        if (string.IsNullOrEmpty(text)) return string.Empty;
        return text.Replace("\"", "\"\"");
    }
}

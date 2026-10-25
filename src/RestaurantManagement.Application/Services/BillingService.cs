using RestaurantManagement.Application.DTOs;
using RestaurantManagement.Application.Interfaces;
using RestaurantManagement.Domain.Entities;
using RestaurantManagement.Domain.Enums;

namespace RestaurantManagement.Application.Services;

public class BillingService : IBillingService
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly ICustomerService _customerService;

    public BillingService(IUnitOfWork unitOfWork, ICustomerService customerService)
    {
        _unitOfWork = unitOfWork;
        _customerService = customerService;
    }

    public async Task<BillDto> GenerateBillAsync(CreateBillDto dto)
    {
        var order = await _unitOfWork.Orders.GetByIdAsync(dto.OrderId);
        if (order == null || order.IsDeleted) throw new KeyNotFoundException($"Order {dto.OrderId} not found");
        if (order.Status == OrderStatus.Completed || order.Status == OrderStatus.Cancelled)
        {
            throw new InvalidOperationException("Cannot generate bill for completed or cancelled order.");
        }

        // Check if bill already exists
        var existingBill = (await _unitOfWork.Bills.FindAsync(b => b.OrderId == dto.OrderId && !b.IsDeleted)).FirstOrDefault();
        if (existingBill != null)
        {
            return (await GetBillByIdAsync(existingBill.Id))!;
        }

        int billCount = await _unitOfWork.Bills.CountAsync();
        string invoiceNo = $"INV-{(billCount + 10001)}";

        decimal subtotal = order.SubTotal;
        decimal discount = dto.DiscountAmount;
        decimal serviceCharge = dto.ServiceCharge;
        decimal taxableSubtotal = Math.Max(0, subtotal - discount + serviceCharge);

        decimal cgst = Math.Round(taxableSubtotal * 0.025m, 2);
        decimal sgst = Math.Round(taxableSubtotal * 0.025m, 2);
        decimal grandTotal = Math.Round(taxableSubtotal + cgst + sgst, 0); // Rounded grand total

        var bill = new Bill
        {
            InvoiceNumber = invoiceNo,
            OrderId = dto.OrderId,
            BillDate = DateTime.UtcNow,
            SubTotal = subtotal,
            DiscountAmount = discount,
            ServiceCharge = serviceCharge,
            CGST = cgst,
            SGST = sgst,
            GrandTotal = grandTotal,
            IsPaid = false
        };

        await _unitOfWork.Bills.AddAsync(bill);

        if (order.TableId.HasValue)
        {
            var table = await _unitOfWork.Tables.GetByIdAsync(order.TableId.Value);
            if (table != null)
            {
                table.Status = TableStatus.Billing;
                await _unitOfWork.Tables.UpdateAsync(table);
            }
        }

        await _unitOfWork.SaveChangesAsync();

        return (await GetBillByIdAsync(bill.Id))!;
    }

    public async Task<BillDto?> GetBillByIdAsync(int id)
    {
        var bill = await _unitOfWork.Bills.GetByIdAsync(id);
        if (bill == null || bill.IsDeleted) return null;

        var order = await _unitOfWork.Orders.GetByIdAsync(bill.OrderId);
        var table = order?.TableId.HasValue == true ? await _unitOfWork.Tables.GetByIdAsync(order.TableId.Value) : null;
        var payments = await _unitOfWork.Payments.FindAsync(p => p.BillId == id && !p.IsDeleted);

        var dto = new BillDto
        {
            Id = bill.Id,
            InvoiceNumber = bill.InvoiceNumber,
            OrderId = bill.OrderId,
            OrderNumber = order?.OrderNumber ?? "",
            TableNumber = table?.TableNumber,
            BillDate = bill.BillDate,
            SubTotal = bill.SubTotal,
            DiscountAmount = bill.DiscountAmount,
            ServiceCharge = bill.ServiceCharge,
            CGST = bill.CGST,
            SGST = bill.SGST,
            GrandTotal = bill.GrandTotal,
            IsPaid = bill.IsPaid,
            Payments = payments.Select(p => new PaymentDto
            {
                Id = p.Id,
                BillId = p.BillId,
                PaymentMethod = p.PaymentMethod,
                Amount = p.Amount,
                PaymentStatus = p.PaymentStatus,
                TransactionId = p.TransactionId,
                PaymentDate = p.PaymentDate,
                Notes = p.Notes
            }).ToList()
        };

        if (order != null)
        {
            var items = await _unitOfWork.OrderItems.FindAsync(i => i.OrderId == order.Id && !i.IsDeleted && i.Status != OrderItemStatus.Cancelled);
            dto.Order = new OrderDto
            {
                Id = order.Id,
                OrderNumber = order.OrderNumber,
                OrderType = order.OrderType,
                Status = order.Status,
                CustomerName = order.CustomerName,
                CustomerPhone = order.CustomerPhone,
                SubTotal = order.SubTotal,
                GrandTotal = order.GrandTotal,
                Items = items.Select(i => new OrderItemDto
                {
                    Id = i.Id,
                    ItemName = i.ItemName,
                    UnitPrice = i.UnitPrice,
                    Quantity = i.Quantity,
                    TotalPrice = i.TotalPrice,
                    SelectedVariantName = i.SelectedVariantName
                }).ToList()
            };
        }

        return dto;
    }

    public async Task<BillDto?> GetBillByOrderIdAsync(int orderId)
    {
        var bills = await _unitOfWork.Bills.FindAsync(b => b.OrderId == orderId && !b.IsDeleted);
        var bill = bills.FirstOrDefault();
        return bill != null ? await GetBillByIdAsync(bill.Id) : null;
    }

    public async Task<BillDto> CollectPaymentAsync(CollectPaymentDto dto)
    {
        var bill = await _unitOfWork.Bills.GetByIdAsync(dto.BillId);
        if (bill == null || bill.IsDeleted) throw new KeyNotFoundException($"Bill {dto.BillId} not found");

        var existingPayments = (await _unitOfWork.Payments.FindAsync(p => p.BillId == dto.BillId && !p.IsDeleted)).ToList();
        decimal paidSoFar = existingPayments.Sum(p => p.Amount);
        decimal remaining = bill.GrandTotal - paidSoFar;

        if (dto.Amount > remaining + 0.01m)
        {
            throw new InvalidOperationException($"Payment amount ₹{dto.Amount} exceeds remaining bill balance ₹{remaining}.");
        }

        var payment = new Payment
        {
            BillId = dto.BillId,
            PaymentMethod = dto.PaymentMethod,
            Amount = dto.Amount,
            PaymentStatus = PaymentStatus.Paid,
            TransactionId = string.IsNullOrEmpty(dto.TransactionId) ? $"TXN-{Guid.NewGuid().ToString().Substring(0, 8).ToUpper()}" : dto.TransactionId,
            PaymentDate = DateTime.UtcNow,
            Notes = dto.Notes
        };

        await _unitOfWork.Payments.AddAsync(payment);

        if (paidSoFar + dto.Amount >= bill.GrandTotal - 0.01m)
        {
            bill.IsPaid = true;
            await _unitOfWork.Bills.UpdateAsync(bill);

            // Complete order and release table
            var order = await _unitOfWork.Orders.GetByIdAsync(bill.OrderId);
            if (order != null)
            {
                order.Status = OrderStatus.Completed;
                await _unitOfWork.Orders.UpdateAsync(order);

                if (order.TableId.HasValue)
                {
                    var table = await _unitOfWork.Tables.GetByIdAsync(order.TableId.Value);
                    if (table != null)
                    {
                        table.Status = TableStatus.Available;
                        table.CurrentOrderId = null;
                        await _unitOfWork.Tables.UpdateAsync(table);
                    }
                }

                // Award loyalty points if customer ID is attached
                if (order.CustomerId.HasValue)
                {
                    await _customerService.AwardLoyaltyPointsAsync(order.CustomerId.Value, bill.GrandTotal, order.OrderNumber);
                }
            }
        }

        await _unitOfWork.SaveChangesAsync();

        return (await GetBillByIdAsync(dto.BillId))!;
    }

    public async Task<IEnumerable<BillDto>> GetRecentBillsAsync()
    {
        var bills = await _unitOfWork.Bills.GetAllAsync();
        var result = new List<BillDto>();
        foreach (var b in bills.Where(x => !x.IsDeleted).OrderByDescending(x => x.BillDate).Take(30))
        {
            var dto = await GetBillByIdAsync(b.Id);
            if (dto != null) result.Add(dto);
        }
        return result;
    }
}

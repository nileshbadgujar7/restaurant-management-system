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

        var settings = await _unitOfWork.RestaurantSettings.GetAllAsync();
        var setting = settings.FirstOrDefault() ?? new RestaurantSetting();

        // Mode A / KOT Workflow enforcement: Block if unserved items exist and block setting is active
        if (setting.IsKOTWorkflowEnabled && setting.AllowBillingWithUnservedItems == BillingUnservedItemsSetting.No)
        {
            var kots = await _unitOfWork.KOTs.FindAsync(k => k.OrderId == dto.OrderId && !k.IsDeleted);
            if (kots.Any(k => k.Status != KOTStatus.Served))
            {
                throw new InvalidOperationException("Cannot finalize bill: Kitchen ticket items are still being prepared. All items must be marked Served before billing in Mode A.");
            }
        }

        // Check if bill already exists
        var existingBill = (await _unitOfWork.Bills.FindAsync(b => b.OrderId == dto.OrderId && !b.IsDeleted)).FirstOrDefault();
        if (existingBill != null)
        {
            return (await GetBillByIdAsync(existingBill.Id))!;
        }

        int billCount = await _unitOfWork.Bills.CountAsync();
        string prefix = string.IsNullOrWhiteSpace(setting.InvoicePrefix) ? "INV" : setting.InvoicePrefix;
        string invoiceNo = $"{prefix}-{(billCount + 10001)}";

        decimal subtotal = order.SubTotal;
        decimal discount = dto.DiscountAmount;
        decimal serviceCharge = dto.ServiceCharge;
        decimal taxableSubtotal = Math.Max(0, subtotal - discount + serviceCharge);

        decimal cgst = 0m;
        decimal sgst = 0m;
        if (setting.IsGSTEnabled)
        {
            cgst = Math.Round(taxableSubtotal * (setting.CGSTPercentage / 100m), 2);
            sgst = Math.Round(taxableSubtotal * (setting.SGSTPercentage / 100m), 2);
        }
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

        if (dto.PaymentMethod == PaymentMethod.Split && dto.SplitCashAmount > 0 && dto.SplitOnlineAmount > 0)
        {
            var pCash = new Payment
            {
                BillId = dto.BillId,
                PaymentMethod = PaymentMethod.Cash,
                Amount = dto.SplitCashAmount,
                PaymentStatus = PaymentStatus.Paid,
                TransactionId = string.IsNullOrEmpty(dto.TransactionId) ? $"CASH-{Guid.NewGuid().ToString().Substring(0, 6).ToUpper()}" : $"{dto.TransactionId}-CASH",
                PaymentDate = DateTime.UtcNow,
                Notes = dto.Notes
            };
            var pOnline = new Payment
            {
                BillId = dto.BillId,
                PaymentMethod = PaymentMethod.UPI,
                Amount = dto.SplitOnlineAmount,
                PaymentStatus = PaymentStatus.Paid,
                TransactionId = string.IsNullOrEmpty(dto.TransactionId) ? $"UPI-{Guid.NewGuid().ToString().Substring(0, 6).ToUpper()}" : $"{dto.TransactionId}-UPI",
                PaymentDate = DateTime.UtcNow,
                Notes = dto.Notes
            };
            await _unitOfWork.Payments.AddAsync(pCash);
            await _unitOfWork.Payments.AddAsync(pOnline);
        }
        else
        {
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
        }

        // Customer Credit (Udhari) Record
        if (dto.PaymentMethod == PaymentMethod.Credit)
        {
            var ord = await _unitOfWork.Orders.GetByIdAsync(bill.OrderId);
            Customer? cust = null;

            if (dto.CustomerId.HasValue && dto.CustomerId.Value > 0)
            {
                cust = await _unitOfWork.Customers.GetByIdAsync(dto.CustomerId.Value);
            }
            else if (ord?.CustomerId.HasValue == true)
            {
                cust = await _unitOfWork.Customers.GetByIdAsync(ord.CustomerId.Value);
            }
            else if (!string.IsNullOrWhiteSpace(dto.NewCustomerName))
            {
                if (!string.IsNullOrWhiteSpace(dto.NewCustomerPhone))
                {
                    var found = await _unitOfWork.Customers.FindAsync(c => c.Phone == dto.NewCustomerPhone.Trim() && !c.IsDeleted);
                    cust = found.FirstOrDefault();
                }

                if (cust == null)
                {
                    cust = new Customer
                    {
                        Name = dto.NewCustomerName.Trim(),
                        Phone = dto.NewCustomerPhone?.Trim() ?? string.Empty,
                        OutstandingCredit = 0m,
                        CreatedAt = DateTime.UtcNow
                    };
                    await _unitOfWork.Customers.AddAsync(cust);
                    await _unitOfWork.SaveChangesAsync();
                }
            }

            if (cust != null)
            {
                cust.OutstandingCredit += dto.Amount;
                cust.UpdatedAt = DateTime.UtcNow;
                await _unitOfWork.Customers.UpdateAsync(cust);

                if (ord != null && !ord.CustomerId.HasValue)
                {
                    ord.CustomerId = cust.Id;
                    ord.CustomerName = cust.Name;
                    ord.CustomerPhone = cust.Phone;
                    await _unitOfWork.Orders.UpdateAsync(ord);
                }

                await _unitOfWork.CustomerLedgers.AddAsync(new CustomerLedger
                {
                    CustomerId = cust.Id,
                    Date = DateTime.UtcNow,
                    Description = $"Udhari on Invoice #{bill.InvoiceNumber}",
                    DebitAmount = dto.Amount,
                    CreditAmount = 0m,
                    RunningBalance = cust.OutstandingCredit,
                    ReferenceInvoiceNumber = bill.InvoiceNumber,
                    Notes = dto.Notes ?? "Customer credit settlement",
                    RecordedBy = "Cashier",
                    CreatedAt = DateTime.UtcNow
                });
            }
        }

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

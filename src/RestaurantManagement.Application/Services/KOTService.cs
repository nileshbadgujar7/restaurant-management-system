using RestaurantManagement.Application.DTOs;
using RestaurantManagement.Application.Interfaces;
using RestaurantManagement.Domain.Enums;

namespace RestaurantManagement.Application.Services;

public class KOTService : IKOTService
{
    private readonly IUnitOfWork _unitOfWork;

    public KOTService(IUnitOfWork unitOfWork)
    {
        _unitOfWork = unitOfWork;
    }

    public async Task<IEnumerable<KOTDto>> GetActiveKOTsAsync()
    {
        var kots = await _unitOfWork.KOTs.GetAllAsync();
        var items = await _unitOfWork.KOTItems.GetAllAsync();

        var itemDict = items.GroupBy(i => i.KOTId).ToDictionary(g => g.Key, g => g.ToList());

        return kots.Where(k => !k.IsDeleted && k.Status != KOTStatus.Served)
            .OrderByDescending(k => k.CreatedAt)
            .Select(k => new KOTDto
            {
                Id = k.Id,
                KOTNumber = k.KOTNumber,
                OrderId = k.OrderId,
                TableNumber = k.TableNumber,
                WaiterName = k.WaiterName,
                Status = k.Status,
                Notes = k.Notes,
                Type = k.Type,
                Station = k.Station,
                IsRush = k.IsRush,
                PrintFailed = k.PrintFailed,
                AcknowledgedByChef = k.AcknowledgedByChef,
                Reason = k.Reason,
                CreatedAt = k.CreatedAt,
                Items = itemDict.TryGetValue(k.Id, out var itemList) ? itemList.Select(i => new KOTItemDto
                {
                    Id = i.Id,
                    MenuItemName = i.MenuItemName,
                    Quantity = i.Quantity,
                    SpecialInstructions = i.SpecialInstructions,
                    Status = i.Status,
                    OrderItemId = i.OrderItemId,
                    Course = i.Course,
                    IsCancelled = i.IsCancelled,
                    Station = i.Station
                }).ToList() : new List<KOTItemDto>()
            });
    }

    public async Task<KOTDto?> GetKOTByIdAsync(int id)
    {
        var kot = await _unitOfWork.KOTs.GetByIdAsync(id);
        if (kot == null || kot.IsDeleted) return null;

        var items = await _unitOfWork.KOTItems.FindAsync(i => i.KOTId == id);

        return new KOTDto
        {
            Id = kot.Id,
            KOTNumber = kot.KOTNumber,
            OrderId = kot.OrderId,
            TableNumber = kot.TableNumber,
            WaiterName = kot.WaiterName,
            Status = kot.Status,
            Notes = kot.Notes,
            Type = kot.Type,
            Station = kot.Station,
            IsRush = kot.IsRush,
            PrintFailed = kot.PrintFailed,
            AcknowledgedByChef = kot.AcknowledgedByChef,
            Reason = kot.Reason,
            CreatedAt = kot.CreatedAt,
            Items = items.Select(i => new KOTItemDto
            {
                Id = i.Id,
                MenuItemName = i.MenuItemName,
                Quantity = i.Quantity,
                SpecialInstructions = i.SpecialInstructions,
                Status = i.Status,
                OrderItemId = i.OrderItemId,
                Course = i.Course,
                IsCancelled = i.IsCancelled,
                Station = i.Station
            }).ToList()
        };
    }

    public async Task<KOTDto> UpdateKOTStatusAsync(int kotId, KOTStatus status)
    {
        var kot = await _unitOfWork.KOTs.GetByIdAsync(kotId);
        if (kot == null) throw new KeyNotFoundException($"KOT {kotId} not found");

        kot.Status = status;
        kot.UpdatedAt = DateTime.UtcNow;

        var items = (await _unitOfWork.KOTItems.FindAsync(i => i.KOTId == kotId)).ToList();
        foreach (var item in items)
        {
            item.Status = status;
            item.UpdatedAt = DateTime.UtcNow;
            await _unitOfWork.KOTItems.UpdateAsync(item);
        }

        await _unitOfWork.KOTs.UpdateAsync(kot);

        // Update corresponding Order Status (only for active, un-settled orders)
        var order = await _unitOfWork.Orders.GetByIdAsync(kot.OrderId);
        if (order != null && !order.IsSettled && order.Status != OrderStatus.Completed && order.Status != OrderStatus.Cancelled)
        {
            if (status == KOTStatus.Preparing) order.Status = OrderStatus.Preparing;
            else if (status == KOTStatus.Ready) order.Status = OrderStatus.Ready;
            await _unitOfWork.Orders.UpdateAsync(order);
        }

        await _unitOfWork.SaveChangesAsync();

        return (await GetKOTByIdAsync(kotId))!;
    }
}

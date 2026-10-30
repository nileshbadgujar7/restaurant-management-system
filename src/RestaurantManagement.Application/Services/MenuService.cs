using RestaurantManagement.Application.DTOs;
using RestaurantManagement.Application.Interfaces;
using RestaurantManagement.Domain.Entities;

namespace RestaurantManagement.Application.Services;

public class MenuService : IMenuService
{
    private readonly IUnitOfWork _unitOfWork;

    public MenuService(IUnitOfWork unitOfWork)
    {
        _unitOfWork = unitOfWork;
    }

    public async Task<IEnumerable<CategoryDto>> GetCategoriesAsync()
    {
        var categories = await _unitOfWork.Categories.GetAllAsync();
        var menuItems = await _unitOfWork.MenuItems.GetAllAsync();

        var activeItems = menuItems.Where(m => !m.IsDeleted).ToList();

        return categories.Where(c => !c.IsDeleted).OrderBy(c => c.DisplayOrder).Select(c => new CategoryDto
        {
            Id = c.Id,
            Name = c.Name,
            NameMarathi = c.NameMarathi,
            Description = c.Description,
            DisplayOrder = c.DisplayOrder,
            IsActive = c.IsActive,
            ImageUrl = c.ImageUrl,
            ItemCount = activeItems.Count(m => m.CategoryId == c.Id)
        });
    }

    public async Task<CategoryDto?> GetCategoryByIdAsync(int id)
    {
        var category = await _unitOfWork.Categories.GetByIdAsync(id);
        if (category == null || category.IsDeleted) return null;

        var items = await _unitOfWork.MenuItems.FindAsync(m => m.CategoryId == id && !m.IsDeleted);

        return new CategoryDto
        {
            Id = category.Id,
            Name = category.Name,
            NameMarathi = category.NameMarathi,
            Description = category.Description,
            DisplayOrder = category.DisplayOrder,
            IsActive = category.IsActive,
            ImageUrl = category.ImageUrl,
            ItemCount = items.Count(),
            MenuItems = items.Select(m => MapToMenuItemDto(m, category.Name)).ToList()
        };
    }

    public async Task<CategoryDto> CreateCategoryAsync(CategoryDto dto)
    {
        var category = new Category
        {
            Name = dto.Name.Trim(),
            NameMarathi = dto.NameMarathi?.Trim() ?? string.Empty,
            Description = dto.Description ?? string.Empty,
            DisplayOrder = dto.DisplayOrder,
            IsActive = dto.IsActive,
            ImageUrl = dto.ImageUrl ?? string.Empty
        };

        await _unitOfWork.Categories.AddAsync(category);
        await _unitOfWork.SaveChangesAsync();

        dto.Id = category.Id;
        return dto;
    }

    public async Task<CategoryDto> UpdateCategoryAsync(int id, CategoryDto dto)
    {
        var category = await _unitOfWork.Categories.GetByIdAsync(id);
        if (category == null || category.IsDeleted) throw new KeyNotFoundException($"Category with id {id} not found.");

        category.Name = dto.Name.Trim();
        category.NameMarathi = dto.NameMarathi?.Trim() ?? string.Empty;
        category.Description = dto.Description ?? string.Empty;
        category.DisplayOrder = dto.DisplayOrder;
        category.IsActive = dto.IsActive;
        category.ImageUrl = dto.ImageUrl ?? string.Empty;
        category.UpdatedAt = DateTime.UtcNow;

        await _unitOfWork.Categories.UpdateAsync(category);
        await _unitOfWork.SaveChangesAsync();

        dto.Id = category.Id;
        return dto;
    }

    public async Task<bool> DeleteCategoryAsync(int id)
    {
        var category = await _unitOfWork.Categories.GetByIdAsync(id);
        if (category == null || category.IsDeleted) return false;

        category.IsDeleted = true;
        category.UpdatedAt = DateTime.UtcNow;
        await _unitOfWork.Categories.UpdateAsync(category);
        await _unitOfWork.SaveChangesAsync();
        return true;
    }

    public async Task<IEnumerable<MenuItemDto>> GetMenuItemsAsync(int? categoryId = null, bool? isVeg = null, string? search = null)
    {
        var items = await _unitOfWork.MenuItems.GetAllAsync();
        var categories = (await _unitOfWork.Categories.GetAllAsync()).ToDictionary(c => c.Id);
        var variants = (await _unitOfWork.MenuItemVariants.GetAllAsync()).GroupBy(v => v.MenuItemId).ToDictionary(g => g.Key, g => g.ToList());

        var query = items.Where(i => !i.IsDeleted);

        if (categoryId.HasValue) query = query.Where(i => i.CategoryId == categoryId.Value);
        if (isVeg.HasValue) query = query.Where(i => i.IsVeg == isVeg.Value);
        if (!string.IsNullOrWhiteSpace(search))
        {
            var s = search.ToLower();
            query = query.Where(i => i.Name.ToLower().Contains(s) || i.Description.ToLower().Contains(s));
        }

        return query.Select(i => MapToMenuItemDto(i, categories.TryGetValue(i.CategoryId, out var c) ? c.Name : "", variants.TryGetValue(i.Id, out var vList) ? vList : null));
    }

    public async Task<MenuItemDto?> GetMenuItemByIdAsync(int id)
    {
        var item = await _unitOfWork.MenuItems.GetByIdAsync(id);
        if (item == null || item.IsDeleted) return null;

        var category = await _unitOfWork.Categories.GetByIdAsync(item.CategoryId);
        var variants = await _unitOfWork.MenuItemVariants.FindAsync(v => v.MenuItemId == id && !v.IsDeleted);

        return MapToMenuItemDto(item, category?.Name ?? "", variants.ToList());
    }

    public async Task<MenuItemDto> CreateMenuItemAsync(MenuItemCreateUpdateDto dto)
    {
        var item = new MenuItem
        {
            CategoryId = dto.CategoryId,
            Name = dto.Name.Trim(),
            NameMarathi = dto.NameMarathi?.Trim() ?? string.Empty,
            ItemCode = dto.ItemCode?.Trim() ?? string.Empty,
            ShortCode = dto.ShortCode?.Trim() ?? string.Empty,
            Description = dto.Description ?? string.Empty,
            Price = dto.Price,
            TaxPercentage = dto.TaxPercentage,
            IsVeg = dto.IsVeg,
            ImageUrl = dto.ImageUrl ?? string.Empty,
            IsAvailable = dto.IsAvailable,
            IsActive = dto.IsActive,
            KitchenCategory = string.IsNullOrWhiteSpace(dto.KitchenCategory) ? "Main Kitchen" : dto.KitchenCategory.Trim(),
            PreparationTimeMinutes = dto.PreparationTimeMinutes
        };

        await _unitOfWork.MenuItems.AddAsync(item);
        await _unitOfWork.SaveChangesAsync();

        var category = await _unitOfWork.Categories.GetByIdAsync(dto.CategoryId);
        return MapToMenuItemDto(item, category?.Name ?? "");
    }

    public async Task<MenuItemDto> UpdateMenuItemAsync(int id, MenuItemCreateUpdateDto dto)
    {
        var item = await _unitOfWork.MenuItems.GetByIdAsync(id);
        if (item == null) throw new KeyNotFoundException($"Menu item {id} not found");

        item.CategoryId = dto.CategoryId;
        item.Name = dto.Name.Trim();
        item.NameMarathi = dto.NameMarathi?.Trim() ?? string.Empty;
        item.ItemCode = dto.ItemCode?.Trim() ?? string.Empty;
        item.ShortCode = dto.ShortCode?.Trim() ?? string.Empty;
        item.Description = dto.Description ?? string.Empty;
        item.Price = dto.Price;
        item.TaxPercentage = dto.TaxPercentage;
        item.IsVeg = dto.IsVeg;
        item.ImageUrl = dto.ImageUrl ?? string.Empty;
        item.IsAvailable = dto.IsAvailable;
        item.IsActive = dto.IsActive;
        item.KitchenCategory = string.IsNullOrWhiteSpace(dto.KitchenCategory) ? "Main Kitchen" : dto.KitchenCategory.Trim();
        item.PreparationTimeMinutes = dto.PreparationTimeMinutes;
        item.UpdatedAt = DateTime.UtcNow;

        await _unitOfWork.MenuItems.UpdateAsync(item);
        await _unitOfWork.SaveChangesAsync();

        var category = await _unitOfWork.Categories.GetByIdAsync(dto.CategoryId);
        return MapToMenuItemDto(item, category?.Name ?? "");
    }

    public async Task<bool> ToggleItemAvailabilityAsync(int id)
    {
        var item = await _unitOfWork.MenuItems.GetByIdAsync(id);
        if (item == null) return false;

        item.IsAvailable = !item.IsAvailable;
        item.UpdatedAt = DateTime.UtcNow;
        await _unitOfWork.MenuItems.UpdateAsync(item);
        await _unitOfWork.SaveChangesAsync();
        return true;
    }

    public async Task<bool> DeleteMenuItemAsync(int id)
    {
        var item = await _unitOfWork.MenuItems.GetByIdAsync(id);
        if (item == null) return false;

        item.IsDeleted = true;
        await _unitOfWork.MenuItems.UpdateAsync(item);
        await _unitOfWork.SaveChangesAsync();
        return true;
    }

    private static MenuItemDto MapToMenuItemDto(MenuItem m, string categoryName, List<MenuItemVariant>? variants = null)
    {
        return new MenuItemDto
        {
            Id = m.Id,
            CategoryId = m.CategoryId,
            CategoryName = categoryName,
            Name = m.Name,
            NameMarathi = m.NameMarathi,
            ItemCode = m.ItemCode,
            ShortCode = m.ShortCode,
            Description = m.Description,
            Price = m.Price,
            TaxPercentage = m.TaxPercentage,
            IsVeg = m.IsVeg,
            ImageUrl = m.ImageUrl,
            IsAvailable = m.IsAvailable,
            IsActive = m.IsActive,
            KitchenCategory = m.KitchenCategory,
            PreparationTimeMinutes = m.PreparationTimeMinutes,
            Variants = variants?.Select(v => new MenuItemVariantDto
            {
                Id = v.Id,
                MenuItemId = v.MenuItemId,
                VariantName = v.VariantName,
                Price = v.Price
            }).ToList() ?? new List<MenuItemVariantDto>()
        };
    }
}

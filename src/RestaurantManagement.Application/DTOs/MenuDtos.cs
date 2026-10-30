namespace RestaurantManagement.Application.DTOs;

public class CategoryDto
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string NameMarathi { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public int DisplayOrder { get; set; }
    public bool IsActive { get; set; } = true;
    public string ImageUrl { get; set; } = string.Empty;
    public int ItemCount { get; set; }
    public List<MenuItemDto> MenuItems { get; set; } = new();
}

public class MenuItemDto
{
    public int Id { get; set; }
    public int CategoryId { get; set; }
    public string CategoryName { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string NameMarathi { get; set; } = string.Empty;
    public string ItemCode { get; set; } = string.Empty;
    public string ShortCode { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public decimal Price { get; set; }
    public decimal TaxPercentage { get; set; }
    public bool IsVeg { get; set; }
    public string ImageUrl { get; set; } = string.Empty;
    public bool IsAvailable { get; set; }
    public bool IsActive { get; set; } = true;
    public string KitchenCategory { get; set; } = "Main Kitchen";
    public int PreparationTimeMinutes { get; set; }
    public List<MenuItemVariantDto> Variants { get; set; } = new();
    public List<AddOnDto> AddOns { get; set; } = new();
}

public class MenuItemVariantDto
{
    public int Id { get; set; }
    public int MenuItemId { get; set; }
    public string VariantName { get; set; } = string.Empty;
    public decimal Price { get; set; }
}

public class AddOnDto
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public decimal Price { get; set; }
    public bool IsAvailable { get; set; }
}

public class MenuItemCreateUpdateDto
{
    public int CategoryId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string NameMarathi { get; set; } = string.Empty;
    public string ItemCode { get; set; } = string.Empty;
    public string ShortCode { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public decimal Price { get; set; }
    public decimal TaxPercentage { get; set; } = 5.0m;
    public bool IsVeg { get; set; } = true;
    public string ImageUrl { get; set; } = string.Empty;
    public bool IsAvailable { get; set; } = true;
    public bool IsActive { get; set; } = true;
    public string KitchenCategory { get; set; } = "Main Kitchen";
    public int PreparationTimeMinutes { get; set; } = 15;
}

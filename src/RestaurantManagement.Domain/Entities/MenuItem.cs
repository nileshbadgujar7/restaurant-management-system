namespace RestaurantManagement.Domain.Entities;

public class MenuItem : BaseEntity
{
    public int CategoryId { get; set; }
    public Category? Category { get; set; }
    public string Name { get; set; } = string.Empty;
    public string NameMarathi { get; set; } = string.Empty;
    public string ItemCode { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public decimal Price { get; set; }
    public decimal TaxPercentage { get; set; } = 5.0m;
    public bool IsVeg { get; set; } = true;
    public string ImageUrl { get; set; } = string.Empty;
    public bool IsAvailable { get; set; } = true;
    public string KitchenCategory { get; set; } = "Main Kitchen";
    public int PreparationTimeMinutes { get; set; } = 15;
    
    public ICollection<MenuItemVariant> Variants { get; set; } = new List<MenuItemVariant>();
    public ICollection<MenuItemAddOn> MenuItemAddOns { get; set; } = new List<MenuItemAddOn>();
    public Recipe? Recipe { get; set; }
}

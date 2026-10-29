namespace RestaurantManagement.Domain.Entities;

public class Category : BaseEntity
{
    public string Name { get; set; } = string.Empty;
    public string NameMarathi { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public int DisplayOrder { get; set; } = 1;
    public string ImageUrl { get; set; } = string.Empty;
    public ICollection<MenuItem> MenuItems { get; set; } = new List<MenuItem>();
}

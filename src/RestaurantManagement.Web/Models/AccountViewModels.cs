using System.ComponentModel.DataAnnotations;

namespace RestaurantManagement.Web.Models;

public class LoginViewModel
{
    [Required(ErrorMessage = "Please enter your username")]
    public string Username { get; set; } = "manager";

    [Required(ErrorMessage = "Please enter your password")]
    [DataType(DataType.Password)]
    public string Password { get; set; } = "manager123";

    public bool RememberMe { get; set; } = true;
    public string? ReturnUrl { get; set; }

    // Lean Branding & Mode Info
    public string RestaurantName { get; set; } = "Girana Garden Fine Dine & Bar";
    public string RestaurantNameMarathi { get; set; } = string.Empty;
    public string LogoUrl { get; set; } = string.Empty;
    public string KitchenModeName { get; set; } = "Mode 2: Printer Only";

    // Flash Messages
    public string? ErrorMessage { get; set; }
    public string? SuccessMessage { get; set; }
}

public class ForgotPasswordDto
{
    [Required(ErrorMessage = "Username is required")]
    public string Username { get; set; } = string.Empty;

    [Required(ErrorMessage = "Manager Security PIN is required")]
    public string ManagerPin { get; set; } = string.Empty;

    [Required(ErrorMessage = "New password is required")]
    [MinLength(4, ErrorMessage = "Password must be at least 4 characters long")]
    public string NewPassword { get; set; } = string.Empty;

    [Required(ErrorMessage = "Please confirm your new password")]
    [Compare(nameof(NewPassword), ErrorMessage = "Passwords do not match")]
    public string ConfirmPassword { get; set; } = string.Empty;
}

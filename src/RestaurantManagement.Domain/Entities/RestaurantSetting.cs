using RestaurantManagement.Domain.Enums;

namespace RestaurantManagement.Domain.Entities;

public class RestaurantSetting : BaseEntity
{
    public string RestaurantName { get; set; } = "Girana Garden Fine Dine";
    public string Address { get; set; } = "101 Grand Galleria, MG Road, Ahmedabad";
    public string Phone { get; set; } = "+91 98765 43210";
    public string Email { get; set; } = "contact@giranagarden.com";
    public string GSTIN { get; set; } = "24AAACG1234F1Z9";
    public string CurrencySymbol { get; set; } = "₹";
    public decimal CGSTPercentage { get; set; } = 2.5m;
    public decimal SGSTPercentage { get; set; } = 2.5m;
    public decimal ServiceChargePercentage { get; set; } = 0.0m;
    public string LogoUrl { get; set; } = "/images/logo.png";
    public string InvoicePrefix { get; set; } = "INV";

    // Small-restaurant settings (Section 2.2)
    public KitchenMode KitchenMode { get; set; } = KitchenMode.PrinterOnly; // Default: Mode 2
    public bool TrackReadyStatus { get; set; } = false; // Mode 2/3 (Default: Off)
    public int AutoMarkServedMinutes { get; set; } = 0; // Off=0, or 5 to 120 minutes (Default: Off)
    public VoidApprovalSetting VoidApproval { get; set; } = VoidApprovalSetting.ManagerPIN; // Default: Manager PIN
    public BillingUnservedItemsSetting AllowBillingWithUnservedItems { get; set; } = BillingUnservedItemsSetting.Ask; // Default: Ask
    public bool KitchenLiteScreen { get; set; } = false; // Default: Off
    public bool SinglePersonMode { get; set; } = false; // Default: Off
    public int DraftExpiryHours { get; set; } = 4; // Default: 4 hours
    public decimal PackagingCharge { get; set; } = 15.0m;
    public decimal DeliveryCharge { get; set; } = 30.0m;
    public string ManagerPIN { get; set; } = "1234";

    // Centralized Configuration Masters (Section 5.6)
    public bool IsGSTEnabled { get; set; } = true;
    public bool IsTaxInclusive { get; set; } = false;
    public bool IsKOTWorkflowEnabled { get; set; } = true; // Mode A (true) vs Mode B (false)
    public bool IsQRCodeEnabled { get; set; } = true;
    public string QRCodeData { get; set; } = "https://giranagarden.com/menu";
    public int ThermalPrinterWidth { get; set; } = 80; // 58mm or 80mm
    public string WhatsAppApiKey { get; set; } = "";
    public string EmailSmtpHost { get; set; } = "";
    public int EmailSmtpPort { get; set; } = 587;
    public string EmailSmtpUser { get; set; } = "";
    public string EmailSmtpPassword { get; set; } = "";
    public string DefaultLanguage { get; set; } = "en"; // "en" or "mr"
}


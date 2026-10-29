namespace RestaurantManagement.Domain.Enums;

public enum UserRole
{
    Admin = 1,
    Manager = 2,
    Cashier = 3,
    Waiter = 4,
    KitchenStaff = 5,
    InventoryStaff = 6,
    Cook = 7,
    Housekeeping = 8
}

public enum TableStatus
{
    Available = 1,
    Occupied = 2,
    Reserved = 3,
    Billing = 4,
    Cleaning = 5,
    OutOfService = 6
}

public enum OrderType
{
    DineIn = 1,
    Takeaway = 2,
    Delivery = 3
}

public enum OrderStatus
{
    Pending = 1,
    Confirmed = 2,
    Preparing = 3,
    Ready = 4,
    Completed = 5,
    Cancelled = 6
}

public enum KOTStatus
{
    New = 1,
    Accepted = 2,
    Preparing = 3,
    Ready = 4,
    Served = 5
}

public enum PaymentMethod
{
    Cash = 1,
    UPI = 2,
    CreditCard = 3,
    DebitCard = 4,
    Wallet = 5,
    Split = 6,
    Credit = 7 // Customer Credit / Udhari
}

public enum PaymentStatus
{
    Pending = 1,
    PartiallyPaid = 2,
    Paid = 3,
    Refunded = 4,
    Failed = 5
}

public enum ReservationStatus
{
    Pending = 1,
    Confirmed = 2,
    Seated = 3,
    Completed = 4,
    Cancelled = 5,
    NoShow = 6
}

public enum LoyaltyTier
{
    Silver = 1,
    Gold = 2,
    Platinum = 3
}

public enum KitchenMode
{
    FullKDS = 1,     // Mode 1: Full KDS (Screen in kitchen)
    PrinterOnly = 2, // Mode 2: Printer only (Thermal printer, no screen)
    NoDevice = 3     // Mode 3: No kitchen device (Cafe/counter slip)
}

public enum OrderItemStatus
{
    Pending = 1,     // Section B: New item (not yet sent to kitchen)
    Sent = 2,        // Section A: Sent to kitchen (cooking not started / waiting)
    Preparing = 3,   // Mode 1 only: Chef cooking
    Ready = 4,       // Mode 1 or Mode 2 (if tracked): Ready for pickup
    Served = 5,      // Served to guest
    Cancelled = 6    // Cancelled / Voided
}

public enum KOTType
{
    Initial = 1,     // First KOT for order
    Addition = 2,    // ADDITION KOT for new items
    Cancellation = 3,// Cancel slip (STOP: CANCELLED)
    NoteChange = 4,  // NOTE CHANGE slip
    Remake = 5       // REMAKE slip (no charge)
}

public enum VoidApprovalSetting
{
    ManagerPIN = 1,
    AnySignedInUser = 2,
    Off = 3
}

public enum BillingUnservedItemsSetting
{
    Yes = 1,
    Ask = 2,
    No = 3
}


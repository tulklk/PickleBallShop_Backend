namespace PickleBall.Domain.Entities;

public enum Role
{
    Customer = 1,
    Admin = 2
}

public enum PaymentStatus
{
    Pending = 1,
    Paid = 2,
    Failed = 3,
    Refunded = 4
}

public enum OrderStatus
{
    Pending = 1,
    Confirmed = 2,
    Shipping = 3,
    Completed = 4,
    Cancelled = 5
}

public enum PaymentMethod
{
    COD = 0,
    MoMo = 1,
    VNPay = 2
}

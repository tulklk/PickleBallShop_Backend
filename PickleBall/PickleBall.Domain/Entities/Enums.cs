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
    Processing = 1,        // Đang xử lý
    Confirmed = 2,         // Đã xác nhận
    Prepared = 3,          // Đã chuẩn bị hàng
    Shipped = 4,           // Đã giao cho đơn vị vận chuyển
    Completed = 5,        // Hoàn thành
    Cancelled = 6         // Đã hủy
}

public enum PaymentMethod
{
    COD = 0,
    MoMo = 1,
    VNPay = 2,
    BankTransfer = 3
}

public enum VoucherDiscountType
{
    Percent = 1,
    Amount = 2
}

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
    MoMo = 1,
    VNPay = 2,
    COD = 3
}

public enum ProductType
{
    Ball = 1,
    Paddle = 2,
    Net = 3,
    Apparel = 4,
    Shoe = 5,
    Accessory = 6,
    TrainingAid = 7
}

public enum SkillLevel
{
    Beginner = 1,
    Intermediate = 2,
    Advanced = 3
}

public enum WeightRange
{
    Light = 1,
    Midweight = 2,
    Heavy = 3
}

public enum SurfaceType
{
    Indoor = 1,
    Outdoor = 2,
    IndoorOutdoor = 3
}

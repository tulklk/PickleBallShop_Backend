using System.Linq.Expressions;
using PickleBall.Application.DTOs.Cart;
using PickleBall.Application.DTOs.News;
using PickleBall.Application.DTOs.Orders;
using PickleBall.Application.DTOs.Products;
using PickleBall.Application.DTOs.Reviews;
using PickleBall.Application.DTOs.Vouchers;
using PickleBall.Application.DTOs.Wishlist;
using PickleBall.Domain.Entities;
using AuthUserDto = PickleBall.Application.DTOs.Auth.UserDto;
using UsersUserDto = PickleBall.Application.DTOs.Users.UserDto;

namespace PickleBall.Application.Mapping;

/// <summary>
/// Explicit entity -> DTO mappings. Written by hand (no reflection / no third-party mapper) so that every
/// field exposed by the API is a deliberate decision: add a property to a DTO here, never by convention.
/// </summary>
public static class DtoMappers
{
    // ---------------------------------------------------------------- users
    public static AuthUserDto ToAuthUserDto(User u) => new()
    {
        Id = u.Id,
        Email = u.Email,
        FullName = u.FullName,
        PhoneNumber = u.PhoneNumber,
        Avatar = u.Avatar,
        Role = u.Role.ToString(),
        EmailVerified = u.EmailVerified
    };

    public static UsersUserDto ToUserDto(User u) => new()
    {
        Id = u.Id,
        Email = u.Email,
        FullName = u.FullName,
        PhoneNumber = u.PhoneNumber,
        Avatar = u.Avatar,
        Role = u.Role.ToString(),
        IsActive = u.IsActive,
        EmailVerified = u.EmailVerified,
        CreatedAt = u.CreatedAt,
        UpdatedAt = u.UpdatedAt
    };

    // ------------------------------------------------------------- products
    public static ProductVariantDto ToVariantDto(ProductVariant v) => new()
    {
        Id = v.Id,
        ProductId = v.ProductId,
        Color = v.Color,
        Size = v.Size,
        Spec = v.Spec,
        Price = v.Price,
        OriginalPrice = v.OriginalPrice,
        StockQuantity = v.StockQuantity,
        IsActive = v.IsActive
    };

    public static ProductSpecificationDto ToSpecificationDto(ProductSpecification s) => new()
    {
        Id = s.Id,
        ProductId = s.ProductId,
        Key = s.Key,
        Value = s.Value,
        DisplayOrder = s.DisplayOrder
    };

    public static ProductSpecification ToSpecification(CreateProductSpecificationRequest r) => new()
    {
        Key = r.Key,
        Value = r.Value,
        DisplayOrder = r.DisplayOrder
    };

    public static ProductSpecification ToSpecification(UpdateProductSpecificationRequest r) => new()
    {
        Key = r.Key,
        Value = r.Value,
        DisplayOrder = r.DisplayOrder
    };

    public static void Apply(UpdateProductSpecificationRequest r, ProductSpecification s)
    {
        s.Key = r.Key;
        s.Value = r.Value;
        s.DisplayOrder = r.DisplayOrder;
    }

    public static string? PrimaryImageUrl(IEnumerable<ProductImage>? images)
    {
        if (images == null)
        {
            return null;
        }

        var list = images as IList<ProductImage> ?? images.ToList();
        return (list.FirstOrDefault(i => i.IsPrimary) ?? list.FirstOrDefault())?.ImageUrl;
    }

    public static ProductDto ToProductDto(Product p) => new()
    {
        Id = p.Id,
        Name = p.Name,
        Slug = p.Slug,
        Description = p.Description,
        CategoryId = p.CategoryId,
        CategoryName = p.Category?.Name ?? string.Empty,
        Price = p.Price,
        OriginalPrice = p.OriginalPrice,
        StockQuantity = p.StockQuantity,
        Brand = p.Brand,
        IsActive = p.IsActive,
        ImageUrls = p.Images.Select(i => i.ImageUrl).ToList(),
        PrimaryImageUrl = PrimaryImageUrl(p.Images),
        AverageRating = p.Reviews.Any() ? p.Reviews.Average(r => r.Rating) : null,
        ReviewCount = p.Reviews.Count,
        HasVariants = p.HasVariants,
        Variants = p.Variants.Where(v => v.IsActive).Select(ToVariantDto).ToList(),
        Specifications = p.Specifications.OrderBy(s => s.DisplayOrder).Select(ToSpecificationDto).ToList(),
        CreatedAt = p.CreatedAt
    };

    // ----------------------------------------------------------------- cart
    public static string? VariantInfo(ProductVariant? v)
    {
        if (v == null)
        {
            return null;
        }

        return string.Join(", ", new[]
        {
            !string.IsNullOrWhiteSpace(v.Color) ? $"Màu: {v.Color}" : null,
            !string.IsNullOrWhiteSpace(v.Size) ? $"Size: {v.Size}" : null,
            !string.IsNullOrWhiteSpace(v.Spec) ? $"Thông số: {v.Spec}" : null
        }.Where(x => x != null));
    }

    public static CartItemDto ToCartItemDto(CartItem ci) => new()
    {
        Id = ci.Id,
        ProductId = ci.ProductId,
        ProductVariantId = ci.ProductVariantId,
        ProductName = ci.Product?.Name ?? string.Empty,
        ProductImageUrl = PrimaryImageUrl(ci.Product?.Images),
        VariantInfo = VariantInfo(ci.ProductVariant),
        UnitPrice = ci.UnitPriceSnapshot,
        Quantity = ci.Quantity,
        LineTotal = ci.UnitPriceSnapshot * ci.Quantity
    };

    public static CartDto ToCartDto(Cart c) => new()
    {
        Id = c.Id,
        UserId = c.UserId,
        Items = c.Items.Select(ToCartItemDto).ToList(),
        SubTotal = c.Items.Sum(i => i.UnitPriceSnapshot * i.Quantity),
        TotalItems = c.Items.Sum(i => i.Quantity)
    };

    // ---------------------------------------------------------------- orders
    private static ShippingAddressDto ToShippingAddress(Order o) => new()
    {
        FullName = o.ShippingFullName,
        PhoneNumber = o.ShippingPhoneNumber,
        AddressLine = o.ShippingAddressLine,
        Ward = o.ShippingWard,
        District = o.ShippingDistrict,
        City = o.ShippingCity
    };

    public static OrderItemDto ToOrderItemDto(OrderItem i) => new()
    {
        Id = i.Id,
        ProductId = i.ProductId,
        ProductVariantId = i.ProductVariantId,
        ProductName = i.ProductNameSnapshot,
        VariantInfo = i.VariantInfoSnapshot,
        UnitPrice = i.UnitPrice,
        Quantity = i.Quantity,
        LineTotal = i.LineTotal
    };

    public static OrderDto ToOrderDto(Order o) => new()
    {
        Id = o.Id,
        OrderNumber = o.OrderNumber,
        UserId = o.UserId,
        TotalAmount = o.TotalAmount,
        PaymentStatus = o.PaymentStatus.ToString(),
        OrderStatus = o.OrderStatus.ToString(),
        PaymentMethod = o.PaymentMethod?.ToString(),
        GuestEmail = o.GuestEmail,
        GuestFullName = o.GuestFullName,
        ShippingAddress = ToShippingAddress(o),
        Items = o.Items.Select(ToOrderItemDto).ToList(),
        CreatedAt = o.CreatedAt,
        UpdatedAt = o.UpdatedAt
    };

    public static AdminOrderDto ToAdminOrderDto(Order o) => new()
    {
        Id = o.Id,
        OrderNumber = o.OrderNumber,
        UserId = o.UserId,
        User = o.User == null
            ? null
            : new UserInfoDto
            {
                Id = o.User.Id,
                Email = o.User.Email,
                FullName = o.User.FullName ?? string.Empty,
                PhoneNumber = o.User.PhoneNumber
            },
        GuestEmail = o.GuestEmail,
        GuestFullName = o.GuestFullName,
        TotalAmount = o.TotalAmount,
        PaymentStatus = o.PaymentStatus.ToString(),
        OrderStatus = o.OrderStatus.ToString(),
        PaymentMethod = o.PaymentMethod?.ToString(),
        PaymentTransactionId = o.PaymentTransactionId,
        ShippingAddress = ToShippingAddress(o),
        Items = o.Items.Select(ToOrderItemDto).ToList(),
        CreatedAt = o.CreatedAt,
        UpdatedAt = o.UpdatedAt
    };

    // -------------------------------------------------------------- wishlist
    public static WishlistItemDto ToWishlistItemDto(Wishlist w) => new()
    {
        Id = w.Id,
        ProductId = w.ProductId,
        ProductName = w.Product?.Name ?? string.Empty,
        ProductDescription = w.Product?.Description,
        ProductImageUrl = PrimaryImageUrl(w.Product?.Images),
        Price = w.Product?.Price ?? 0,
        OriginalPrice = w.Product?.OriginalPrice,
        IsActive = w.Product?.IsActive ?? false,
        HasVariants = w.Product?.HasVariants ?? false,
        AddedAt = w.CreatedAt
    };

    // --------------------------------------------------------------- reviews
    /// <summary>Public review representation. Reviewer phone/email are intentionally NOT exposed.</summary>
    public static ReviewDto ToReviewDto(ProductReview r) => new()
    {
        Id = r.Id,
        ProductId = r.ProductId,
        UserId = r.UserId,
        FullName = r.FullName,
        Rating = r.Rating,
        Comment = r.Comment,
        ReviewImageUrl = r.ReviewImageUrl,
        CreatedAt = r.CreatedAt,
        UpdatedAt = r.UpdatedAt
    };

    // ------------------------------------------------------------------ news
    public static NewsDto ToNewsDto(NewsPost n) => new()
    {
        Id = n.Id,
        Title = n.Title,
        Slug = n.Slug,
        Content = n.Content,
        Summary = n.Summary,
        ThumbnailUrl = n.ThumbnailUrl,
        AuthorId = n.AuthorId,
        AuthorName = n.Author?.FullName ?? string.Empty,
        Category = n.Category,
        Tags = n.Tags,
        IsPublished = n.IsPublished,
        PublishedAt = n.PublishedAt,
        ViewCount = n.ViewCount,
        CreatedAt = n.CreatedAt,
        UpdatedAt = n.UpdatedAt
    };

    // -------------------------------------------------------------- vouchers
    /// <summary>EF-translatable projection (used with IQueryable.Select).</summary>
    public static readonly Expression<Func<Voucher, VoucherDto>> VoucherProjection = v => new VoucherDto
    {
        Id = v.Id,
        Code = v.Code,
        Name = v.Name,
        Description = v.Description,
        DiscountType = v.DiscountType,
        DiscountValue = v.DiscountValue,
        MaxDiscountAmount = v.MaxDiscountAmount,
        MinOrderAmount = v.MinOrderAmount,
        StartDate = v.StartDate,
        EndDate = v.EndDate,
        IsPublic = v.IsPublic,
        UsageLimitTotal = v.UsageLimitTotal,
        UsageLimitPerCustomer = v.UsageLimitPerCustomer,
        UsedCount = v.UsedCount
    };

    private static readonly Func<Voucher, VoucherDto> CompiledVoucherProjection = VoucherProjection.Compile();

    public static VoucherDto ToVoucherDto(Voucher v) => CompiledVoucherProjection(v);

    public static Voucher ToVoucher(CreateVoucherRequest r) => new()
    {
        Code = r.Code,
        Name = r.Name,
        Description = r.Description,
        DiscountType = r.DiscountType,
        DiscountValue = r.DiscountValue,
        MaxDiscountAmount = r.MaxDiscountAmount,
        MinOrderAmount = r.MinOrderAmount,
        StartDate = r.StartDate,
        EndDate = r.EndDate,
        IsPublic = r.IsPublic,
        UsageLimitTotal = r.UsageLimitTotal,
        UsageLimitPerCustomer = r.UsageLimitPerCustomer
    };

    public static void Apply(UpdateVoucherRequest r, Voucher v)
    {
        v.Name = r.Name;
        v.Description = r.Description;
        v.DiscountType = r.DiscountType;
        v.DiscountValue = r.DiscountValue;
        v.MaxDiscountAmount = r.MaxDiscountAmount;
        v.MinOrderAmount = r.MinOrderAmount;
        v.StartDate = r.StartDate;
        v.EndDate = r.EndDate;
        v.IsPublic = r.IsPublic;
        v.UsageLimitTotal = r.UsageLimitTotal;
        v.UsageLimitPerCustomer = r.UsageLimitPerCustomer;
    }
}

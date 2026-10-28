using PickleBall.Application.DTOs.Cart;
using PickleBall.Application.DTOs.News;
using PickleBall.Application.DTOs.Orders;
using PickleBall.Application.DTOs.Products;
using PickleBall.Application.DTOs.Reviews;
using PickleBall.Application.DTOs.Vouchers;
using PickleBall.Application.DTOs.Wishlist;
using PickleBall.Application.Mapping;
using PickleBall.Domain.Entities;
using AuthUserDto = PickleBall.Application.DTOs.Auth.UserDto;
using UsersUserDto = PickleBall.Application.DTOs.Users.UserDto;

namespace PickleBall.Tests;

public class MappingTests
{
    private readonly IMapper _mapper = new AppMapper();

    private static User NewUser() => new()
    {
        Email = "a@example.test",
        FullName = "A",
        PasswordHash = "SECRET-HASH",
        PasswordResetTokenHash = "SECRET-RESET",
        GoogleId = "g-1",
        Role = Role.Admin,
        EmailVerified = true
    };

    private static Product NewProduct()
    {
        var product = new Product
        {
            Name = "Paddle",
            Slug = "paddle",
            Price = 100,
            Category = new ProductCategory { Name = "Paddles" },
            HasVariants = true
        };
        product.Images.Add(new ProductImage { ImageUrl = "https://img/1.png" });
        product.Images.Add(new ProductImage { ImageUrl = "https://img/2.png", IsPrimary = true });
        product.Variants.Add(new ProductVariant { Color = "Red", Spec = "13mm", Price = 110, IsActive = true });
        product.Variants.Add(new ProductVariant { Color = "Gone", Price = 1, IsActive = false });
        product.Specifications.Add(new ProductSpecification { Key = "b", Value = "2", DisplayOrder = 2 });
        product.Specifications.Add(new ProductSpecification { Key = "a", Value = "1", DisplayOrder = 1 });
        product.Reviews.Add(new ProductReview { Rating = 5 });
        product.Reviews.Add(new ProductReview { Rating = 3 });
        return product;
    }

    [Fact]
    public void User_dtos_never_contain_secrets_and_stringify_the_role()
    {
        var user = NewUser();

        var auth = _mapper.Map<AuthUserDto>(user);
        var full = _mapper.Map<UsersUserDto>(user);

        Assert.Equal("Admin", auth.Role);
        Assert.Equal("Admin", full.Role);
        Assert.True(auth.EmailVerified);

        foreach (var dto in new object[] { auth, full })
        {
            var json = System.Text.Json.JsonSerializer.Serialize(dto, dto.GetType());
            Assert.DoesNotContain("SECRET", json);
            Assert.DoesNotContain("g-1", json);
        }

        var list = _mapper.Map<List<UsersUserDto>>(new List<User> { user, NewUser() });
        Assert.Equal(2, list.Count);
    }

    [Fact]
    public void Product_mapping_prefers_primary_image_filters_inactive_variants_and_orders_specs()
    {
        var dto = _mapper.Map<ProductDto>(NewProduct());

        Assert.Equal("Paddles", dto.CategoryName);
        Assert.Equal("https://img/2.png", dto.PrimaryImageUrl);
        Assert.Equal(2, dto.ImageUrls.Count);
        Assert.Equal(4.0, dto.AverageRating);
        Assert.Equal(2, dto.ReviewCount);
        var variant = Assert.Single(dto.Variants);
        Assert.Equal("Red", variant.Color);
        Assert.Equal(new[] { "a", "b" }, dto.Specifications.Select(s => s.Key));
    }

    [Fact]
    public void Product_without_images_or_reviews_maps_cleanly()
    {
        var dto = _mapper.Map<ProductDto>(new Product { Name = "x", Category = new ProductCategory { Name = "c" } });

        Assert.Null(dto.PrimaryImageUrl);
        Assert.Null(dto.AverageRating);
        Assert.Equal(0, dto.ReviewCount);
        Assert.Empty(dto.Variants);
    }

    [Fact]
    public void Cart_items_carry_price_snapshot_and_variant_info()
    {
        var product = NewProduct();
        var cart = new Cart { UserId = Guid.NewGuid() };
        cart.Items.Add(new CartItem
        {
            Product = product,
            ProductId = product.Id,
            ProductVariant = new ProductVariant { Color = "Red", Size = "42", Spec = "13mm" },
            Quantity = 3,
            UnitPriceSnapshot = 50
        });

        var dto = _mapper.Map<CartDto>(cart);

        var item = Assert.Single(dto.Items);
        Assert.Equal("Paddle", item.ProductName);
        Assert.Equal("https://img/2.png", item.ProductImageUrl);
        Assert.Equal("Màu: Red, Size: 42, Thông số: 13mm", item.VariantInfo);
        Assert.Equal(50, item.UnitPrice);
        Assert.Equal(150, item.LineTotal);
        Assert.Equal(150, dto.SubTotal);
        Assert.Equal(3, dto.TotalItems);

        var single = _mapper.Map<CartItemDto>(cart.Items.First());
        Assert.Equal(150, single.LineTotal);
    }

    private static Order NewOrder(bool guest)
    {
        var order = new Order
        {
            OrderNumber = "PB-1",
            UserId = guest ? null : Guid.NewGuid(),
            User = guest ? null : NewUser(),
            GuestEmail = guest ? "g@example.test" : null,
            TotalAmount = 300,
            PaymentStatus = PaymentStatus.Pending,
            OrderStatus = OrderStatus.Processing,
            PaymentMethod = PaymentMethod.BankTransfer,
            ShippingFullName = "N",
            ShippingPhoneNumber = "0900000000",
            ShippingAddressLine = "1 Street",
            ShippingCity = "HCM"
        };
        order.Items.Add(new OrderItem
        {
            ProductNameSnapshot = "Paddle",
            VariantInfoSnapshot = "Màu: Red",
            UnitPrice = 100,
            Quantity = 3,
            LineTotal = 300
        });
        return order;
    }

    [Fact]
    public void Order_mapping_exposes_enums_as_strings_and_snapshots_as_item_names()
    {
        var dto = _mapper.Map<OrderDto>(NewOrder(guest: true));

        Assert.Equal("Pending", dto.PaymentStatus);
        Assert.Equal("Processing", dto.OrderStatus);
        Assert.Equal("BankTransfer", dto.PaymentMethod);
        Assert.Equal("1 Street", dto.ShippingAddress.AddressLine);
        var item = Assert.Single(dto.Items);
        Assert.Equal("Paddle", item.ProductName);
        Assert.Equal("Màu: Red", item.VariantInfo);
        Assert.Equal(300, item.LineTotal);
    }

    [Fact]
    public void Admin_order_mapping_includes_user_info_for_registered_and_flags_guest_orders()
    {
        var registered = _mapper.Map<AdminOrderDto>(NewOrder(guest: false));
        Assert.NotNull(registered.User);
        Assert.False(registered.IsGuestOrder);
        var json = System.Text.Json.JsonSerializer.Serialize(registered);
        Assert.DoesNotContain("SECRET", json);

        var guest = _mapper.Map<AdminOrderDto>(NewOrder(guest: true));
        Assert.Null(guest.User);
        Assert.True(guest.IsGuestOrder);

        Assert.Equal(2, _mapper.Map<List<AdminOrderDto>>(new List<Order> { NewOrder(true), NewOrder(false) }).Count);
        Assert.Single(_mapper.Map<List<OrderDto>>(new[] { NewOrder(true) }.ToList()));
    }

    [Fact]
    public void Review_mapping_never_exposes_reviewer_phone_or_email()
    {
        var review = new ProductReview
        {
            FullName = "R",
            PhoneNumber = "0911111111",
            Email = "reviewer@example.test",
            Rating = 4,
            Comment = "nice paddle indeed"
        };

        var dto = _mapper.Map<ReviewDto>(review);
        var json = System.Text.Json.JsonSerializer.Serialize(dto);

        Assert.Equal(4, dto.Rating);
        Assert.DoesNotContain("0911111111", json);
        Assert.DoesNotContain("reviewer@example.test", json);
        Assert.Single(_mapper.Map<List<ReviewDto>>(new List<ProductReview> { review }));
    }

    [Fact]
    public void Wishlist_news_and_voucher_mappings_copy_the_expected_fields()
    {
        var product = NewProduct();
        var wish = _mapper.Map<WishlistItemDto>(new Wishlist { Product = product, ProductId = product.Id });
        Assert.Equal("Paddle", wish.ProductName);
        Assert.Equal("https://img/2.png", wish.ProductImageUrl);
        Assert.Equal(100, wish.Price);
        Assert.True(wish.HasVariants);

        var news = _mapper.Map<NewsDto>(new NewsPost
        {
            Title = "t",
            Author = new User { FullName = "Writer" },
            Category = "c"
        });
        Assert.Equal("Writer", news.AuthorName);

        var voucher = _mapper.Map<Voucher>(new CreateVoucherRequest
        {
            Code = "SAVE10",
            Name = "n",
            DiscountType = VoucherDiscountType.Percent,
            DiscountValue = 10,
            UsageLimitTotal = 5
        });
        Assert.Equal("SAVE10", voucher.Code);
        Assert.Equal(0, voucher.UsedCount);

        _mapper.Map(new UpdateVoucherRequest { Name = "renamed", DiscountValue = 20, UsageLimitTotal = null }, voucher);
        Assert.Equal("renamed", voucher.Name);
        Assert.Equal(20, voucher.DiscountValue);
        Assert.Null(voucher.UsageLimitTotal);
        Assert.Equal("SAVE10", voucher.Code); // code is immutable on update

        var dto = _mapper.Map<VoucherDto>(voucher);
        Assert.Equal("SAVE10", dto.Code);
    }

    [Fact]
    public void Specification_mappings_roundtrip()
    {
        var spec = _mapper.Map<ProductSpecification>(new CreateProductSpecificationRequest { Key = "k", Value = "v", DisplayOrder = 3 });
        Assert.Equal("k", spec.Key);

        _mapper.Map(new UpdateProductSpecificationRequest { Key = "k2", Value = "v2", DisplayOrder = 4 }, spec);
        var dto = _mapper.Map<ProductSpecificationDto>(spec);
        Assert.Equal(("k2", "v2", 4), (dto.Key, dto.Value, dto.DisplayOrder));
    }

    [Fact]
    public void Unknown_mappings_fail_loudly()
    {
        Assert.Throws<InvalidOperationException>(() => _mapper.Map<ProductDto>(new Voucher()));
    }
}


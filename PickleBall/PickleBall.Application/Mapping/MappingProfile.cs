using AutoMapper;
using PickleBall.Application.DTOs.Auth;
using PickleBall.Application.DTOs.Cart;
using PickleBall.Application.DTOs.Categories;
using PickleBall.Application.DTOs.News;
using PickleBall.Application.DTOs.Orders;
using PickleBall.Application.DTOs.Products;
using PickleBall.Application.DTOs.Reviews;
using PickleBall.Application.DTOs.Users;
using PickleBall.Application.DTOs.Wishlist;
using PickleBall.Domain.Entities;

namespace PickleBall.Application.Mapping;

public class MappingProfile : Profile
{
    public MappingProfile()
    {
        // User mapping (Auth namespace)
        CreateMap<User, DTOs.Auth.UserDto>()
            .ForMember(dest => dest.Role, opt => opt.MapFrom(src => src.Role.ToString()));

        // User mapping (Users namespace)
        CreateMap<User, DTOs.Users.UserDto>()
            .ForMember(dest => dest.Role, opt => opt.MapFrom(src => src.Role.ToString()));

        // ProductVariant mapping
        CreateMap<ProductVariant, ProductVariantDto>();

        // Product mapping
        CreateMap<Product, ProductDto>()
            .ForMember(dest => dest.CategoryName, opt => opt.MapFrom(src => src.Category.Name))
            .ForMember(dest => dest.ImageUrls, opt => opt.MapFrom(src => src.Images.Select(i => i.ImageUrl).ToList()))
            .ForMember(dest => dest.PrimaryImageUrl, opt => opt.MapFrom(src => src.Images.FirstOrDefault(i => i.IsPrimary) != null ? src.Images.FirstOrDefault(i => i.IsPrimary)!.ImageUrl : src.Images.FirstOrDefault() != null ? src.Images.FirstOrDefault()!.ImageUrl : null))
            .ForMember(dest => dest.AverageRating, opt => opt.MapFrom(src => src.Reviews.Any() ? src.Reviews.Average(r => r.Rating) : (double?)null))
            .ForMember(dest => dest.ReviewCount, opt => opt.MapFrom(src => src.Reviews.Count))
            .ForMember(dest => dest.Variants, opt => opt.MapFrom(src => src.Variants.Where(v => v.IsActive).ToList()));

        CreateMap<Product, ProductListDto>()
            .ForMember(dest => dest.PrimaryImageUrl, opt => opt.MapFrom(src => src.Images.FirstOrDefault(i => i.IsPrimary) != null ? src.Images.FirstOrDefault(i => i.IsPrimary)!.ImageUrl : src.Images.FirstOrDefault() != null ? src.Images.FirstOrDefault()!.ImageUrl : null))
            .ForMember(dest => dest.AverageRating, opt => opt.MapFrom(src => src.Reviews.Any() ? src.Reviews.Average(r => r.Rating) : (double?)null))
            .ForMember(dest => dest.ReviewCount, opt => opt.MapFrom(src => src.Reviews.Count));

        // Category mapping
        CreateMap<ProductCategory, CategoryDto>()
            .ForMember(dest => dest.ProductCount, opt => opt.MapFrom(src => src.Products.Count(p => p.IsActive)));

        // Cart mapping
        CreateMap<Cart, CartDto>();
        
        CreateMap<CartItem, CartItemDto>()
            .ForMember(dest => dest.ProductName, opt => opt.MapFrom(src => src.Product.Name))
            .ForMember(dest => dest.ProductImageUrl, opt => opt.MapFrom(src => src.Product.Images.FirstOrDefault(i => i.IsPrimary) != null ? src.Product.Images.FirstOrDefault(i => i.IsPrimary)!.ImageUrl : src.Product.Images.FirstOrDefault() != null ? src.Product.Images.FirstOrDefault()!.ImageUrl : null))
            .ForMember(dest => dest.VariantInfo, opt => opt.MapFrom(src => src.ProductVariant != null 
                ? string.Join(", ", new[] { 
                    !string.IsNullOrWhiteSpace(src.ProductVariant.Color) ? $"Màu: {src.ProductVariant.Color}" : null,
                    !string.IsNullOrWhiteSpace(src.ProductVariant.Size) ? $"Size: {src.ProductVariant.Size}" : null,
                    !string.IsNullOrWhiteSpace(src.ProductVariant.Spec) ? $"Thông số: {src.ProductVariant.Spec}" : null
                }.Where(x => x != null))
                : null))
            .ForMember(dest => dest.LineTotal, opt => opt.MapFrom(src => src.UnitPriceSnapshot * src.Quantity));

        // Order mapping
        CreateMap<Order, OrderDto>()
            .ForMember(dest => dest.PaymentStatus, opt => opt.MapFrom(src => src.PaymentStatus.ToString()))
            .ForMember(dest => dest.OrderStatus, opt => opt.MapFrom(src => src.OrderStatus.ToString()))
            .ForMember(dest => dest.PaymentMethod, opt => opt.MapFrom(src => src.PaymentMethod.HasValue ? src.PaymentMethod.Value.ToString() : null))
            .ForMember(dest => dest.ShippingAddress, opt => opt.MapFrom(src => new ShippingAddressDto
            {
                FullName = src.ShippingFullName,
                PhoneNumber = src.ShippingPhoneNumber,
                AddressLine = src.ShippingAddressLine,
                Ward = src.ShippingWard,
                District = src.ShippingDistrict,
                City = src.ShippingCity
            }));

        CreateMap<OrderItem, OrderItemDto>()
            .ForMember(dest => dest.ProductName, opt => opt.MapFrom(src => src.ProductNameSnapshot))
            .ForMember(dest => dest.VariantInfo, opt => opt.MapFrom(src => src.VariantInfoSnapshot));

        // Admin Order mapping with user info
        CreateMap<Order, AdminOrderDto>()
            .ForMember(dest => dest.PaymentStatus, opt => opt.MapFrom(src => src.PaymentStatus.ToString()))
            .ForMember(dest => dest.OrderStatus, opt => opt.MapFrom(src => src.OrderStatus.ToString()))
            .ForMember(dest => dest.PaymentMethod, opt => opt.MapFrom(src => src.PaymentMethod.HasValue ? src.PaymentMethod.Value.ToString() : null))
            .ForMember(dest => dest.User, opt => opt.MapFrom(src => src.User != null ? new DTOs.Orders.UserInfoDto
            {
                Id = src.User.Id,
                Email = src.User.Email,
                FullName = src.User.FullName ?? string.Empty,
                PhoneNumber = src.User.PhoneNumber
            } : null))
            .ForMember(dest => dest.ShippingAddress, opt => opt.MapFrom(src => new ShippingAddressDto
            {
                FullName = src.ShippingFullName,
                PhoneNumber = src.ShippingPhoneNumber,
                AddressLine = src.ShippingAddressLine,
                Ward = src.ShippingWard,
                District = src.ShippingDistrict,
                City = src.ShippingCity
            }));

        // Wishlist mapping
        CreateMap<Wishlist, WishlistItemDto>()
            .ForMember(dest => dest.ProductId, opt => opt.MapFrom(src => src.ProductId))
            .ForMember(dest => dest.ProductName, opt => opt.MapFrom(src => src.Product.Name))
            .ForMember(dest => dest.ProductDescription, opt => opt.MapFrom(src => src.Product.Description))
            .ForMember(dest => dest.ProductImageUrl, opt => opt.MapFrom(src => src.Product.Images.FirstOrDefault(i => i.IsPrimary) != null 
                ? src.Product.Images.FirstOrDefault(i => i.IsPrimary)!.ImageUrl 
                : src.Product.Images.FirstOrDefault() != null 
                    ? src.Product.Images.FirstOrDefault()!.ImageUrl 
                    : null))
            .ForMember(dest => dest.Price, opt => opt.MapFrom(src => src.Product.Price))
            .ForMember(dest => dest.OriginalPrice, opt => opt.MapFrom(src => src.Product.OriginalPrice))
            .ForMember(dest => dest.IsActive, opt => opt.MapFrom(src => src.Product.IsActive))
            .ForMember(dest => dest.HasVariants, opt => opt.MapFrom(src => src.Product.HasVariants))
            .ForMember(dest => dest.AddedAt, opt => opt.MapFrom(src => src.CreatedAt));

        // ProductReview mapping
        CreateMap<ProductReview, ReviewDto>();

        // News mapping
        CreateMap<NewsPost, NewsDto>()
            .ForMember(dest => dest.AuthorName, opt => opt.MapFrom(src => src.Author.FullName));
        
        CreateMap<NewsPost, NewsListDto>()
            .ForMember(dest => dest.AuthorName, opt => opt.MapFrom(src => src.Author.FullName));
    }
}

using AutoMapper;
using PickleBall.Application.DTOs.Auth;
using PickleBall.Application.DTOs.Cart;
using PickleBall.Application.DTOs.Categories;
using PickleBall.Application.DTOs.Orders;
using PickleBall.Application.DTOs.Products;
using PickleBall.Domain.Entities;

namespace PickleBall.Application.Mapping;

public class MappingProfile : Profile
{
    public MappingProfile()
    {
        // User mapping
        CreateMap<User, UserDto>()
            .ForMember(dest => dest.Role, opt => opt.MapFrom(src => src.Role.ToString()));

        // Product mapping
        CreateMap<Product, ProductDto>()
            .ForMember(dest => dest.CategoryName, opt => opt.MapFrom(src => src.Category.Name))
            .ForMember(dest => dest.ImageUrls, opt => opt.MapFrom(src => src.Images.Select(i => i.ImageUrl).ToList()))
            .ForMember(dest => dest.PrimaryImageUrl, opt => opt.MapFrom(src => src.Images.FirstOrDefault(i => i.IsPrimary) != null ? src.Images.FirstOrDefault(i => i.IsPrimary)!.ImageUrl : src.Images.FirstOrDefault() != null ? src.Images.FirstOrDefault()!.ImageUrl : null))
            .ForMember(dest => dest.AverageRating, opt => opt.MapFrom(src => src.Reviews.Any() ? src.Reviews.Average(r => r.Rating) : (double?)null))
            .ForMember(dest => dest.ReviewCount, opt => opt.MapFrom(src => src.Reviews.Count));

        CreateMap<Product, ProductListDto>()
            .ForMember(dest => dest.PrimaryImageUrl, opt => opt.MapFrom(src => src.Images.FirstOrDefault(i => i.IsPrimary) != null ? src.Images.FirstOrDefault(i => i.IsPrimary)!.ImageUrl : src.Images.FirstOrDefault() != null ? src.Images.FirstOrDefault()!.ImageUrl : null))
            .ForMember(dest => dest.AverageRating, opt => opt.MapFrom(src => src.Reviews.Any() ? src.Reviews.Average(r => r.Rating) : (double?)null))
            .ForMember(dest => dest.ReviewCount, opt => opt.MapFrom(src => src.Reviews.Count));

        // Category mapping
        CreateMap<ProductCategory, CategoryDto>()
            .ForMember(dest => dest.ParentCategoryName, opt => opt.MapFrom(src => src.ParentCategory != null ? src.ParentCategory.Name : null))
            .ForMember(dest => dest.ProductCount, opt => opt.MapFrom(src => src.Products.Count(p => p.IsActive)))
            .ForMember(dest => dest.SubCategories, opt => opt.MapFrom(src => src.SubCategories));

        // Cart mapping
        CreateMap<Cart, CartDto>();
        
        CreateMap<CartItem, CartItemDto>()
            .ForMember(dest => dest.ProductName, opt => opt.MapFrom(src => src.Product.Name))
            .ForMember(dest => dest.ProductImageUrl, opt => opt.MapFrom(src => src.Product.Images.FirstOrDefault(i => i.IsPrimary) != null ? src.Product.Images.FirstOrDefault(i => i.IsPrimary)!.ImageUrl : src.Product.Images.FirstOrDefault() != null ? src.Product.Images.FirstOrDefault()!.ImageUrl : null))
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

        CreateMap<OrderItem, OrderItemDto>();
    }
}

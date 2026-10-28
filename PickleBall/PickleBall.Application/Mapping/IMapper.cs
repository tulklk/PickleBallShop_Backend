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

/// <summary>Minimal mapping facade over <see cref="DtoMappers"/> (replaces AutoMapper).</summary>
public interface IMapper
{
    TDestination Map<TDestination>(object source);
    TDestination Map<TSource, TDestination>(TSource source, TDestination destination);
}

public sealed class AppMapper : IMapper
{
    private static readonly Dictionary<(Type Source, Type Destination), Func<object, object>> Maps = new()
    {
        [(typeof(User), typeof(AuthUserDto))] = s => DtoMappers.ToAuthUserDto((User)s),
        [(typeof(User), typeof(UsersUserDto))] = s => DtoMappers.ToUserDto((User)s),
        [(typeof(Product), typeof(ProductDto))] = s => DtoMappers.ToProductDto((Product)s),
        [(typeof(ProductVariant), typeof(ProductVariantDto))] = s => DtoMappers.ToVariantDto((ProductVariant)s),
        [(typeof(ProductSpecification), typeof(ProductSpecificationDto))] = s => DtoMappers.ToSpecificationDto((ProductSpecification)s),
        [(typeof(CreateProductSpecificationRequest), typeof(ProductSpecification))] = s => DtoMappers.ToSpecification((CreateProductSpecificationRequest)s),
        [(typeof(UpdateProductSpecificationRequest), typeof(ProductSpecification))] = s => DtoMappers.ToSpecification((UpdateProductSpecificationRequest)s),
        [(typeof(Cart), typeof(CartDto))] = s => DtoMappers.ToCartDto((Cart)s),
        [(typeof(CartItem), typeof(CartItemDto))] = s => DtoMappers.ToCartItemDto((CartItem)s),
        [(typeof(Order), typeof(OrderDto))] = s => DtoMappers.ToOrderDto((Order)s),
        [(typeof(Order), typeof(AdminOrderDto))] = s => DtoMappers.ToAdminOrderDto((Order)s),
        [(typeof(Wishlist), typeof(WishlistItemDto))] = s => DtoMappers.ToWishlistItemDto((Wishlist)s),
        [(typeof(ProductReview), typeof(ReviewDto))] = s => DtoMappers.ToReviewDto((ProductReview)s),
        [(typeof(NewsPost), typeof(NewsDto))] = s => DtoMappers.ToNewsDto((NewsPost)s),
        [(typeof(Voucher), typeof(VoucherDto))] = s => DtoMappers.ToVoucherDto((Voucher)s),
        [(typeof(CreateVoucherRequest), typeof(Voucher))] = s => DtoMappers.ToVoucher((CreateVoucherRequest)s),
    };

    private static readonly Dictionary<(Type Source, Type Destination), Action<object, object>> Updates = new()
    {
        [(typeof(UpdateVoucherRequest), typeof(Voucher))] = (s, d) => DtoMappers.Apply((UpdateVoucherRequest)s, (Voucher)d),
        [(typeof(UpdateProductSpecificationRequest), typeof(ProductSpecification))] =
            (s, d) => DtoMappers.Apply((UpdateProductSpecificationRequest)s, (ProductSpecification)d),
    };

    public TDestination Map<TDestination>(object source)
    {
        if (source is null)
        {
            return default!;
        }

        return (TDestination)MapObject(source, typeof(TDestination));
    }

    public TDestination Map<TSource, TDestination>(TSource source, TDestination destination)
    {
        if (source is null || destination is null)
        {
            return destination;
        }

        if (!Updates.TryGetValue((typeof(TSource), typeof(TDestination)), out var apply))
        {
            throw new InvalidOperationException($"No update mapping from {typeof(TSource).Name} to {typeof(TDestination).Name}.");
        }

        apply(source, destination);
        return destination;
    }

    private static object MapObject(object source, Type destination)
    {
        if (Maps.TryGetValue((source.GetType(), destination), out var map))
        {
            return map(source);
        }

        if (destination.IsGenericType &&
            destination.GetGenericTypeDefinition() == typeof(List<>) &&
            source is System.Collections.IEnumerable items)
        {
            var element = destination.GetGenericArguments()[0];
            var list = (System.Collections.IList)Activator.CreateInstance(destination)!;
            foreach (var item in items)
            {
                list.Add(MapObject(item, element));
            }

            return list;
        }

        throw new InvalidOperationException($"No mapping from {source.GetType().Name} to {destination.Name}.");
    }
}

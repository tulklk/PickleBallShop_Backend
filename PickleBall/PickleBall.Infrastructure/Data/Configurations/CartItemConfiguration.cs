using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PickleBall.Domain.Entities;

namespace PickleBall.Infrastructure.Data.Configurations;

public class CartItemConfiguration : IEntityTypeConfiguration<CartItem>
{
    public void Configure(EntityTypeBuilder<CartItem> builder)
    {
        builder.HasKey(ci => ci.Id);
        builder.Property(ci => ci.Quantity).IsRequired();
        builder.Property(ci => ci.UnitPriceSnapshot).HasColumnType("decimal(18,2)").IsRequired();

        // Unique constraint: một variant của một product chỉ có thể có một lần trong một cart
        // Nếu không có variant, thì một product chỉ có thể có một lần trong một cart
        builder.HasIndex(ci => new { ci.CartId, ci.ProductId, ci.ProductVariantId }).IsUnique();
    }
}

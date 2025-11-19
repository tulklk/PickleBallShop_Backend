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

        builder.HasIndex(ci => new { ci.CartId, ci.ProductId }).IsUnique();
    }
}

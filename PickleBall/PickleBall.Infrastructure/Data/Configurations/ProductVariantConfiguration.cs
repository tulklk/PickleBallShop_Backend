using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PickleBall.Domain.Entities;

namespace PickleBall.Infrastructure.Data.Configurations;

public class ProductVariantConfiguration : IEntityTypeConfiguration<ProductVariant>
{
    public void Configure(EntityTypeBuilder<ProductVariant> builder)
    {
        builder.HasKey(pv => pv.Id);
        
        builder.Property(pv => pv.Color).HasMaxLength(100);
        builder.Property(pv => pv.Size).HasMaxLength(50);
        builder.Property(pv => pv.Spec).HasMaxLength(100);
        builder.Property(pv => pv.Price).HasColumnType("decimal(18,2)").IsRequired();
        builder.Property(pv => pv.OriginalPrice).HasColumnType("decimal(18,2)");
        builder.Property(pv => pv.StockQuantity).IsRequired().HasDefaultValue(0);
        builder.Property(pv => pv.SKU).IsRequired().HasMaxLength(100);
        builder.HasIndex(pv => pv.SKU).IsUnique();
        builder.Property(pv => pv.IsActive).HasDefaultValue(true);

        // Relationship with Product
        builder.HasOne(pv => pv.Product)
            .WithMany(p => p.Variants)
            .HasForeignKey(pv => pv.ProductId)
            .OnDelete(DeleteBehavior.Cascade);

        // Relationship with CartItem
        builder.HasMany(pv => pv.CartItems)
            .WithOne(ci => ci.ProductVariant)
            .HasForeignKey(ci => ci.ProductVariantId)
            .OnDelete(DeleteBehavior.SetNull);

        // Relationship with OrderItem
        builder.HasMany(pv => pv.OrderItems)
            .WithOne(oi => oi.ProductVariant)
            .HasForeignKey(oi => oi.ProductVariantId)
            .OnDelete(DeleteBehavior.SetNull);
    }
}


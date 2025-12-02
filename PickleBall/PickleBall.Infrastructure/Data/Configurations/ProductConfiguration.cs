using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PickleBall.Domain.Entities;

namespace PickleBall.Infrastructure.Data.Configurations;

public class ProductConfiguration : IEntityTypeConfiguration<Product>
{
    public void Configure(EntityTypeBuilder<Product> builder)
    {
        builder.HasKey(p => p.Id);
        builder.Property(p => p.Name).IsRequired().HasMaxLength(256);
        builder.Property(p => p.Description).HasMaxLength(5000);
        builder.Property(p => p.Price).HasColumnType("decimal(18,2)").IsRequired();
        builder.Property(p => p.OriginalPrice).HasColumnType("decimal(18,2)");
        builder.Property(p => p.StockQuantity).IsRequired().HasDefaultValue(0);
        builder.Property(p => p.SKU).IsRequired().HasMaxLength(100);
        builder.HasIndex(p => p.SKU).IsUnique();
        builder.Property(p => p.Brand).HasMaxLength(256);
        builder.Property(p => p.IsActive).HasDefaultValue(true);
        builder.Property(p => p.HasVariants).HasDefaultValue(false);

        builder.HasMany(p => p.Variants)
            .WithOne(pv => pv.Product)
            .HasForeignKey(pv => pv.ProductId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasMany(p => p.Images)
            .WithOne(pi => pi.Product)
            .HasForeignKey(pi => pi.ProductId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasMany(p => p.Reviews)
            .WithOne(pr => pr.Product)
            .HasForeignKey(pr => pr.ProductId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasMany(p => p.InventoryTransactions)
            .WithOne(it => it.Product)
            .HasForeignKey(it => it.ProductId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}

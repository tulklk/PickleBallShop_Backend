using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PickleBall.Domain.Entities;

namespace PickleBall.Infrastructure.Data.Configurations;

public class ProductCategoryConfiguration : IEntityTypeConfiguration<ProductCategory>
{
    public void Configure(EntityTypeBuilder<ProductCategory> builder)
    {
        builder.HasKey(pc => pc.Id);
        builder.Property(pc => pc.Name).IsRequired().HasMaxLength(256);
        builder.Property(pc => pc.Description).HasMaxLength(1000);

        builder.HasMany(pc => pc.Products)
            .WithOne(p => p.Category)
            .HasForeignKey(p => p.CategoryId)
            .OnDelete(DeleteBehavior.Restrict);

        // Self-referencing parent-child
        builder.HasMany(pc => pc.Children)
            .WithOne(pc => pc.Parent)
            .HasForeignKey(pc => pc.ParentId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}

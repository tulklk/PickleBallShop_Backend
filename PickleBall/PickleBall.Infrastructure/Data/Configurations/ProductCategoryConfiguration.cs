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

        builder.HasOne(pc => pc.ParentCategory)
            .WithMany(pc => pc.SubCategories)
            .HasForeignKey(pc => pc.ParentCategoryId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasMany(pc => pc.Products)
            .WithOne(p => p.Category)
            .HasForeignKey(p => p.CategoryId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}

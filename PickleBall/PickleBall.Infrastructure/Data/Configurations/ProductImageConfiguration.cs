using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PickleBall.Domain.Entities;

namespace PickleBall.Infrastructure.Data.Configurations;

public class ProductImageConfiguration : IEntityTypeConfiguration<ProductImage>
{
    public void Configure(EntityTypeBuilder<ProductImage> builder)
    {
        builder.HasKey(pi => pi.Id);
        builder.Property(pi => pi.ImageUrl).IsRequired().HasMaxLength(1000);
        builder.Property(pi => pi.IsPrimary).HasDefaultValue(false);
    }
}

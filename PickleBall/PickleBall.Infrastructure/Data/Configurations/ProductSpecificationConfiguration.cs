using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PickleBall.Domain.Entities;

namespace PickleBall.Infrastructure.Data.Configurations;

public class ProductSpecificationConfiguration : IEntityTypeConfiguration<ProductSpecification>
{
    public void Configure(EntityTypeBuilder<ProductSpecification> builder)
    {
        builder.HasKey(ps => ps.Id);

        builder.Property(ps => ps.Key)
            .IsRequired()
            .HasMaxLength(200);

        builder.Property(ps => ps.Value)
            .IsRequired()
            .HasMaxLength(500);

        builder.Property(ps => ps.DisplayOrder)
            .HasDefaultValue(0);

        builder.HasOne(ps => ps.Product)
            .WithMany(p => p.Specifications)
            .HasForeignKey(ps => ps.ProductId)
            .OnDelete(DeleteBehavior.Cascade);

        // Index để query nhanh hơn
        builder.HasIndex(ps => ps.ProductId);
        builder.HasIndex(ps => new { ps.ProductId, ps.DisplayOrder });
    }
}

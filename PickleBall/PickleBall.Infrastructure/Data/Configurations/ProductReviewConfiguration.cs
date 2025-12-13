using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PickleBall.Domain.Entities;

namespace PickleBall.Infrastructure.Data.Configurations;

public class ProductReviewConfiguration : IEntityTypeConfiguration<ProductReview>
{
    public void Configure(EntityTypeBuilder<ProductReview> builder)
    {
        builder.HasKey(pr => pr.Id);
        builder.Property(pr => pr.Rating).IsRequired();
        builder.ToTable(t => t.HasCheckConstraint("CK_ProductReview_Rating", "rating >= 1 AND rating <= 5"));
        builder.Property(pr => pr.Comment).HasMaxLength(2000);
        builder.Property(pr => pr.FullName).IsRequired().HasMaxLength(256);
        builder.Property(pr => pr.PhoneNumber).IsRequired().HasMaxLength(20);
        builder.Property(pr => pr.Email).HasMaxLength(256);
        builder.Property(pr => pr.ReviewImageUrl).HasMaxLength(1000);

        // Relationship with User is configured in UserConfiguration

        // Index chỉ áp dụng khi có UserId (để tránh user đánh giá nhiều lần)
        // Với anonymous reviews, không có unique constraint
        builder.HasIndex(pr => new { pr.ProductId, pr.UserId })
            .IsUnique()
            .HasFilter("\"UserId\" IS NOT NULL");
    }
}

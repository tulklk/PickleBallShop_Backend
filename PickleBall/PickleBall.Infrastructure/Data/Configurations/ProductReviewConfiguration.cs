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

        builder.HasIndex(pr => new { pr.ProductId, pr.UserId }).IsUnique();
    }
}

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PickleBall.Domain.Entities;

namespace PickleBall.Infrastructure.Data.Configurations;

public class ProductCommentConfiguration : IEntityTypeConfiguration<ProductComment>
{
    public void Configure(EntityTypeBuilder<ProductComment> builder)
    {
        builder.HasKey(pc => pc.Id);
        
        builder.Property(pc => pc.Content)
            .IsRequired()
            .HasMaxLength(2000);
        
        builder.Property(pc => pc.IsEdited)
            .HasDefaultValue(false);

        // Relationship: ProductComment -> Product
        builder.HasOne(pc => pc.Product)
            .WithMany(p => p.Comments)
            .HasForeignKey(pc => pc.ProductId)
            .OnDelete(DeleteBehavior.Cascade);

        // Relationship: ProductComment -> User
        builder.HasOne(pc => pc.User)
            .WithMany()
            .HasForeignKey(pc => pc.UserId)
            .OnDelete(DeleteBehavior.Restrict);

        // Self-referencing relationship: Parent comment -> Replies
        builder.HasOne(pc => pc.ParentComment)
            .WithMany(pc => pc.Replies)
            .HasForeignKey(pc => pc.ParentCommentId)
            .OnDelete(DeleteBehavior.Cascade);

        // Indexes
        builder.HasIndex(pc => pc.ProductId);
        builder.HasIndex(pc => pc.UserId);
        builder.HasIndex(pc => pc.ParentCommentId);
        builder.HasIndex(pc => pc.CreatedAt);
    }
}


using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PickleBall.Domain.Entities;

namespace PickleBall.Infrastructure.Data.Configurations;

public class NewsPostConfiguration : IEntityTypeConfiguration<NewsPost>
{
    public void Configure(EntityTypeBuilder<NewsPost> builder)
    {
        builder.HasKey(n => n.Id);
        
        builder.Property(n => n.Title)
            .IsRequired()
            .HasMaxLength(500);
        
        builder.Property(n => n.Slug)
            .IsRequired()
            .HasMaxLength(500);
        
        builder.HasIndex(n => n.Slug)
            .IsUnique();
        
        builder.Property(n => n.Content)
            .IsRequired()
            .HasMaxLength(50000); // Allow long content
        
        builder.Property(n => n.Summary)
            .HasMaxLength(1000);
        
        builder.Property(n => n.ThumbnailUrl)
            .IsRequired()
            .HasMaxLength(1000);
        
        builder.Property(n => n.Category)
            .IsRequired()
            .HasMaxLength(100);
        
        builder.Property(n => n.Tags)
            .HasMaxLength(500);
        
        builder.Property(n => n.IsPublished)
            .HasDefaultValue(false);
        
        builder.Property(n => n.ViewCount)
            .HasDefaultValue(0);
        
        // Relationship with User (Author)
        builder.HasOne(n => n.Author)
            .WithMany()
            .HasForeignKey(n => n.AuthorId)
            .OnDelete(DeleteBehavior.Restrict);
        
        // Index for performance
        builder.HasIndex(n => n.IsPublished);
        builder.HasIndex(n => n.Category);
        builder.HasIndex(n => n.PublishedAt);
    }
}


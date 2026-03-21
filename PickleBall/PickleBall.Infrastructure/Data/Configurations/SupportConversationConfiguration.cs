using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PickleBall.Domain.Entities;

namespace PickleBall.Infrastructure.Data.Configurations;

public class SupportConversationConfiguration : IEntityTypeConfiguration<SupportConversation>
{
    public void Configure(EntityTypeBuilder<SupportConversation> builder)
    {
        builder.HasKey(c => c.Id);

        builder.Property(c => c.LastMessagePreview)
            .HasMaxLength(500);

        builder.Property(c => c.CustomerUnreadCount)
            .HasDefaultValue(0);

        builder.Property(c => c.AdminUnreadCount)
            .HasDefaultValue(0);

        builder.HasOne(c => c.Customer)
            .WithMany()
            .HasForeignKey(c => c.CustomerId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(c => c.CustomerId)
            .IsUnique();

        builder.HasIndex(c => c.LastMessageAt);
    }
}

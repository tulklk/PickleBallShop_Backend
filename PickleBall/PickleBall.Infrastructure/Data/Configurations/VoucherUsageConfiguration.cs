using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PickleBall.Domain.Entities;

namespace PickleBall.Infrastructure.Data.Configurations;

public class VoucherUsageConfiguration : IEntityTypeConfiguration<VoucherUsage>
{
    public void Configure(EntityTypeBuilder<VoucherUsage> builder)
    {
        builder.HasKey(u => u.Id);

        builder.Property(u => u.GuestEmail)
            .HasMaxLength(256);

        builder.HasOne(u => u.Order)
            .WithMany()
            .HasForeignKey(u => u.OrderId)
            .OnDelete(DeleteBehavior.SetNull);

        builder.HasOne(u => u.User)
            .WithMany()
            .HasForeignKey(u => u.UserId)
            .OnDelete(DeleteBehavior.SetNull);

        builder.HasIndex(u => new { u.VoucherId, u.UserId });
        builder.HasIndex(u => new { u.VoucherId, u.GuestEmail });
    }
}


using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PickleBall.Domain.Entities;

namespace PickleBall.Infrastructure.Data.Configurations;

public class OrderConfiguration : IEntityTypeConfiguration<Order>
{
    public void Configure(EntityTypeBuilder<Order> builder)
    {
        builder.HasKey(o => o.Id);
        builder.Property(o => o.OrderNumber).IsRequired().HasMaxLength(100);
        builder.HasIndex(o => o.OrderNumber).IsUnique();
        builder.Property(o => o.TotalAmount).HasColumnType("decimal(18,2)").IsRequired();
        builder.Property(o => o.PaymentStatus).HasConversion<int>().IsRequired();
        builder.Property(o => o.OrderStatus).HasConversion<int>().IsRequired();
        builder.Property(o => o.PaymentMethod).HasConversion<int?>();
        builder.Property(o => o.PaymentTransactionId).HasMaxLength(256);

        // Guest checkout fields
        builder.Property(o => o.GuestEmail).HasMaxLength(256);
        builder.Property(o => o.GuestFullName).HasMaxLength(256);

        // Shipping address fields
        builder.Property(o => o.ShippingFullName).IsRequired().HasMaxLength(256);
        builder.Property(o => o.ShippingPhoneNumber).IsRequired().HasMaxLength(20);
        builder.Property(o => o.ShippingAddressLine).IsRequired().HasMaxLength(500);
        builder.Property(o => o.ShippingWard).HasMaxLength(100);
        builder.Property(o => o.ShippingDistrict).HasMaxLength(100);
        builder.Property(o => o.ShippingCity).HasMaxLength(100);

        // Voucher
        builder.Property(o => o.VoucherDiscountAmount).HasColumnType("decimal(18,2)");

        // UserId is nullable for guest checkout
        builder.Property(o => o.UserId).IsRequired(false);

        builder.HasMany(o => o.Items)
            .WithOne(oi => oi.Order)
            .HasForeignKey(oi => oi.OrderId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasMany(o => o.Payments)
            .WithOne(p => p.Order)
            .HasForeignKey(p => p.OrderId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(o => o.UserId);
        builder.HasIndex(o => o.OrderStatus);
        builder.HasIndex(o => o.PaymentStatus);
        builder.HasIndex(o => o.CreatedAt);
        builder.HasIndex(o => o.GuestEmail); // Index for guest email lookup
    }
}

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PickleBall.Domain.Entities;

namespace PickleBall.Infrastructure.Data.Configurations;

public class VoucherConfiguration : IEntityTypeConfiguration<Voucher>
{
    public void Configure(EntityTypeBuilder<Voucher> builder)
    {
        builder.HasKey(v => v.Id);

        builder.Property(v => v.Code)
            .IsRequired()
            .HasMaxLength(100);

        builder.HasIndex(v => v.Code)
            .IsUnique();

        builder.Property(v => v.Name)
            .IsRequired()
            .HasMaxLength(256);

        builder.Property(v => v.Description)
            .HasMaxLength(1000);

        builder.Property(v => v.DiscountType)
            .HasConversion<int>()
            .IsRequired();

        builder.Property(v => v.DiscountValue)
            .HasColumnType("decimal(18,2)")
            .IsRequired();

        builder.Property(v => v.MaxDiscountAmount)
            .HasColumnType("decimal(18,2)");

        builder.Property(v => v.MinOrderAmount)
            .HasColumnType("decimal(18,2)");

        builder.Property(v => v.StartDate)
            .IsRequired();

        builder.Property(v => v.EndDate)
            .IsRequired();

        builder.Property(v => v.IsPublic)
            .HasDefaultValue(false);

        builder.Property(v => v.UsageLimitTotal);
        builder.Property(v => v.UsageLimitPerCustomer);
        builder.Property(v => v.UsedCount)
            .HasDefaultValue(0);

        builder.HasMany(v => v.Usages)
            .WithOne(u => u.Voucher!)
            .HasForeignKey(u => u.VoucherId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}


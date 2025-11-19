using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PickleBall.Domain.Entities;

namespace PickleBall.Infrastructure.Data.Configurations;

public class PaymentConfiguration : IEntityTypeConfiguration<Payment>
{
    public void Configure(EntityTypeBuilder<Payment> builder)
    {
        builder.HasKey(p => p.Id);
        builder.Property(p => p.PaymentGateway).HasConversion<int>().IsRequired();
        builder.Property(p => p.Amount).HasColumnType("decimal(18,2)").IsRequired();
        builder.Property(p => p.Currency).IsRequired().HasMaxLength(10).HasDefaultValue("VND");
        builder.Property(p => p.TransactionId).HasMaxLength(256);
        builder.Property(p => p.Status).HasConversion<int>().IsRequired();
        builder.Property(p => p.RawResponse).HasMaxLength(5000);

        builder.HasIndex(p => p.OrderId);
        builder.HasIndex(p => p.TransactionId);
    }
}

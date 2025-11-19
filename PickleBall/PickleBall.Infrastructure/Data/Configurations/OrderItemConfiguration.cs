using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PickleBall.Domain.Entities;

namespace PickleBall.Infrastructure.Data.Configurations;

public class OrderItemConfiguration : IEntityTypeConfiguration<OrderItem>
{
    public void Configure(EntityTypeBuilder<OrderItem> builder)
    {
        builder.HasKey(oi => oi.Id);
        builder.Property(oi => oi.ProductNameSnapshot).IsRequired().HasMaxLength(256);
        builder.Property(oi => oi.UnitPrice).HasColumnType("decimal(18,2)").IsRequired();
        builder.Property(oi => oi.Quantity).IsRequired();
        builder.Property(oi => oi.LineTotal).HasColumnType("decimal(18,2)").IsRequired();
    }
}

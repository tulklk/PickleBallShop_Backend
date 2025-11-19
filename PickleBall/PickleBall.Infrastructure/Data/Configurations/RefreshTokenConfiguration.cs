using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PickleBall.Domain.Entities;

namespace PickleBall.Infrastructure.Data.Configurations;

public class RefreshTokenConfiguration : IEntityTypeConfiguration<RefreshToken>
{
    public void Configure(EntityTypeBuilder<RefreshToken> builder)
    {
        builder.HasKey(rt => rt.Id);
        builder.Property(rt => rt.Token).IsRequired().HasMaxLength(500);
        builder.Property(rt => rt.IpAddress).HasMaxLength(45);
        builder.Property(rt => rt.UserAgent).HasMaxLength(500);
        builder.HasIndex(rt => rt.Token);
        builder.HasIndex(rt => rt.UserId);
    }
}

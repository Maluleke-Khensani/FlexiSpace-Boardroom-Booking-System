using FlexiSpace.Core.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace FlexiSpace.Infrastructure.Persistence.Configurations
{
    public class DeviceTokenConfiguration : IEntityTypeConfiguration<DeviceToken>
    {
        public void Configure(EntityTypeBuilder<DeviceToken> builder)
        {
            builder.ToTable("DeviceTokens");

            // Primary Key
            builder.HasKey(d => d.Id);

            // Properties
            builder.Property(d => d.Token)
                   .IsRequired()
                   .HasMaxLength(500);

            builder.Property(d => d.Platform)
                   .IsRequired()
                   .HasMaxLength(20);

            builder.Property(d => d.RegisteredAt)
                   .IsRequired();

            builder.Property(d => d.LastSeenAt)
                   .IsRequired();

            // The same physical device should only ever have one row -
            // re-registering the same token updates LastSeenAt instead of
            // inserting a duplicate (see DeviceTokenService).
            builder.HasIndex(d => d.Token)
                   .IsUnique();

            // Relationships
            builder.HasOne(d => d.User)
                   .WithMany(u => u.DeviceTokens)
                   .HasForeignKey(d => d.UserId)
                   .OnDelete(DeleteBehavior.Cascade);
        }
    }
}

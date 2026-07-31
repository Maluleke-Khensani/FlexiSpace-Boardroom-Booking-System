using FlexiSpace.Core.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace FlexiSpace.Infrastructure.Persistence.Configurations
{
    public class LocationCalendarAccountConfiguration : IEntityTypeConfiguration<LocationCalendarAccount>
    {
        public void Configure(EntityTypeBuilder<LocationCalendarAccount> builder)
        {
            builder.ToTable("LocationCalendarAccounts");

            // Primary Key
            builder.HasKey(lca => lca.Id);

            // Properties
            builder.Property(lca => lca.Email)
                   .IsRequired()
                   .HasMaxLength(255);

            builder.Property(lca => lca.DisplayName)
                   .IsRequired()
                   .HasMaxLength(100);

            builder.Property(lca => lca.IsPrimary)
                   .IsRequired();

            builder.Property(lca => lca.IsActive)
                   .IsRequired();

            builder.Property(lca => lca.CreatedAt)
                   .IsRequired();

            // Relationships
            builder.HasOne(lca => lca.Location)
                   .WithMany(l => l.LocationCalendarAccounts)
                   .HasForeignKey(lca => lca.LocationId)
                   .OnDelete(DeleteBehavior.Restrict);
        }
    }
}
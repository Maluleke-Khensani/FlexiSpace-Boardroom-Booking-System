using FlexiSpace.Core.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace FlexiSpace.Infrastructure.Persistence.Configurations
{
    public class LocationConfiguration : IEntityTypeConfiguration<Location>
    {
        public void Configure(EntityTypeBuilder<Location> builder)
        {
            builder.ToTable("Locations");
            // Primary Key
            builder.HasKey(l => l.Id);

            // Properties
            builder.Property(l => l.Name)
                   .IsRequired()
                   .HasMaxLength(100);

            builder.Property(l => l.Address)
                   .IsRequired()
                   .HasMaxLength(255);

            // Relationships

            builder.HasMany(l => l.Boardrooms) //Go to the Location object (l) and use its Boardrooms collection.
                  .WithOne(b => b.Location)
                  .HasForeignKey(b => b.LocationId)
                  .OnDelete(DeleteBehavior.Restrict);

            builder.HasMany(l => l.Users)
                  .WithOne(u => u.Location)
                  .HasForeignKey(u => u.LocationId)
                  .OnDelete(DeleteBehavior.Restrict);

            builder.HasMany(l => l.LocationCalendarAccounts)
                  .WithOne(c => c.Location)
                  .HasForeignKey(c => c.LocationId)
                  .OnDelete(DeleteBehavior.Restrict);
        }
    }
}

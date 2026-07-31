using FlexiSpace.Core.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace FlexiSpace.Infrastructure.Persistence.Configurations
{
    public class UserConfiguration : IEntityTypeConfiguration<User>
    {
        public void Configure(EntityTypeBuilder<User> builder)
        {
            builder.ToTable("Users");

            builder.HasKey(u => u.Id);

            builder.Property(u => u.FirstName)
                   .IsRequired()
                   .HasMaxLength(100);

            builder.Property(u => u.LastName)
                   .IsRequired()
                   .HasMaxLength(100);

            builder.Property(u => u.Email)
                   .IsRequired()
                   .HasMaxLength(255);

            builder.Property(u => u.PhoneNumber)
                   .IsRequired()
                   .HasMaxLength(20);

            builder.Property(u => u.EntraObjectId)
                   .IsRequired();

            builder.Property(u => u.Role)
                   .IsRequired();

            builder.Property(u => u.CreatedAt)
                   .IsRequired();

            builder.Property(u => u.IsActive)
                   .IsRequired();

            builder.HasIndex(u => u.Email)
                   .IsUnique();

            builder.HasIndex(u => u.EntraObjectId)
                   .IsUnique();

            builder.HasOne(u => u.Location)
                   .WithMany(l => l.Users)
                   .HasForeignKey(u => u.LocationId)
                   .OnDelete(DeleteBehavior.Restrict);

            builder.HasMany(u => u.Bookings)
                   .WithOne(b => b.User)
                   .HasForeignKey(b => b.UserId)
                   .OnDelete(DeleteBehavior.Restrict);

            builder.HasMany(u => u.ApprovedBookings)
                   .WithOne(b => b.ApprovedBy)
                   .HasForeignKey(b => b.ApprovedById)
                   .OnDelete(DeleteBehavior.Restrict);
        }
    }
}
using FlexiSpace.Core.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace FlexiSpace.Infrastructure.Persistence.Configurations
{
    public class BookingConfiguration : IEntityTypeConfiguration<Booking>
    {
        public void Configure(EntityTypeBuilder<Booking> builder)
        {
            builder.ToTable("Bookings");

            // Primary Key
            builder.HasKey(b => b.Id);

            // Properties

            builder.Property(b => b.BookingDate)
                   .IsRequired();

            builder.Property(b => b.StartTime)
                   .IsRequired();

            builder.Property(b => b.EndTime)
                   .IsRequired();

            builder.Property(b => b.Status)
                   .IsRequired();

            builder.Property(b => b.Company)
                   .HasMaxLength(150);

            builder.Property(b => b.NumberOfAttendees)
                   .IsRequired();

            builder.Property(b => b.Notes)
                   .HasMaxLength(1000);

            builder.Property(b => b.OutlookEventId)
                   .HasMaxLength(255);

            builder.Property(b => b.CreatedAt)
                   .IsRequired();

            builder.Property(b => b.ModifiedAt);

            // Relationships

            // Booking -> User
            builder.HasOne(b => b.User)
                   .WithMany(u => u.Bookings)
                   .HasForeignKey(b => b.UserId)
                   .OnDelete(DeleteBehavior.Restrict);

            // Booking -> Boardroom
            builder.HasOne(b => b.Boardroom)
                   .WithMany(br => br.Bookings)
                   .HasForeignKey(b => b.BoardroomId)
                   .OnDelete(DeleteBehavior.Restrict);

            // Booking -> Approver (User)

            builder.HasOne(b => b.ApprovedBy)
                   .WithMany(u => u.ApprovedBookings)
                   .HasForeignKey(b => b.ApprovedById)
                   .OnDelete(DeleteBehavior.Restrict);

            // Booking -> BookingEquipment
            builder.HasMany(b => b.BookingEquipments)
                   .WithOne(be => be.Booking)
                   .HasForeignKey(be => be.BookingId)
                   .OnDelete(DeleteBehavior.Cascade);

            // Booking -> BookingCatering
            builder.HasMany(b => b.BookingCaterings)
                   .WithOne(bc => bc.Booking)
                   .HasForeignKey(bc => bc.BookingId)
                   .OnDelete(DeleteBehavior.Cascade);

        }
    }
}
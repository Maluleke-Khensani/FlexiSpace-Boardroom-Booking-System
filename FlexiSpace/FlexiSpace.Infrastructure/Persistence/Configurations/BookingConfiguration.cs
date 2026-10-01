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

            // Reminder stamps: set by BookingReminderHostedService when the
            // 24-hour, 2-hour and 1-hour reminders go out, so each is sent
            // once. Null = not sent yet. Columns added by the migrations
            // AddBookingReminderTracking and AddDeviceTokensAndReminderWindows.
            builder.Property(b => b.Reminder24hSentAt);

            builder.Property(b => b.Reminder2hSentAt);

            builder.Property(b => b.ReminderSentAt);

            // Relationships

// Booking -> User (the person who made the booking)
                    builder.HasOne(b => b.User)
                   .WithMany(u => u.Bookings)
                   .HasForeignKey(b => b.UserId)
                   .OnDelete(DeleteBehavior.Restrict);

            // Booking -> Boardroom
            builder.HasOne(b => b.Boardroom)
                   .WithMany(br => br.Bookings)
                   .HasForeignKey(b => b.BoardroomId)
                   .OnDelete(DeleteBehavior.Restrict);
            // Booking -> ModifiedBy (User)
            builder.HasOne(b => b.ModifiedBy)
                   .WithMany(u => u.ModifiedBookings)
                   .HasForeignKey(b => b.ModifiedById)
                   .OnDelete(DeleteBehavior.Restrict);

            // Booking -> CancelledBy (User)
            builder.HasOne(b => b.CancelledBy)
                   .WithMany(u => u.CancelledBookings)
                   .HasForeignKey(b => b.CancelledById)
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
using FlexiSpace.Core.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace FlexiSpace.Infrastructure.Persistence.Configurations
{
    public class BookingEquipmentConfiguration : IEntityTypeConfiguration<BookingEquipment>
    {
        public void Configure(EntityTypeBuilder<BookingEquipment> builder)
        {
            builder.ToTable("BookingEquipment");

            // Composite Primary Key
            builder.HasKey(be => new { be.BookingId, be.EquipmentId });

            // Properties
            builder.Property(be => be.Quantity)
                   .IsRequired();

            // Relationships
            builder.HasOne(be => be.Booking)
                   .WithMany(b => b.BookingEquipments)
                   .HasForeignKey(be => be.BookingId)
                   .OnDelete(DeleteBehavior.Cascade);

            builder.HasOne(be => be.Equipment)
                   .WithMany(e => e.BookingEquipments)
                   .HasForeignKey(be => be.EquipmentId)
                   .OnDelete(DeleteBehavior.Restrict);
        }
    }
}
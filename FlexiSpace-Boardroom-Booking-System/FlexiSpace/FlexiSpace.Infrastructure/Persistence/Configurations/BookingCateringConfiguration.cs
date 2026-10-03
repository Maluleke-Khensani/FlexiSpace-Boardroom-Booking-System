using FlexiSpace.Core.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace FlexiSpace.Infrastructure.Persistence.Configurations
{
    public class BookingCateringConfiguration : IEntityTypeConfiguration<BookingCatering>
    {
        public void Configure(EntityTypeBuilder<BookingCatering> builder)
        {
            builder.ToTable("BookingCatering");

            // Composite Primary Key
            builder.HasKey(bc => new { bc.BookingId, bc.CateringId });

            // Properties
            builder.Property(bc => bc.Quantity)
                   .IsRequired();

            // Relationships
            builder.HasOne(bc => bc.Booking)
                   .WithMany(b => b.BookingCaterings)
                   .HasForeignKey(bc => bc.BookingId)
                   .OnDelete(DeleteBehavior.Cascade);

            builder.HasOne(bc => bc.Catering)
                   .WithMany(c => c.BookingCaterings)
                   .HasForeignKey(bc => bc.CateringId)
                   .OnDelete(DeleteBehavior.Restrict);
        }
    }
}
using FlexiSpace.Core.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace FlexiSpace.Infrastructure.Persistence.Configurations
{
    public class CateringConfiguration : IEntityTypeConfiguration<Catering>
    {
        public void Configure(EntityTypeBuilder<Catering> builder)
        {
            builder.ToTable("Caterings");

            // Primary Key
            builder.HasKey(c => c.Id);

            // Properties
            builder.Property(c => c.Name)
                   .IsRequired()
                   .HasMaxLength(100);

            builder.Property(c => c.Description)
                   .HasMaxLength(500);

            builder.Property(c => c.IsActive)
                   .IsRequired();

            builder.Property(c => c.CreatedAt)
                   .IsRequired();

            // Relationships
            builder.HasMany(c => c.BookingCaterings)
                   .WithOne(bc => bc.Catering)
                   .HasForeignKey(bc => bc.CateringId)
                   .OnDelete(DeleteBehavior.Restrict);
        }
    }
}
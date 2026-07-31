using FlexiSpace.Core.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace FlexiSpace.Infrastructure.Persistence.Configurations
{
    public class EquipmentConfiguration : IEntityTypeConfiguration<Equipment>
    {
        public void Configure(EntityTypeBuilder<Equipment> builder)
        {
            builder.ToTable("Equipment");

            // Primary Key
            builder.HasKey(e => e.Id);

            // Properties
            builder.Property(e => e.Name)
                   .IsRequired()
                   .HasMaxLength(100);

            builder.Property(e => e.Description)
                   .IsRequired()
                   .HasMaxLength(500);

            builder.Property(e => e.IsActive)
                   .IsRequired();

            // Relationships

            builder.HasMany(e => e.BoardroomEquipments)
                   .WithOne(be => be.Equipment)
                   .HasForeignKey(be => be.EquipmentId)
                   .OnDelete(DeleteBehavior.Cascade);

            builder.HasMany(e => e.BookingEquipments)
                   .WithOne(be => be.Equipment)
                   .HasForeignKey(be => be.EquipmentId)
                   .OnDelete(DeleteBehavior.Restrict);
        }
    }
}
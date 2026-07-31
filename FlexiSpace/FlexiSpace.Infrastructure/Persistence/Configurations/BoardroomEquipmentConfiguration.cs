using FlexiSpace.Core.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace FlexiSpace.Infrastructure.Persistence.Configurations
{
    public class BoardroomEquipmentConfiguration : IEntityTypeConfiguration<BoardroomEquipment>
    {
        public void Configure(EntityTypeBuilder<BoardroomEquipment> builder)
        {
            builder.ToTable("BoardroomEquipment");

            // Composite Primary Key
            builder.HasKey(be => new { be.BoardroomId, be.EquipmentId });

            // Properties
            builder.Property(be => be.Quantity)
                   .IsRequired();

            // Relationships
            builder.HasOne(be => be.Boardroom)
                   .WithMany(b => b.BoardroomEquipments)
                   .HasForeignKey(be => be.BoardroomId)
                   .OnDelete(DeleteBehavior.Cascade);

            builder.HasOne(be => be.Equipment)
                   .WithMany(e => e.BoardroomEquipments)
                   .HasForeignKey(be => be.EquipmentId)
                   .OnDelete(DeleteBehavior.Cascade);
        }
    }
}
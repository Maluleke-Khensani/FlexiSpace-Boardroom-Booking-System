using FlexiSpace.Core.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace FlexiSpace.Infrastructure.Persistence.Configurations
{
    public class BoardroomConfiguration : IEntityTypeConfiguration<Boardroom>

    {
        public void Configure(EntityTypeBuilder<Boardroom> builder)
        {
            builder.ToTable("Boardrooms");

            // Primary Key
            builder.HasKey(b => b.Id);

            // Properties
            builder.Property(b => b.Name)
                   .IsRequired()
                   .HasMaxLength(100);

            builder.Property(b => b.Capacity)
                   .IsRequired();

            builder.Property(b => b.Status)
                   .IsRequired();

            builder.Property(b => b.CreatedAt)
                   .IsRequired();

            builder.Property(b => b.IsActive)
                   .IsRequired();

            // Relationships

            builder.HasOne(b => b.Location)
                   .WithMany(l => l.Boardrooms)
                   .HasForeignKey(b => b.LocationId)
                   .OnDelete(DeleteBehavior.Restrict);

            builder.HasMany(b => b.Bookings)
                   .WithOne(bk => bk.Boardroom)
                   .HasForeignKey(bk => bk.BoardroomId)
                   .OnDelete(DeleteBehavior.Restrict);

            builder.HasMany(b => b.BoardroomEquipments)
                   .WithOne(be => be.Boardroom)
                   .HasForeignKey(be => be.BoardroomId)
                   .OnDelete(DeleteBehavior.Cascade);


        }
    }
}

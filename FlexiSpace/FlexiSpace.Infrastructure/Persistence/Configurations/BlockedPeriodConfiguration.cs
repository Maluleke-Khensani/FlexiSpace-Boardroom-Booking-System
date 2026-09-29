using FlexiSpace.Core.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace FlexiSpace.Infrastructure.Persistence.Configurations
{
    public class BlockedPeriodConfiguration : IEntityTypeConfiguration<BlockedPeriod>
    {
        public void Configure(EntityTypeBuilder<BlockedPeriod> builder)
        {
            builder.ToTable("BlockedPeriods");

            // Primary Key
            builder.HasKey(bp => bp.Id);

            // Properties
            builder.Property(bp => bp.Start)
                   .IsRequired();

            builder.Property(bp => bp.End)
                   .IsRequired();

            builder.Property(bp => bp.Reason)
                   .IsRequired()
                   .HasMaxLength(500);

            builder.Property(bp => bp.CreatedAt)
                   .IsRequired();

            // Conflict checks always filter by boardroom and a time range.
            builder.HasIndex(bp => new { bp.BoardroomId, bp.Start, bp.End });

            // Relationships
            // A block has no meaning without its boardroom, so deleting a
            // boardroom removes its blocks with it.
            builder.HasOne(bp => bp.Boardroom)
                   .WithMany()
                   .HasForeignKey(bp => bp.BoardroomId)
                   .OnDelete(DeleteBehavior.Cascade);

            // Keep the record of who created a block: a user who created
            // blocks can't be hard-deleted out from under them.
            builder.HasOne(bp => bp.CreatedBy)
                   .WithMany()
                   .HasForeignKey(bp => bp.CreatedById)
                   .OnDelete(DeleteBehavior.Restrict);
        }
    }
}

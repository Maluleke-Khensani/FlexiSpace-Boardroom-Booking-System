using FlexiSpace.Core.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace FlexiSpace.Infrastructure.Persistence.Configurations;

public class BlockedPeriodConfiguration : IEntityTypeConfiguration<BlockedPeriod>
{
    public void Configure(EntityTypeBuilder<BlockedPeriod> builder)
    {
        builder.ToTable("BlockedPeriods");
        builder.HasKey(b => b.Id);

        builder.Property(b => b.Reason).HasMaxLength(1000);

        builder.HasOne(b => b.Boardroom)
            .WithMany()
            .HasForeignKey(b => b.BoardroomId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(b => b.CreatedByUser)
            .WithMany()
            .HasForeignKey(b => b.CreatedById)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(b => new { b.BoardroomId, b.Start, b.End });
    }
}

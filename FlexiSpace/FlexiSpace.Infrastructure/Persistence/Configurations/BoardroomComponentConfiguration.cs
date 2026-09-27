using FlexiSpace.Core.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace FlexiSpace.Infrastructure.Persistence.Configurations
{
    public class BoardroomComponentConfiguration : IEntityTypeConfiguration<BoardroomComponent>
    {
        public void Configure(EntityTypeBuilder<BoardroomComponent> builder)
        {
            builder.ToTable("BoardroomComponents");

            // Composite Primary Key
            builder.HasKey(bc => new { bc.CombinedBoardroomId, bc.ComponentBoardroomId });

            // Both foreign keys point at Boardroom, so both must be Restrict -
            // SQL Server rejects multiple cascade-delete paths onto the same
            // table, and Restrict also gives us a clean point to guard
            // deletes from the service layer instead of a raw DB error.
            builder.HasOne(bc => bc.CombinedBoardroom)
                   .WithMany(b => b.Components)
                   .HasForeignKey(bc => bc.CombinedBoardroomId)
                   .OnDelete(DeleteBehavior.Restrict);

            builder.HasOne(bc => bc.ComponentBoardroom)
                   .WithMany(b => b.PartOfCombinations)
                   .HasForeignKey(bc => bc.ComponentBoardroomId)
                   .OnDelete(DeleteBehavior.Restrict);
        }
    }
}

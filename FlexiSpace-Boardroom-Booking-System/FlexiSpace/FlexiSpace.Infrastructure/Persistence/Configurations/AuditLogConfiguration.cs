using FlexiSpace.Core.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace FlexiSpace.Infrastructure.Persistence.Configurations
{
    public class AuditLogConfiguration : IEntityTypeConfiguration<AuditLog>
    {
        public void Configure(EntityTypeBuilder<AuditLog> builder)
        {
            builder.ToTable("AuditLogs");

            // Primary Key
            builder.HasKey(a => a.Id);

            // Properties
            builder.Property(a => a.Action)
        .HasConversion<string>()
        .IsRequired();

            builder.Property(a => a.EntityName)
                   .IsRequired()
                   .HasMaxLength(100);

            builder.Property(a => a.EntityId)
                   .IsRequired()
                   .HasMaxLength(100);

            builder.Property(a => a.Timestamp)
                   .IsRequired();

            builder.Property(a => a.OldValues);

            builder.Property(a => a.NewValues);
               

            // Relationships
            builder.HasOne(a => a.User)
                   .WithMany(u => u.AuditLogs)
                   .HasForeignKey(a => a.UserId)
                   .OnDelete(DeleteBehavior.Restrict);
        }
    }
}
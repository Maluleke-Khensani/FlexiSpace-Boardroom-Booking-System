using FlexiSpace.Core.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace FlexiSpace.Infrastructure.Persistence.Configurations
{
    public class NotificationConfiguration : IEntityTypeConfiguration<Notification>
    {
        public void Configure(EntityTypeBuilder<Notification> builder)
        {
            builder.ToTable("Notifications");

            // Primary Key
            builder.HasKey(n => n.Id);

            // Properties
            builder.Property(n => n.Title)
                   .IsRequired()
                   .HasMaxLength(150);

            builder.Property(n => n.Message)
                   .IsRequired()
                   .HasMaxLength(1000);

            builder.Property(n => n.Type)
                   .IsRequired();

            builder.Property(n => n.IsRead)
                   .IsRequired();

            builder.Property(n => n.CreatedAt)
                   .IsRequired();

            builder.Property(n => n.SentAt)
                   .IsRequired();

            // Relationships
            builder.HasOne(n => n.User)
          .WithMany(u => u.Notifications)
          .HasForeignKey(n => n.UserId)
          .OnDelete(DeleteBehavior.Cascade);


        }
    }
}
using System;
using System.Threading;
using System.Threading.Tasks;
using FlexiSpace.Core.Entities;
using Microsoft.EntityFrameworkCore;



namespace FlexiSpace.Infrastructure.Persistence
{
    public class ApplicationDbContext: DbContext 
    {

        //Dependency injection for the DbContextOptions to configure the database connection
        public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options) : base(options)
        {

        }

        // Define DbSet properties for each entity in the application
        //=> is the same as { get; set; } but it is more concise and can be used for read-only properties.
        public DbSet<Location> Locations => Set<Location>();
        public DbSet<Boardroom> Boardrooms => Set<Boardroom>();
        public DbSet<User> Users => Set<User>();
        public DbSet<Booking> Bookings => Set<Booking>();

        //Lookup Eneities
        public DbSet<Equipment> Equipments => Set<Equipment>();
        public DbSet<Catering> Caterings => Set<Catering>();

        // Junction Entities
        public DbSet<BoardroomEquipment> BoardroomEquipments => Set<BoardroomEquipment>();
        public DbSet<BookingEquipment> BookingEquipments => Set<BookingEquipment>();
        public DbSet<BookingCatering> BookingCaterings => Set<BookingCatering>();


        // Supporting Entities
        public DbSet<LocationCalendarAccount> LocationCalendarAccounts => Set<LocationCalendarAccount>();
        public DbSet<Notification> Notifications => Set<Notification>();
        public DbSet<AuditLog> AuditLogs => Set<AuditLog>();

        //// Automatically apply all Fluent API configurations in this assembly.
        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            modelBuilder.ApplyConfigurationsFromAssembly(typeof(ApplicationDbContext).Assembly);
        }

        public override int SaveChanges()
        {
            ApplyBookingTimestamps();
            return base.SaveChanges();
        }

        public override async Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
        {
            ApplyBookingTimestamps();
            return await base.SaveChangesAsync(cancellationToken);
        }

        private void ApplyBookingTimestamps()
        {
            var entries = ChangeTracker.Entries<Booking>();

            foreach (var entry in entries)
            {
                if (entry.State == EntityState.Added)
                {
                    entry.Entity.CreatedAt = DateTime.UtcNow;
                }
                else if (entry.State == EntityState.Modified)
                {
                    entry.Entity.ModifiedAt = DateTime.UtcNow;
                }
            }
        }
    }
}

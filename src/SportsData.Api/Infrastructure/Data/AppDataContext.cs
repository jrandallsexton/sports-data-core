using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;

using SportsData.Api.Infrastructure.Data.Entities;

namespace SportsData.Api.Infrastructure.Data
{
    public class AppDataContext : DbContext
    {
        public AppDataContext(DbContextOptions<AppDataContext> options)
            : base(options) { }

        public DbSet<User> Users { get; set; }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            modelBuilder.ApplyConfigurationsFromAssembly(typeof(Entities.User.EntityConfiguration).Assembly);

            // Seed the Firebase test user
            modelBuilder.Entity<User>().HasData(new User
            {
                Id = Guid.Parse("11111111-1111-1111-1111-111111111111"),
                FirebaseUid = "a3GLn01j7pepPpVUSugtKWbRtQG3",
                Email = "jrandallsexton@gmail.com",
                EmailVerified = false,
                SignInProvider = "password",
                CreatedUtc = DateTime.UtcNow,
                LastLoginUtc = DateTime.UtcNow,
                DisplayName = "Randall Sexton"
            });
        }

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
        {
            optionsBuilder.ConfigureWarnings(x =>
            {
                x.Ignore([RelationalEventId.PendingModelChangesWarning]);
            });
            optionsBuilder.EnableSensitiveDataLogging(false);
        }
    }
}

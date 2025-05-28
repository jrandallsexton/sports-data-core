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

        public DbSet<PickemGroupUserPick> UserPicks { get; set; }

        public DbSet<Contest> Contests { get; set; }

        public DbSet<PickemGroup> PickemGroups { get; set; }

        public DbSet<PickResult> PickResults { get; set; }

        public DbSet<PickemGroupWeekResult> PickemGroupWeekResults { get; set; }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            modelBuilder.ApplyConfigurationsFromAssembly(typeof(Entities.User.EntityConfiguration).Assembly);

            // Seed the Firebase test user
            modelBuilder.Entity<User>().HasData(new User
            {
                Id = Guid.Parse("11111111-1111-1111-1111-111111111111"),
                FirebaseUid = "ngovRAr5E8cjMVaZNvcqN1nPFPJ2",
                Email = "foo@bar.com",
                EmailVerified = true,
                SignInProvider = "password",
                CreatedUtc = DateTime.UtcNow,
                LastLoginUtc = DateTime.UtcNow,
                DisplayName = "Foo Bar"
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

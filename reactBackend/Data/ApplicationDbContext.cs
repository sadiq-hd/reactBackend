using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using reactBackend.Models;

namespace reactBackend.Data
{
    public class ApplicationDbContext : DbContext
    {
        public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
            : base(options)
        {
        }

        public DbSet<User> Users { get; set; }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            // بيانات تجريبية 
            modelBuilder.Entity<User>().HasData(
                new User
                {
                    Id = 1,
                    Email = "admin@example.com",
                    Password = BCrypt.Net.BCrypt.HashPassword("admin123"),
                    Name = "المدير",
                    Role = "admin",
                    Username = "المدير",
                    PhoneNumber = "0553065029"
                },
                new User
                {
                    Id = 2,
                    Email = "user@example.com",
                    Password = BCrypt.Net.BCrypt.HashPassword("user123"),
                    Name = "Sadiq",
                    Role = "user",
                    Username = "Sadiq",
                    PhoneNumber = "0553065029"
                }
            );
        }

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
        {
            if (!optionsBuilder.IsConfigured)
            {
                optionsBuilder.ConfigureWarnings(warnings =>
                    warnings.Ignore(CoreEventId.ManyServiceProvidersCreatedWarning));
            }
        }
    }
}
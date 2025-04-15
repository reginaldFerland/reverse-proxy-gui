using Microsoft.EntityFrameworkCore;
using ReverseProxy.Models;

namespace ReverseProxy.Data;

public class ApplicationDbContext : DbContext
{
    public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
        : base(options)
    {
    }

    public DbSet<Mapping> Mappings { get; set; } = null!;

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // Configure the Mapping entity
        modelBuilder.Entity<Mapping>()
            .HasKey(m => m.Id);

        modelBuilder.Entity<Mapping>()
            .Property(m => m.Name)
            .IsRequired();

        modelBuilder.Entity<Mapping>()
            .Property(m => m.RoutePattern)
            .IsRequired();

        modelBuilder.Entity<Mapping>()
            .Property(m => m.Destination1)
            .IsRequired();

        // Seed some initial data
        modelBuilder.Entity<Mapping>().HasData(
            new Mapping
            {
                Id = 1,
                Name = "Default Route",
                RoutePattern = "/api/{**catch-all}",
                Destination1 = "https://api1.example.com",
                Destination2 = "https://api2.example.com",
                ActiveDestination = 1
            }
        );
    }
}
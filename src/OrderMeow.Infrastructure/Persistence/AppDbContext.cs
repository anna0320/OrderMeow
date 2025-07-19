using Microsoft.EntityFrameworkCore;
using OrderMeow.Domain.Entities;

namespace OrderMeow.Infrastructure.Persistence;

public class AppDbContext: DbContext
{
    public DbSet<User> Users => Set<User>();
    public DbSet<Order> Orders => Set<Order>();

    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Order>()
            .HasOne(o=>o.User)
            .WithMany(u=>u.Orders)
            .HasForeignKey(o=>o.UserId)
            .OnDelete(DeleteBehavior.Cascade);
        base.OnModelCreating(modelBuilder);
    }
}
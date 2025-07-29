using Microsoft.EntityFrameworkCore;
using OrderMeow.Core.Entities;

namespace OrderMeow.Infrastructure.Persistence;

public class AppDbContext: DbContext
{
    public DbSet<User> Users => Set<User>();
    public DbSet<Order> Orders => Set<Order>();
    public DbSet<RefreshToken> RefreshTokens => Set<RefreshToken>();    

    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Order>()
            .HasOne(o=>o.User)
            .WithMany(u=>u.Orders)
            .HasForeignKey(o=>o.UserId)
            .OnDelete(DeleteBehavior.Cascade);
        modelBuilder.Entity<RefreshToken>()
            .HasOne(rt=>rt.User)
            .WithMany(u=>u.RefreshTokens)
            .HasForeignKey(rt=>rt.UserId)
            .OnDelete(DeleteBehavior.Cascade);
        
        base.OnModelCreating(modelBuilder);
    }
}
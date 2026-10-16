using Microsoft.EntityFrameworkCore;
using VaultFlow.Api.Models;

namespace VaultFlow.Api.Data;

public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

    public DbSet<Transaction> Transactions => Set<Transaction>();
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        modelBuilder.Entity<Transaction>()
        .Property(t => t.Amount)
        .HasColumnType("decimal(18,2)");
    }
}
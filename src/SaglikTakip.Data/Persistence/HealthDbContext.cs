using Microsoft.EntityFrameworkCore;
using SaglikTakip.Domain.Entities;

namespace SaglikTakip.Data.Persistence;

/// <summary>
/// EF Core context for the health tracking database. Table and column names follow the original
/// schema (Kullanicilar, SaglikKayitlari, Egzersizler).
/// </summary>
public sealed class HealthDbContext(DbContextOptions<HealthDbContext> options) : DbContext(options)
{
    /// <summary>Name of the shadow concurrency token configured on every table.</summary>
    public const string RowVersion = "RowVersion";

    public DbSet<User> Users => Set<User>();

    public DbSet<HealthRecord> HealthRecords => Set<HealthRecord>();

    public DbSet<Exercise> Exercises => Set<Exercise>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        ArgumentNullException.ThrowIfNull(modelBuilder);
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(HealthDbContext).Assembly);
    }
}

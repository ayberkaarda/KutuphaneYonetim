using KutuphaneYonetim.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace KutuphaneYonetim.Data.Persistence;

/// <summary>
/// EF Core context for the library database. Table and column names follow the original
/// schema (Kitaplar, Uyeler, OduncIslemleri) and its Turkish naming (Rezervasyonlar).
/// </summary>
public sealed class LibraryDbContext(DbContextOptions<LibraryDbContext> options) : DbContext(options)
{
    /// <summary>Name of the shadow concurrency token configured on every table.</summary>
    public const string RowVersion = "RowVersion";

    private const string SqliteProviderName = "Microsoft.EntityFrameworkCore.Sqlite";

    public DbSet<Book> Books => Set<Book>();

    public DbSet<Member> Members => Set<Member>();

    public DbSet<Loan> Loans => Set<Loan>();

    public DbSet<Reservation> Reservations => Set<Reservation>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        ArgumentNullException.ThrowIfNull(modelBuilder);
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(LibraryDbContext).Assembly);

        if (string.Equals(Database.ProviderName, SqliteProviderName, StringComparison.Ordinal))
        {
            // SQLite (used by the tests) cannot compare, order or sum decimal columns; store them as REAL there.
            foreach (var property in modelBuilder.Model.GetEntityTypes()
                .SelectMany(entity => entity.GetProperties())
                .Where(property => Nullable.GetUnderlyingType(property.ClrType) == typeof(decimal) || property.ClrType == typeof(decimal)))
            {
                property.SetProviderClrType(typeof(double));
            }
        }
    }
}

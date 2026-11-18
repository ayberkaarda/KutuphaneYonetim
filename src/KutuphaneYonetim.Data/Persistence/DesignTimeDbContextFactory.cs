using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace KutuphaneYonetim.Data.Persistence;

/// <summary>
/// Used by <c>dotnet ef</c> only. Reads the connection string from KUTUPHANE_CONNECTION and
/// falls back to a local SQL Server Express instance.
/// </summary>
internal sealed class DesignTimeDbContextFactory : IDesignTimeDbContextFactory<LibraryDbContext>
{
    private const string FallbackConnectionString =
        "Server=localhost\\SQLEXPRESS;Database=KutuphaneYonetim;Trusted_Connection=True;TrustServerCertificate=True;";

    public LibraryDbContext CreateDbContext(string[] args)
    {
        var connectionString = Environment.GetEnvironmentVariable(DataServiceCollectionExtensions.ConnectionEnvironmentVariable);
        var options = new DbContextOptionsBuilder<LibraryDbContext>()
            .UseSqlServer(string.IsNullOrWhiteSpace(connectionString) ? FallbackConnectionString : connectionString)
            .Options;
        return new LibraryDbContext(options);
    }
}

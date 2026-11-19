using KutuphaneYonetim.Data.Persistence;
using KutuphaneYonetim.Data.Repositories;
using KutuphaneYonetim.Domain.Repositories;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace KutuphaneYonetim.Data;

public static class DataServiceCollectionExtensions
{
    /// <summary>Environment variable that overrides the configured connection string.</summary>
    public const string ConnectionEnvironmentVariable = "KUTUPHANE_CONNECTION";

    /// <summary>Registers the data access layer on SQL Server.</summary>
    public static IServiceCollection AddSqlServerDataAccess(this IServiceCollection services, string connectionString)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(connectionString);
        return services.AddDataAccess(options => options.UseSqlServer(connectionString));
    }

    /// <summary>
    /// Registers the data access layer with a caller supplied provider configuration, so tests can
    /// plug in another provider (for example SQLite in-memory).
    /// </summary>
    public static IServiceCollection AddDataAccess(
        this IServiceCollection services,
        Action<DbContextOptionsBuilder> configureProvider)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configureProvider);

        services.AddDbContextFactory<LibraryDbContext>(configureProvider);
        services.AddSingleton<IDatabaseInitializer, DatabaseInitializer>();
        services.AddSingleton<IBookRepository, BookRepository>();
        services.AddSingleton<IMemberRepository, MemberRepository>();
        services.AddSingleton<ILoanRepository, LoanRepository>();
        services.AddSingleton<IReservationRepository, ReservationRepository>();
        services.AddSingleton<IReportRepository, ReportRepository>();
        return services;
    }
}

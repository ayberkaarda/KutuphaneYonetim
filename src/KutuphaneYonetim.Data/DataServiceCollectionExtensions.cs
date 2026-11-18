using KutuphaneYonetim.Data.Repositories;
using KutuphaneYonetim.Domain.Repositories;
using Microsoft.Extensions.DependencyInjection;

namespace KutuphaneYonetim.Data;

public static class DataServiceCollectionExtensions
{
    /// <summary>Registers the SQL Server data access layer (Dapper repositories and DbUp migrator).</summary>
    public static IServiceCollection AddDataAccess(this IServiceCollection services, string connectionString)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentException.ThrowIfNullOrWhiteSpace(connectionString);

        services.AddSingleton<ISqlConnectionFactory>(new SqlConnectionFactory(connectionString));
        services.AddSingleton<IDatabaseMigrator>(new DatabaseMigrator(connectionString));
        services.AddSingleton<IBookRepository, BookRepository>();
        services.AddSingleton<IMemberRepository, MemberRepository>();
        services.AddSingleton<ILoanRepository, LoanRepository>();
        return services;
    }
}

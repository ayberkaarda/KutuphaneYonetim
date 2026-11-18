using KutuphaneYonetim.Business;
using KutuphaneYonetim.Data.Persistence;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace KutuphaneYonetim.Data.Tests;

/// <summary>
/// A private SQLite in-memory database wired through the application's own registrations.
/// The connection stays open for the lifetime of the instance, which keeps the database alive.
/// The schema and sample data come from the EF Core model (8 books, 3 members, 2 loans; book 3 is on loan).
/// </summary>
public sealed class SqliteDatabase : IAsyncLifetime
{
    private readonly SqliteConnection _connection = new("Data Source=:memory:");
    private ServiceProvider? _provider;

    public IServiceProvider Services => _provider ?? throw new InvalidOperationException("Not initialized.");

    public T Get<T>()
        where T : notnull => Services.GetRequiredService<T>();

    public LibraryDbContext CreateContext() =>
        Services.GetRequiredService<IDbContextFactory<LibraryDbContext>>().CreateDbContext();

    public async ValueTask InitializeAsync()
    {
        await _connection.OpenAsync();

        var services = new ServiceCollection();
        services.AddDataAccess(options => options.UseSqlite(_connection));
        services.AddBusinessServices();
        _provider = services.BuildServiceProvider(new ServiceProviderOptions { ValidateOnBuild = true });

        await Get<IDatabaseInitializer>().InitializeAsync();
    }

    public async ValueTask DisposeAsync()
    {
        if (_provider is not null)
        {
            await _provider.DisposeAsync();
        }

        await _connection.DisposeAsync();
    }
}

using KutuphaneYonetim.Business;
using KutuphaneYonetim.Data.Persistence;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace KutuphaneYonetim.Data.Tests;

/// <summary>
/// A throw-away database on a real SQL Server instance, wired through the application's own registrations
/// plus <see cref="WaitPointInterceptor"/>, for tests that interleave concurrent operations. Seeded like
/// <see cref="SqliteDatabase"/>. KUTUPHANE_TEST_SQLSERVER chooses the instance (default: local SQL Express
/// with Windows authentication); KUTUPHANE_SKIP_SQL_TESTS=1 or an unreachable server skips the test.
/// </summary>
public sealed class SqlServerDatabase : IAsyncDisposable
{
    private const string ServerEnvironmentVariable = "KUTUPHANE_TEST_SQLSERVER";
    private const string SkipEnvironmentVariable = "KUTUPHANE_SKIP_SQL_TESTS";
    private const string DefaultServer =
        @"Server=localhost\SQLEXPRESS;Integrated Security=True;TrustServerCertificate=True;Connect Timeout=5;";

    private static readonly TimeSpan WaitLimit = TimeSpan.FromSeconds(30);

    private readonly string _server;
    private readonly string _name;
    private ServiceProvider? _provider;

    private SqlServerDatabase(string server, string name)
    {
        _server = server;
        _name = name;
    }

    public IServiceProvider Services => _provider ?? throw new InvalidOperationException("Not initialized.");

    public T Get<T>()
        where T : notnull => Services.GetRequiredService<T>();

    public LibraryDbContext CreateContext() =>
        Services.GetRequiredService<IDbContextFactory<LibraryDbContext>>().CreateDbContext();

    /// <summary>Creates and initializes the database, or skips the calling test when no server is available.</summary>
    public static async Task<SqlServerDatabase> CreateAsync(TimeProvider clock, LibraryPolicy policy, CancellationToken ct)
    {
        var server = await ResolveServerOrSkipAsync(ct);
        var database = new SqlServerDatabase(server, "KutuphaneRaceTest_" + Guid.NewGuid().ToString("N"));
        await ExecuteAsync(server, $"CREATE DATABASE [{database._name}]", ct);
        try
        {
            var connectionString = new SqlConnectionStringBuilder(server) { InitialCatalog = database._name }.ConnectionString;
            var services = new ServiceCollection();
            services.AddDataAccess(options => options.UseSqlServer(connectionString).AddInterceptors(WaitPointInterceptor.Instance));
            services.AddSingleton(clock);
            services.AddBusinessServices(policy);
            database._provider = services.BuildServiceProvider(new ServiceProviderOptions { ValidateOnBuild = true });

            await database.Get<IDatabaseInitializer>().InitializeAsync(ct);
            return database;
        }
        catch
        {
            await database.DisposeAsync();
            throw;
        }
    }

    /// <summary>
    /// Waits until <paramref name="operation"/> has finished or one of its commands is blocked by another
    /// session of this database (a lock wait seen by the server), whichever happens first.
    /// </summary>
    public async Task WaitUntilFinishedOrBlockedAsync(Task operation, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(operation);

        const string BlockedRequests = """
            SELECT COUNT(*) FROM sys.dm_exec_requests
            WHERE database_id = DB_ID(@name) AND blocking_session_id <> 0
            """;

        var deadline = DateTime.UtcNow + WaitLimit;
        await using var connection = new SqlConnection(_server);
        await connection.OpenAsync(ct);
        await using var command = new SqlCommand(BlockedRequests, connection);
        command.Parameters.AddWithValue("@name", _name);

        while (!operation.IsCompleted)
        {
            if (Convert.ToInt32(await command.ExecuteScalarAsync(ct), System.Globalization.CultureInfo.InvariantCulture) > 0)
            {
                return;
            }

            if (DateTime.UtcNow > deadline)
            {
                throw new TimeoutException("The operation neither finished nor waited for a lock.");
            }

            await Task.Delay(10, ct);
        }
    }

    public async ValueTask DisposeAsync()
    {
        if (_provider is not null)
        {
            await _provider.DisposeAsync();
        }

        SqlConnection.ClearAllPools();
        await ExecuteAsync(
            _server,
            $"IF DB_ID(N'{_name}') IS NOT NULL BEGIN ALTER DATABASE [{_name}] SET SINGLE_USER WITH ROLLBACK IMMEDIATE; DROP DATABASE [{_name}]; END",
            CancellationToken.None);
    }

    private static async Task<string> ResolveServerOrSkipAsync(CancellationToken ct)
    {
        var skip = Environment.GetEnvironmentVariable(SkipEnvironmentVariable);
        if (skip is "1" || string.Equals(skip, "true", StringComparison.OrdinalIgnoreCase))
        {
            Assert.Skip($"{SkipEnvironmentVariable} is set.");
        }

        var server = Environment.GetEnvironmentVariable(ServerEnvironmentVariable);
        if (string.IsNullOrWhiteSpace(server))
        {
            server = DefaultServer;
        }

        try
        {
            await using var connection = new SqlConnection(server);
            await connection.OpenAsync(ct);
        }
        catch (SqlException ex)
        {
            Assert.Skip($"No SQL Server available ({ex.Message}). Set {ServerEnvironmentVariable} to run this test.");
        }

        return server;
    }

    private static async Task ExecuteAsync(string connectionString, string sql, CancellationToken ct)
    {
        await using var connection = new SqlConnection(connectionString);
        await connection.OpenAsync(ct);
        await using var command = new SqlCommand(sql, connection);
        await command.ExecuteNonQueryAsync(ct);
    }
}

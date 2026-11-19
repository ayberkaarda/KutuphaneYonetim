using KutuphaneYonetim.Business;
using KutuphaneYonetim.Business.Services;
using KutuphaneYonetim.Data.Persistence;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace KutuphaneYonetim.Data.Tests;

/// <summary>
/// Runs against a real SQL Server instance in a throw-away database.
/// Set KUTUPHANE_TEST_SQLSERVER to a server-level connection string to choose the instance
/// (default: local SQL Express with Windows authentication) and KUTUPHANE_SKIP_SQL_TESTS=1 to skip.
/// </summary>
public sealed class LegacyUpgradeTests
{
    private const string ServerEnvironmentVariable = "KUTUPHANE_TEST_SQLSERVER";
    private const string SkipEnvironmentVariable = "KUTUPHANE_SKIP_SQL_TESTS";
    private const string DefaultServer =
        @"Server=localhost\SQLEXPRESS;Integrated Security=True;TrustServerCertificate=True;Connect Timeout=5;";

    // Schema of the original application's database (read from its backup): nullable columns,
    // system-named primary and foreign keys, no indexes, no row versions, no migration history.
    private const string LegacySchema = """
        CREATE TABLE dbo.Kitaplar (
            kitap_id INT IDENTITY(1,1) PRIMARY KEY,
            ad NVARCHAR(100) NULL,
            yazar NVARCHAR(100) NULL,
            durum BIT NULL);
        CREATE TABLE dbo.Uyeler (
            uye_id INT IDENTITY(1,1) PRIMARY KEY,
            ad NVARCHAR(50) NULL,
            soyad NVARCHAR(50) NULL);
        CREATE TABLE dbo.OduncIslemleri (
            odunc_id INT IDENTITY(1,1) PRIMARY KEY,
            kitap_id INT NULL FOREIGN KEY REFERENCES dbo.Kitaplar (kitap_id),
            uye_id INT NULL FOREIGN KEY REFERENCES dbo.Uyeler (uye_id),
            odunc_tarihi DATETIME NULL,
            iade_tarihi DATETIME NULL);
        """;

    // Identity values deliberately do not start at 1 so overwritten or re-seeded rows would show.
    private const string LegacyData = """
        SET IDENTITY_INSERT dbo.Kitaplar ON;
        INSERT dbo.Kitaplar (kitap_id, ad, yazar, durum) VALUES
            (4, N'Eski Kitap A', N'Yazar A', 1),
            (9, N'Eski Kitap B', N'Yazar B', 0),
            (12, N'Eski Kitap C', N'Yazar C', NULL);
        SET IDENTITY_INSERT dbo.Kitaplar OFF;
        SET IDENTITY_INSERT dbo.Uyeler ON;
        INSERT dbo.Uyeler (uye_id, ad, soyad) VALUES (5, N'Eski', N'Uye'), (7, N'Diger', N'Okur');
        SET IDENTITY_INSERT dbo.Uyeler OFF;
        SET IDENTITY_INSERT dbo.OduncIslemleri ON;
        INSERT dbo.OduncIslemleri (odunc_id, kitap_id, uye_id, odunc_tarihi, iade_tarihi) VALUES
            (3, 4, 5, '2025-05-18T00:05:31', '2025-05-18T00:09:58'),
            (6, 9, 7, '2025-05-18T00:23:01', NULL);
        SET IDENTITY_INSERT dbo.OduncIslemleri OFF;
        """;

    [Fact]
    public async Task Initialize_LegacyDatabase_PreservesDataAndEnablesOperations()
    {
        var ct = TestContext.Current.CancellationToken;
        var server = await ResolveServerOrSkipAsync(ct);
        var databaseName = "KutuphaneLegacyTest_" + Guid.NewGuid().ToString("N");
        var connectionString = new SqlConnectionStringBuilder(server) { InitialCatalog = databaseName }.ConnectionString;

        await ExecuteAsync(server, $"CREATE DATABASE [{databaseName}]", ct);
        try
        {
            await ExecuteAsync(connectionString, LegacySchema, ct);
            await ExecuteAsync(connectionString, LegacyData, ct);

            await using (var provider = BuildServices(connectionString))
            {
                var initializer = provider.GetRequiredService<IDatabaseInitializer>();
                await initializer.InitializeAsync(ct);
                await initializer.InitializeAsync(ct); // Second start must be a no-op.

                var books = provider.GetRequiredService<IBookService>();
                var members = provider.GetRequiredService<IMemberService>();
                var loans = provider.GetRequiredService<ILoanService>();

                // Existing rows and identities are kept; no sample data was added.
                var allBooks = await books.GetAllAsync(ct);
                Assert.Equal([4, 9, 12], allBooks.Select(b => b.Id).Order());
                Assert.Equal("Eski Kitap B", allBooks.Single(b => b.Id == 9).Title);
                Assert.False(allBooks.Single(b => b.Id == 9).IsAvailable);
                Assert.True(allBooks.Single(b => b.Id == 12).IsAvailable);
                Assert.Equal([5, 7], (await members.GetAllAsync(ct)).Select(m => m.Id).Order());
                var allLoans = await loans.GetAllAsync(ct);
                Assert.Equal([3, 6], allLoans.Select(l => l.Id).Order());
                Assert.Null(allLoans.Single(l => l.Id == 6).ReturnDate);
                Assert.Equal(1, await ScalarAsync(
                    connectionString,
                    "SELECT COUNT(*) FROM dbo.__EFMigrationsHistory WHERE MigrationId LIKE N'%_InitialCreate'",
                    ct));

                // The migrations after the baseline (due dates, fines, ...) were applied on top of it.
                await using (var context = await provider.GetRequiredService<IDbContextFactory<LibraryDbContext>>().CreateDbContextAsync(ct))
                {
                    Assert.Empty(await context.Database.GetPendingMigrationsAsync(ct));
                }

                Assert.All(allLoans, l => Assert.Null(l.DueDate)); // Legacy loans have no due date.

                // New rows, lending and returning work on the upgraded schema.
                var book = await books.AddAsync("Yeni Kitap", "Yeni Yazar", cancellationToken: ct);
                var member = await members.AddAsync("Yeni", "Uye", ct);
                Assert.True(book.Id > 12);
                Assert.True(member.Id > 7);

                await loans.LendAsync(book.Id, member.Id, cancellationToken: ct);
                Assert.NotNull((await loans.GetAllAsync(ct)).Single(l => l.BookId == book.Id && l.IsActive).DueDate);
                await Assert.ThrowsAsync<BusinessRuleException>(() => loans.LendAsync(book.Id, 5, cancellationToken: ct));
                await Assert.ThrowsAsync<BusinessRuleException>(() => loans.LendAsync(9, member.Id, cancellationToken: ct));

                // The database itself refuses a second open loan for the same book.
                var duplicate = await Assert.ThrowsAsync<SqlException>(() => ExecuteAsync(
                    connectionString,
                    $"INSERT dbo.OduncIslemleri (kitap_id, uye_id, odunc_tarihi) VALUES ({book.Id}, 5, GETDATE())",
                    ct));
                Assert.Contains("UX_OduncIslemleri_active_kitap", duplicate.Message, StringComparison.Ordinal);

                // Reservations work on the legacy loan: the new member queues for book 9 (lent to member 7).
                var reservations = provider.GetRequiredService<IReservationService>();
                await reservations.ReserveAsync(9, member.Id, ct);
                await Assert.ThrowsAsync<BusinessRuleException>(() => reservations.ReserveAsync(9, member.Id, ct));

                await loans.ReturnAsync(book.Id, ct);
                var legacyReturn = await loans.ReturnAsync(9, ct); // Legacy open loan: no fine.
                Assert.Equal((0, 0m, "Yeni Uye"), (legacyReturn.OverdueDays, legacyReturn.Fine, legacyReturn.HeldForMemberName));
                await Assert.ThrowsAsync<BusinessRuleException>(() => loans.LendAsync(9, 5, cancellationToken: ct)); // Held.
                await loans.LendAsync(9, member.Id, cancellationToken: ct);
                Assert.Equal(9, Assert.Single(await loans.GetAllAsync(ct), l => l.ReturnDate is null).BookId);
                Assert.Empty(await reservations.GetActiveAsync(ct));

                // The database refuses a second active reservation of the same member for the same book.
                await ExecuteAsync(
                    connectionString,
                    $"INSERT dbo.Rezervasyonlar (kitap_id, uye_id, olusturma_tarihi, durum) VALUES ({book.Id}, 5, GETDATE(), 0)",
                    ct);
                var duplicateReservation = await Assert.ThrowsAsync<SqlException>(() => ExecuteAsync(
                    connectionString,
                    $"INSERT dbo.Rezervasyonlar (kitap_id, uye_id, olusturma_tarihi, durum) VALUES ({book.Id}, 5, GETDATE(), 0)",
                    ct));
                Assert.Contains("UX_Rezervasyonlar_active_kitap_uye", duplicateReservation.Message, StringComparison.Ordinal);
            }
        }
        finally
        {
            SqlConnection.ClearAllPools();
            await ExecuteAsync(
                server,
                $"ALTER DATABASE [{databaseName}] SET SINGLE_USER WITH ROLLBACK IMMEDIATE; DROP DATABASE [{databaseName}];",
                CancellationToken.None);
        }
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

    private static ServiceProvider BuildServices(string connectionString)
    {
        var services = new ServiceCollection();
        services.AddSqlServerDataAccess(connectionString);
        services.AddBusinessServices();
        return services.BuildServiceProvider();
    }

    private static async Task ExecuteAsync(string connectionString, string sql, CancellationToken ct)
    {
        await using var connection = new SqlConnection(connectionString);
        await connection.OpenAsync(ct);
        await using var command = new SqlCommand(sql, connection);
        await command.ExecuteNonQueryAsync(ct);
    }

    private static async Task<int> ScalarAsync(string connectionString, string sql, CancellationToken ct)
    {
        await using var connection = new SqlConnection(connectionString);
        await connection.OpenAsync(ct);
        await using var command = new SqlCommand(sql, connection);
        return Convert.ToInt32(await command.ExecuteScalarAsync(ct), System.Globalization.CultureInfo.InvariantCulture);
    }
}

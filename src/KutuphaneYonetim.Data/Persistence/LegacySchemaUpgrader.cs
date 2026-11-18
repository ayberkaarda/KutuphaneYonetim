using System.Reflection;
using KutuphaneYonetim.Data.Persistence.Migrations;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.EntityFrameworkCore.Storage;

namespace KutuphaneYonetim.Data.Persistence;

/// <summary>
/// Adopts a database created by the original application (Kitaplar, Uyeler and OduncIslemleri
/// without an EF Core migrations history). The existing rows and identity values are kept; the
/// missing columns, constraints and indexes of <see cref="InitialCreate"/> are added and the
/// migration is recorded as applied, so its sample data is never inserted into such a database.
/// </summary>
/// <remarks>
/// Every step checks the current schema first, and the whole upgrade runs in one transaction under
/// an application lock, so it is safe to run repeatedly and from concurrent processes.
/// </remarks>
internal static class LegacySchemaUpgrader
{
    private const string LockResource = "KutuphaneYonetim.LegacySchemaUpgrade";

    private static readonly string[] LegacyTables = ["Kitaplar", "Uyeler", "OduncIslemleri"];

    /// <summary>Identifier of the migration that a legacy database is upgraded to.</summary>
    internal static string BaselineMigrationId { get; } =
        typeof(InitialCreate).GetCustomAttribute<MigrationAttribute>()!.Id;

    /// <summary>
    /// Upgrades a legacy database in place. Returns <see langword="false"/> when there is nothing to
    /// adopt (no database yet, an empty database, or the baseline migration is already applied).
    /// </summary>
    public static async Task<bool> UpgradeIfLegacyAsync(LibraryDbContext db, CancellationToken cancellationToken)
    {
        if (!await db.GetService<IRelationalDatabaseCreator>().ExistsAsync(cancellationToken).ConfigureAwait(false))
        {
            return false;
        }

        if (!await IsLegacyAsync(db, cancellationToken).ConfigureAwait(false))
        {
            return false;
        }

        var strategy = db.Database.CreateExecutionStrategy();
        return await strategy.ExecuteAsync(
            db,
            static async (context, token) =>
            {
                await using var transaction = await context.Database.BeginTransactionAsync(token).ConfigureAwait(false);

                await ExecuteAsync(
                    context,
                    $"EXEC sp_getapplock @Resource = N'{LockResource}', @LockMode = 'Exclusive', @LockOwner = 'Transaction', @LockTimeout = 60000;",
                    token).ConfigureAwait(false);

                // Another process may have finished the upgrade while this one waited for the lock.
                if (!await IsLegacyAsync(context, token).ConfigureAwait(false))
                {
                    await transaction.RollbackAsync(token).ConfigureAwait(false);
                    return false;
                }

                await EnsureAllTablesExistAsync(context, token).ConfigureAwait(false);
                await ValidateDataAsync(context, token).ConfigureAwait(false);

                foreach (var statement in UpgradeStatements)
                {
                    await ExecuteAsync(context, statement, token).ConfigureAwait(false);
                }

                await MarkBaselineAppliedAsync(context, token).ConfigureAwait(false);
                await transaction.CommitAsync(token).ConfigureAwait(false);
                return true;
            },
            verifySucceeded: null,
            cancellationToken).ConfigureAwait(false);
    }

    private static async Task<bool> IsLegacyAsync(LibraryDbContext db, CancellationToken cancellationToken)
    {
        var applied = await db.Database.GetAppliedMigrationsAsync(cancellationToken).ConfigureAwait(false);
        if (applied.Contains(BaselineMigrationId, StringComparer.Ordinal))
        {
            return false;
        }

        return await CountExistingLegacyTablesAsync(db, cancellationToken).ConfigureAwait(false) > 0;
    }

    private static Task<int> CountExistingLegacyTablesAsync(LibraryDbContext db, CancellationToken cancellationToken) =>
        ScalarAsync(
            db,
            "SELECT COUNT(*) AS [Value] FROM sys.tables WHERE schema_id = SCHEMA_ID(N'dbo') AND name IN (N'Kitaplar', N'Uyeler', N'OduncIslemleri')",
            cancellationToken);

    private static async Task EnsureAllTablesExistAsync(LibraryDbContext db, CancellationToken cancellationToken)
    {
        var count = await CountExistingLegacyTablesAsync(db, cancellationToken).ConfigureAwait(false);
        if (count != LegacyTables.Length)
        {
            throw new InvalidOperationException(
                "The database contains only part of the library schema (Kitaplar, Uyeler, OduncIslemleri) "
                + "and no migration history, so it cannot be upgraded automatically.");
        }
    }

    /// <summary>
    /// Refuses the upgrade, before anything is changed, when existing rows cannot satisfy the new
    /// constraints. Fixing such rows needs a human decision, so no data is altered or deleted here.
    /// </summary>
    private static async Task ValidateDataAsync(LibraryDbContext db, CancellationToken cancellationToken)
    {
        (string Sql, string Problem)[] checks =
        [
            ("SELECT COUNT(*) AS [Value] FROM dbo.OduncIslemleri WHERE kitap_id IS NULL OR uye_id IS NULL OR odunc_tarihi IS NULL",
                "loans without a book, member or loan date"),
            ("SELECT COUNT(*) AS [Value] FROM dbo.OduncIslemleri o WHERE NOT EXISTS (SELECT 1 FROM dbo.Kitaplar k WHERE k.kitap_id = o.kitap_id) "
                + "OR NOT EXISTS (SELECT 1 FROM dbo.Uyeler u WHERE u.uye_id = o.uye_id)",
                "loans that reference a missing book or member"),
            ("SELECT COUNT(*) AS [Value] FROM dbo.OduncIslemleri WHERE iade_tarihi < odunc_tarihi",
                "loans returned before they were lent"),
            ("SELECT COUNT(*) AS [Value] FROM (SELECT kitap_id FROM dbo.OduncIslemleri WHERE iade_tarihi IS NULL GROUP BY kitap_id HAVING COUNT(*) > 1) d",
                "books with more than one open loan"),
            ("SELECT COUNT(*) AS [Value] FROM dbo.Kitaplar WHERE LEN(ad) > 100 OR LEN(yazar) > 100",
                "books with a title or author longer than 100 characters"),
            ("SELECT COUNT(*) AS [Value] FROM dbo.Uyeler WHERE LEN(ad) > 50 OR LEN(soyad) > 50",
                "members with a name longer than 50 characters"),
        ];

        var problems = new List<string>();
        foreach (var (sql, problem) in checks)
        {
            var count = await ScalarAsync(db, sql, cancellationToken).ConfigureAwait(false);
            if (count > 0)
            {
                problems.Add($"{count} {problem}");
            }
        }

        if (problems.Count > 0)
        {
            throw new InvalidOperationException(
                "The existing library database cannot be upgraded until these rows are corrected: "
                + string.Join("; ", problems) + ". No changes were made.");
        }
    }

    /// <summary>
    /// Brings the legacy tables to the shape created by <see cref="InitialCreate"/>. Each statement
    /// is guarded so it only runs when the corresponding part of the schema is missing.
    /// </summary>
    private static readonly string[] UpgradeStatements =
    [
        // Missing values in the original nullable columns: empty text, and availability derived from open loans.
        """
        UPDATE dbo.Kitaplar SET ad = N'' WHERE ad IS NULL;
        UPDATE dbo.Kitaplar SET yazar = N'' WHERE yazar IS NULL;
        UPDATE k SET durum = CASE WHEN EXISTS (SELECT 1 FROM dbo.OduncIslemleri o WHERE o.kitap_id = k.kitap_id AND o.iade_tarihi IS NULL) THEN 0 ELSE 1 END
        FROM dbo.Kitaplar k WHERE k.durum IS NULL;
        UPDATE dbo.Uyeler SET ad = N'' WHERE ad IS NULL;
        UPDATE dbo.Uyeler SET soyad = N'' WHERE soyad IS NULL;
        """,

        // Foreign keys are dropped before their columns are altered and recreated under the model's names.
        DropForeignKeyIfUnnamed("kitap_id", "Kitaplar", "FK_OduncIslemleri_Kitaplar"),
        DropForeignKeyIfUnnamed("uye_id", "Uyeler", "FK_OduncIslemleri_Uyeler"),

        AlterColumn("Kitaplar", "ad", "nvarchar", 200, "NVARCHAR(100)"),
        AlterColumn("Kitaplar", "yazar", "nvarchar", 200, "NVARCHAR(100)"),
        AlterColumn("Kitaplar", "durum", "bit", 1, "BIT"),
        AlterColumn("Uyeler", "ad", "nvarchar", 100, "NVARCHAR(50)"),
        AlterColumn("Uyeler", "soyad", "nvarchar", 100, "NVARCHAR(50)"),
        AlterColumn("OduncIslemleri", "kitap_id", "int", 4, "INT"),
        AlterColumn("OduncIslemleri", "uye_id", "int", 4, "INT"),
        AlterColumn("OduncIslemleri", "odunc_tarihi", "datetime", 8, "DATETIME"),

        RenamePrimaryKey("Kitaplar", "PK_Kitaplar"),
        RenamePrimaryKey("Uyeler", "PK_Uyeler"),
        RenamePrimaryKey("OduncIslemleri", "PK_OduncIslemleri"),

        AddForeignKey("kitap_id", "Kitaplar", "FK_OduncIslemleri_Kitaplar"),
        AddForeignKey("uye_id", "Uyeler", "FK_OduncIslemleri_Uyeler"),

        """
        IF OBJECT_ID(N'dbo.CK_OduncIslemleri_iade_tarihi', N'C') IS NULL
            EXEC(N'ALTER TABLE dbo.OduncIslemleri WITH CHECK ADD CONSTRAINT CK_OduncIslemleri_iade_tarihi CHECK ([iade_tarihi] IS NULL OR [iade_tarihi] >= [odunc_tarihi])');
        """,

        AddRowVersion("Kitaplar"),
        AddRowVersion("Uyeler"),
        AddRowVersion("OduncIslemleri"),

        CreateIndex("Kitaplar", "IX_Kitaplar_ad", "CREATE INDEX IX_Kitaplar_ad ON dbo.Kitaplar (ad)"),
        CreateIndex("OduncIslemleri", "IX_OduncIslemleri_kitap_id", "CREATE INDEX IX_OduncIslemleri_kitap_id ON dbo.OduncIslemleri (kitap_id)"),
        CreateIndex("OduncIslemleri", "IX_OduncIslemleri_uye_id", "CREATE INDEX IX_OduncIslemleri_uye_id ON dbo.OduncIslemleri (uye_id)"),
        CreateIndex(
            "OduncIslemleri",
            "UX_OduncIslemleri_active_kitap",
            "CREATE UNIQUE INDEX UX_OduncIslemleri_active_kitap ON dbo.OduncIslemleri (kitap_id) WHERE [iade_tarihi] IS NULL"),
        CreateIndex("Uyeler", "IX_Uyeler_soyad_ad", "CREATE INDEX IX_Uyeler_soyad_ad ON dbo.Uyeler (soyad, ad)"),
    ];

    private static string AlterColumn(string table, string column, string typeName, int maxLength, string definition) =>
        $"""
        IF EXISTS (
            SELECT 1 FROM sys.columns c JOIN sys.types t ON t.user_type_id = c.user_type_id
            WHERE c.object_id = OBJECT_ID(N'dbo.{table}') AND c.name = N'{column}'
              AND (c.is_nullable = 1 OR t.name <> N'{typeName}' OR c.max_length <> {maxLength}))
            EXEC(N'ALTER TABLE dbo.{table} ALTER COLUMN {column} {definition} NOT NULL');
        """;

    private static string RenamePrimaryKey(string table, string name) =>
        $"""
        DECLARE @current sysname = (
            SELECT name FROM sys.key_constraints WHERE parent_object_id = OBJECT_ID(N'dbo.{table}') AND type = 'PK');
        IF @current IS NOT NULL AND @current <> N'{name}'
        BEGIN
            DECLARE @qualified nvarchar(300) = N'dbo.' + QUOTENAME(@current);
            EXEC sp_rename @objname = @qualified, @newname = N'{name}', @objtype = N'OBJECT';
        END
        """;

    /// <summary>Drops foreign keys on the column that do not carry the model's constraint name.</summary>
    private static string DropForeignKeyIfUnnamed(string column, string principalTable, string name) =>
        $"""
        DECLARE @drop nvarchar(max) = N'';
        SELECT @drop += N'ALTER TABLE dbo.OduncIslemleri DROP CONSTRAINT ' + QUOTENAME(fk.name) + N';'
        FROM sys.foreign_keys fk
        JOIN sys.foreign_key_columns fkc ON fkc.constraint_object_id = fk.object_id
        JOIN sys.columns c ON c.object_id = fkc.parent_object_id AND c.column_id = fkc.parent_column_id
        WHERE fk.parent_object_id = OBJECT_ID(N'dbo.OduncIslemleri')
          AND fk.referenced_object_id = OBJECT_ID(N'dbo.{principalTable}')
          AND c.name = N'{column}'
          AND fk.name <> N'{name}';
        IF @drop <> N'' EXEC(@drop);
        """;

    /// <summary>Adds the named, non-cascading foreign key when it is missing.</summary>
    private static string AddForeignKey(string column, string principalTable, string name) =>
        $"""
        IF OBJECT_ID(N'dbo.{name}', N'F') IS NULL
            EXEC(N'ALTER TABLE dbo.OduncIslemleri WITH CHECK ADD CONSTRAINT {name} FOREIGN KEY ({column}) REFERENCES dbo.{principalTable} ({column})');
        """;

    private static string AddRowVersion(string table) =>
        $"""
        IF COL_LENGTH(N'dbo.{table}', N'{LibraryDbContext.RowVersion}') IS NULL
            EXEC(N'ALTER TABLE dbo.{table} ADD {LibraryDbContext.RowVersion} ROWVERSION');
        """;

    private static string CreateIndex(string table, string name, string createStatement) =>
        $"""
        IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID(N'dbo.{table}') AND name = N'{name}')
            EXEC(N'{createStatement}');
        """;

    private static async Task MarkBaselineAppliedAsync(LibraryDbContext db, CancellationToken cancellationToken)
    {
        var history = db.GetService<IHistoryRepository>();
        await ExecuteAsync(db, history.GetCreateIfNotExistsScript(), cancellationToken).ConfigureAwait(false);
        await ExecuteAsync(
            db,
            history.GetInsertScript(new HistoryRow(BaselineMigrationId, ProductInfo.GetVersion())),
            cancellationToken).ConfigureAwait(false);
    }

    private static Task<int> ScalarAsync(LibraryDbContext db, string sql, CancellationToken cancellationToken) =>
        db.Database.SqlQueryRaw<int>(sql).SingleAsync(cancellationToken);

    private static Task<int> ExecuteAsync(LibraryDbContext db, string sql, CancellationToken cancellationToken) =>
#pragma warning disable EF1002 // The statements are built from constants in this class, never from user input.
        db.Database.ExecuteSqlRawAsync(sql, cancellationToken);
#pragma warning restore EF1002
}

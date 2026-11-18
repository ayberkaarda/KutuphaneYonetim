using DbUp;
using DbUp.Engine;

namespace KutuphaneYonetim.Data;

/// <summary>Applies the embedded SQL scripts under Database/Scripts with DbUp.</summary>
public interface IDatabaseMigrator
{
    /// <summary>
    /// Creates the database when it does not exist and runs every script that has not been applied yet.
    /// </summary>
    void Migrate();
}

internal sealed class DatabaseMigrator(string connectionString) : IDatabaseMigrator
{
    private const string ScriptNamespace = "KutuphaneYonetim.Data.Database.Scripts.";

    public void Migrate()
    {
        EnsureDatabase.For.SqlDatabase(connectionString);

        UpgradeEngine engine = DeployChanges.To
            .SqlDatabase(connectionString)
            .WithScriptsEmbeddedInAssembly(
                typeof(DatabaseMigrator).Assembly,
                name => name.StartsWith(ScriptNamespace, StringComparison.Ordinal))
            .WithTransactionPerScript()
            .LogToNowhere()
            .Build();

        DatabaseUpgradeResult result = engine.PerformUpgrade();
        if (!result.Successful)
        {
            throw new DatabaseMigrationException(result.ErrorScript?.Name, result.Error);
        }
    }
}

/// <summary>Raised when a migration script fails.</summary>
public sealed class DatabaseMigrationException(string? scriptName, Exception? innerException)
    : Exception($"Database migration failed at script '{scriptName ?? "unknown"}'.", innerException)
{
    public string? ScriptName { get; } = scriptName;
}

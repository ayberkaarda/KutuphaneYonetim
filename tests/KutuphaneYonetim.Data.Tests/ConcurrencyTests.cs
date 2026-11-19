using KutuphaneYonetim.Business;

namespace KutuphaneYonetim.Data.Tests;

/// <summary>
/// Interleaves two operations on SQL Server at a fixed point: the first one stops before a given command
/// (<see cref="WaitPoint"/>), the second one runs until it finishes or waits for a lock, then the first one
/// continues. Seed: book 3 is on loan to member 2; members 1 and 3 are free.
/// </summary>
public sealed partial class ConcurrencyTests
{
    private static readonly DateTime Start = new(2026, 6, 1, 10, 0, 0, DateTimeKind.Unspecified);

    private static readonly LibraryPolicy Policy = new()
    {
        LoanPeriodDays = 14,
        DailyFine = 1.50m,
        MaxUnpaidFine = 100m,
        ReservationHoldDays = 2,
    };

    private readonly ManualClock _clock = new(Start);

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private Task<SqlServerDatabase> CreateDatabaseAsync() => SqlServerDatabase.CreateAsync(_clock, Policy, Ct);

    private static async Task RejectedAsync(Func<Task> operation)
    {
        try
        {
            await operation();
        }
        catch (BusinessRuleException)
        {
            // A rule rejection is an acceptable outcome of the race; the database state is checked afterwards.
        }
    }
}

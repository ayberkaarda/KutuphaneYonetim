using System.Data.Common;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace KutuphaneYonetim.Data.Tests;

/// <summary>
/// Stops one operation just before the first database command that matches a predicate, until the test
/// releases it. Only commands issued by the operation started through <see cref="Run{T}"/> are matched,
/// so other operations running at the same time pass through.
/// </summary>
internal sealed class WaitPoint(Func<DbCommand, bool> match)
{
    private static readonly AsyncLocal<WaitPoint?> Armed = new();
    private static readonly TimeSpan WaitLimit = TimeSpan.FromSeconds(30);

    private readonly Func<DbCommand, bool> _match = match;
    private readonly TaskCompletionSource _reached = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private readonly TaskCompletionSource _released = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private int _hit;

    /// <summary>A command whose text contains every one of the given fragments.</summary>
    public static WaitPoint BeforeCommandContaining(params string[] fragments) =>
        new(command => fragments.All(f => command.CommandText.Contains(f, StringComparison.Ordinal)));

    /// <summary>Starts <paramref name="operation"/> on the thread pool with this wait point armed.</summary>
    public Task<T> Run<T>(Func<Task<T>> operation) => Task.Run(() =>
    {
        Armed.Value = this;
        return operation();
    });

    /// <summary>Starts <paramref name="operation"/> on the thread pool with this wait point armed.</summary>
    public Task Run(Func<Task> operation) => Task.Run(() =>
    {
        Armed.Value = this;
        return operation();
    });

    /// <summary>Waits until the operation stops at the wait point; fails when it ends without reaching it.</summary>
    public async Task WaitUntilReachedAsync(Task operation)
    {
        var first = await Task.WhenAny(_reached.Task, operation).WaitAsync(WaitLimit);
        if (first != _reached.Task)
        {
            await operation;
            Assert.Fail("The operation finished without reaching the wait point.");
        }
    }

    public void Release() => _released.TrySetResult();

    internal static async ValueTask PauseIfArmedAsync(DbCommand command, CancellationToken cancellationToken)
    {
        if (Armed.Value is not { } point || !point._match(command) || Interlocked.Exchange(ref point._hit, 1) == 1)
        {
            return;
        }

        point._reached.TrySetResult();
        await point._released.Task.WaitAsync(WaitLimit, cancellationToken);
    }
}

/// <summary>Lets <see cref="WaitPoint"/> stop commands before they are sent to the server.</summary>
internal sealed class WaitPointInterceptor : DbCommandInterceptor
{
    public static WaitPointInterceptor Instance { get; } = new();

    public override async ValueTask<InterceptionResult<DbDataReader>> ReaderExecutingAsync(
        DbCommand command, CommandEventData eventData, InterceptionResult<DbDataReader> result, CancellationToken cancellationToken = default)
    {
        await WaitPoint.PauseIfArmedAsync(command, cancellationToken);
        return result;
    }

    public override async ValueTask<InterceptionResult<int>> NonQueryExecutingAsync(
        DbCommand command, CommandEventData eventData, InterceptionResult<int> result, CancellationToken cancellationToken = default)
    {
        await WaitPoint.PauseIfArmedAsync(command, cancellationToken);
        return result;
    }

    public override async ValueTask<InterceptionResult<object>> ScalarExecutingAsync(
        DbCommand command, CommandEventData eventData, InterceptionResult<object> result, CancellationToken cancellationToken = default)
    {
        await WaitPoint.PauseIfArmedAsync(command, cancellationToken);
        return result;
    }
}

using KutuphaneYonetim.Data.Persistence;
using Microsoft.EntityFrameworkCore;

namespace KutuphaneYonetim.Data.Repositories;

/// <summary>
/// Serialises the operations that move a book between shelf, loan and reservation queue (lend, return,
/// reserve, cancel, hold expiry): each takes this lock first inside its transaction and only then reads the
/// state it decides on, so no other such operation on the same book can change that state before it commits.
/// </summary>
internal static class BookLock
{
    private const int TimeoutMilliseconds = 30_000;

    /// <summary>
    /// Takes an exclusive application lock for the book, owned by the current transaction and released when
    /// it ends. Only SQL Server needs it: SQLite (used by the tests) already serialises writing transactions.
    /// </summary>
    public static async Task AcquireAsync(LibraryDbContext db, int bookId, CancellationToken cancellationToken)
    {
        if (!db.Database.IsSqlServer())
        {
            return;
        }

        var resource = "KutuphaneYonetim.Book." + bookId.ToString(System.Globalization.CultureInfo.InvariantCulture);
        await db.Database.ExecuteSqlAsync(
            $"""
            DECLARE @result int;
            EXEC @result = sp_getapplock @Resource = {resource}, @LockMode = 'Exclusive', @LockOwner = 'Transaction', @LockTimeout = {TimeoutMilliseconds};
            IF @result < 0 THROW 51000, 'The book is locked by another operation.', 1;
            """,
            cancellationToken).ConfigureAwait(false);
    }

    /// <summary>Takes the locks of several books in ascending order, so two callers never wait on each other.</summary>
    public static async Task AcquireAsync(LibraryDbContext db, IEnumerable<int> bookIds, CancellationToken cancellationToken)
    {
        foreach (var bookId in bookIds.Distinct().Order())
        {
            await AcquireAsync(db, bookId, cancellationToken).ConfigureAwait(false);
        }
    }
}

using KutuphaneYonetim.Business.Services;
using KutuphaneYonetim.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace KutuphaneYonetim.Data.Tests;

public sealed partial class ConcurrencyTests
{
    [Fact]
    public async Task Reserve_ConcurrentReturn_DoesNotLeaveUnservedShelfQueue()
    {
        await using var db = await CreateDatabaseAsync();

        // Member 1 reserves book 3 (on loan) and stops right before the insert; the book comes back meanwhile.
        var pause = WaitPoint.BeforeCommandContaining("INSERT INTO [Rezervasyonlar]");
        var reserve = pause.Run(() => RejectedAsync(() => db.Get<IReservationService>().ReserveAsync(3, 1, Ct)));
        await pause.WaitUntilReachedAsync(reserve);
        var returned = db.Get<ILoanService>().ReturnAsync(3, Ct);
        await db.WaitUntilFinishedOrBlockedAsync(returned, Ct);
        pause.Release();

        await reserve;
        await returned;

        // Either the reservation was refused, or the queue of the book on the shelf has its first member served.
        await using var context = db.CreateContext();
        var active = await context.Reservations
            .Where(r => r.BookId == 3 && (r.Status == ReservationStatus.Waiting || r.Status == ReservationStatus.Ready))
            .ToListAsync(Ct);
        var onLoan = await context.Loans.AnyAsync(l => l.BookId == 3 && l.ReturnDate == null, Ct);
        if (active.Count > 0)
        {
            Assert.True(onLoan || active.Any(r => r.Status == ReservationStatus.Ready), "A waiting queue was left on a book on the shelf.");
        }
    }

    [Fact]
    public async Task ExpireHolds_BlockedBook_DoesNotHoldCompletedBooks()
    {
        await using var db = await CreateDatabaseAsync();
        await AddBooksAsync(db, 10, 20);
        await HoldForMember1WithMember3WaitingAsync(db, 10);
        await HoldForMember1WithMember3WaitingAsync(db, 20);
        _clock.AdvanceDays(3);

        // Another session keeps book 20 locked while the expiry of books 10 and 20 runs.
        await using var other = db.CreateContext();
        await using var otherTransaction = await other.Database.BeginTransactionAsync(Ct);
        await other.Database.ExecuteSqlRawAsync(
            "EXEC sp_getapplock @Resource = 'KutuphaneYonetim.Book.20', @LockMode = 'Exclusive', @LockOwner = 'Transaction'", Ct);

        var expire = Task.Run(() => db.Get<IReservationService>().ExpireHoldsAsync(Ct), Ct);
        await db.WaitUntilFinishedOrBlockedAsync(expire, Ct);
        Assert.False(expire.IsCompleted, "The expiry did not wait for book 20.");

        // While the expiry waits on book 20, member 3's reservation of book 10 can be cancelled.
        int member3Hold10;
        await using (var context = db.CreateContext())
        {
            member3Hold10 = (await context.Reservations.SingleAsync(r => r.BookId == 10 && r.MemberId == 3, Ct)).Id;
        }

        var cancel = db.Get<IReservationService>().CancelAsync(member3Hold10, Ct);
        var first = await Task.WhenAny(cancel, Task.Delay(TimeSpan.FromSeconds(10), Ct));
        Assert.True(first == cancel, "Cancelling on book 10 waited for the lock of book 20.");
        await cancel;

        await using (var context = db.CreateContext())
        {
            var book10 = await context.Reservations.AsNoTracking().Where(r => r.BookId == 10).ToDictionaryAsync(r => r.MemberId, Ct);
            Assert.Equal(ReservationStatus.Expired, book10[1].Status);
            Assert.Equal(ReservationStatus.Cancelled, book10[3].Status);
        }

        Assert.False(expire.IsCompleted);
        await otherTransaction.RollbackAsync(Ct);
        Assert.Equal(2, await expire);

        await using (var context = db.CreateContext())
        {
            var book20 = await context.Reservations.AsNoTracking().Where(r => r.BookId == 20).ToDictionaryAsync(r => r.MemberId, Ct);
            Assert.Equal(ReservationStatus.Expired, book20[1].Status);
            Assert.Equal(ReservationStatus.Ready, book20[3].Status);
            Assert.False(await context.Reservations.AnyAsync(r => r.BookId == 10 && r.Status == ReservationStatus.Ready, Ct));
        }
    }

    /// <summary>Adds books on the shelf with the given ids.</summary>
    private static async Task AddBooksAsync(SqlServerDatabase db, params int[] ids)
    {
        await using var context = db.CreateContext();
        foreach (var id in ids)
        {
            await context.Database.ExecuteSqlAsync(
                $"SET IDENTITY_INSERT [Kitaplar] ON; INSERT INTO [Kitaplar] ([kitap_id], [ad], [yazar], [durum]) VALUES ({id}, {"Book " + id}, {"Author"}, 1); SET IDENTITY_INSERT [Kitaplar] OFF;",
                Ct);
        }
    }

    /// <summary>Book is lent to member 2; members 1 and 3 queue; it is returned and held for member 1.</summary>
    private async Task HoldForMember1WithMember3WaitingAsync(SqlServerDatabase db, int bookId)
    {
        await db.Get<ILoanService>().LendAsync(bookId, 2, cancellationToken: Ct);
        var reservations = db.Get<IReservationService>();
        await reservations.ReserveAsync(bookId, 1, Ct);
        _clock.Now = _clock.Now.AddMinutes(1);
        await reservations.ReserveAsync(bookId, 3, Ct);
        Assert.NotNull((await db.Get<ILoanService>().ReturnAsync(bookId, Ct)).HeldForMemberName);
    }
}

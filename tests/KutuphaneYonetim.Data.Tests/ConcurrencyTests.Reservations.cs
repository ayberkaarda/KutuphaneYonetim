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
}

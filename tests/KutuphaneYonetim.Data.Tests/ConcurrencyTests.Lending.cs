using KutuphaneYonetim.Business.Services;
using KutuphaneYonetim.Domain.Entities;
using KutuphaneYonetim.Domain.Repositories;
using Microsoft.EntityFrameworkCore;

namespace KutuphaneYonetim.Data.Tests;

public sealed partial class ConcurrencyTests
{
    [Fact]
    public async Task Lend_WhenHoldExpiresAfterRead_DoesNotBorrowForPreviousHolder()
    {
        await using var db = await CreateDatabaseAsync();
        await HoldBook3ForMember1WithMember3WaitingAsync(db);

        // Lending to member 1 stops after it has seen member 1's hold; the hold expires meanwhile.
        var pause = WaitPoint.BeforeCommandContaining("FROM [Rezervasyonlar]", "[uye_id] = @");
        var lend = pause.Run(() => db.Get<ILoanRepository>().LendAsync(3, 1, _clock.Now, _clock.Now.Date.AddDays(14), Ct));
        await pause.WaitUntilReachedAsync(lend);
        _clock.AdvanceDays(3);
        var expire = db.Get<IReservationService>().ExpireHoldsAsync(Ct);
        await db.WaitUntilFinishedOrBlockedAsync(expire, Ct);
        pause.Release();

        await lend;
        await expire;
        await AssertBook3ConsistentAsync(db);
    }

    [Fact]
    public async Task Lend_WhenHoldIsCancelledAfterRead_DoesNotBorrowForPreviousHolder()
    {
        await using var db = await CreateDatabaseAsync();
        var holdId = await HoldBook3ForMember1WithMember3WaitingAsync(db);

        // Lending to member 1 stops after it has seen member 1's hold; member 1 cancels meanwhile.
        var pause = WaitPoint.BeforeCommandContaining("FROM [Rezervasyonlar]", "[uye_id] = @");
        var lend = pause.Run(() => db.Get<ILoanRepository>().LendAsync(3, 1, _clock.Now, _clock.Now.Date.AddDays(14), Ct));
        await pause.WaitUntilReachedAsync(lend);
        var cancel = RejectedAsync(() => db.Get<IReservationService>().CancelAsync(holdId, Ct));
        await db.WaitUntilFinishedOrBlockedAsync(cancel, Ct);
        pause.Release();

        await lend;
        await cancel;
        await AssertBook3ConsistentAsync(db);
    }

    /// <summary>Members 1 and 3 queue for book 3; it is returned and held for member 1. Returns the hold id.</summary>
    private async Task<int> HoldBook3ForMember1WithMember3WaitingAsync(SqlServerDatabase db)
    {
        var reservations = db.Get<IReservationService>();
        await reservations.ReserveAsync(3, 1, Ct);
        _clock.Now = _clock.Now.AddMinutes(1);
        await reservations.ReserveAsync(3, 3, Ct);
        Assert.NotNull((await db.Get<ILoanService>().ReturnAsync(3, Ct)).HeldForMemberName);

        await using var context = db.CreateContext();
        return (await context.Reservations.SingleAsync(r => r.MemberId == 1 && r.Status == ReservationStatus.Ready, Ct)).Id;
    }

    /// <summary>
    /// Book 3 is never lent while it is held for someone, and a member who borrowed it had their reservation
    /// fulfilled (not expired or cancelled under their feet).
    /// </summary>
    private static async Task AssertBook3ConsistentAsync(SqlServerDatabase db)
    {
        await using var context = db.CreateContext();
        var loan = await context.Loans.SingleOrDefaultAsync(l => l.BookId == 3 && l.ReturnDate == null, Ct);
        if (loan is null)
        {
            return;
        }

        Assert.Empty(await context.Reservations.Where(r => r.BookId == 3 && r.Status == ReservationStatus.Ready).ToListAsync(Ct));
        var borrowerReservation = await context.Reservations.SingleAsync(r => r.BookId == 3 && r.MemberId == loan.MemberId, Ct);
        Assert.Equal(ReservationStatus.Fulfilled, borrowerReservation.Status);
    }
}

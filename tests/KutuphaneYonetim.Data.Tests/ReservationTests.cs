using KutuphaneYonetim.Business;
using KutuphaneYonetim.Business.Services;
using KutuphaneYonetim.Domain;
using KutuphaneYonetim.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace KutuphaneYonetim.Data.Tests;

/// <summary>
/// The reservation queue end to end on SQLite in-memory. Seed: book 3 is on loan to member 2;
/// members 1 ("Örnek Üye") and 3 ("Test Okur") are free. The hold lasts two days.
/// </summary>
public sealed class ReservationTests : IAsyncLifetime
{
    private static readonly DateTime Start = new(2026, 6, 1, 10, 0, 0, DateTimeKind.Unspecified);

    private readonly ManualClock _clock = new(Start);
    private readonly SqliteDatabase _db;

    public ReservationTests()
    {
        _db = new SqliteDatabase(_clock, new LibraryPolicy { ReservationHoldDays = 2 });
    }

    public ValueTask InitializeAsync() => _db.InitializeAsync();

    public ValueTask DisposeAsync() => _db.DisposeAsync();

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private IReservationService Reservations => _db.Get<IReservationService>();

    private ILoanService Loans => _db.Get<ILoanService>();

    private async Task<ReservationDetail[]> ActiveAsync() => (await Reservations.GetActiveAsync(Ct)).ToArray();

    [Fact]
    public async Task Return_HoldsBookForFirstInQueue_AndOnlyThatMemberCanBorrowIt()
    {
        // Same timestamp: the queue falls back to the reservation id.
        await Reservations.ReserveAsync(3, 1, Ct);
        await Reservations.ReserveAsync(3, 3, Ct);
        Assert.Equal([1, 3], (await ActiveAsync()).Select(r => r.MemberId));
        Assert.Equal([1, 2], (await ActiveAsync()).Select(r => r.QueuePosition));

        _clock.AdvanceDays(1);
        var receipt = await Loans.ReturnAsync(3, Ct);
        Assert.Equal("Örnek Üye", receipt.HeldForMemberName);
        Assert.Equal(_clock.Now.AddDays(2), receipt.HeldUntil);

        var held = (await ActiveAsync()).Single(r => r.MemberId == 1);
        Assert.Equal(ReservationStatus.Ready, held.Status);
        Assert.Equal(0, held.QueuePosition);

        // Neither the next member in the queue nor anyone else may take the held book.
        var refused = await Assert.ThrowsAsync<BusinessRuleException>(() => Loans.LendAsync(3, 3, cancellationToken: Ct));
        Assert.Contains("ayrıldı", refused.Message, StringComparison.Ordinal);
        await Assert.ThrowsAsync<BusinessRuleException>(() => Loans.LendAsync(3, 2, cancellationToken: Ct));

        // The holder borrows it; their reservation is fulfilled and member 3 is next.
        await Loans.LendAsync(3, 1, cancellationToken: Ct);
        var remaining = Assert.Single(await ActiveAsync());
        Assert.Equal((3, ReservationStatus.Waiting, 1), (remaining.MemberId, remaining.Status, remaining.QueuePosition));

        await using var context = _db.CreateContext();
        Assert.Equal(
            ReservationStatus.Fulfilled,
            (await context.Reservations.SingleAsync(r => r.MemberId == 1, Ct)).Status);
    }

    [Fact]
    public async Task SinglePersonQueue_HoldExpiresAfterHoldPeriod_ThenBookIsFree()
    {
        await Reservations.ReserveAsync(3, 1, Ct);
        await Loans.ReturnAsync(3, Ct);
        var expiresAt = Assert.Single(await ActiveAsync()).ExpiresAt;
        Assert.Equal(Start.AddDays(2), expiresAt);
        Assert.Equal([3], (await _db.Get<IBookService>().SearchAsync(new BookQuery(Availability: BookAvailability.Reserved), Ct)).Items.Select(b => b.Id));

        // Exactly at the end of the hold the reservation is still valid.
        _clock.Now = expiresAt!.Value;
        Assert.Equal(ReservationStatus.Ready, Assert.Single(await ActiveAsync()).Status);
        await Assert.ThrowsAsync<BusinessRuleException>(() => Loans.LendAsync(3, 3, cancellationToken: Ct));

        // One second later it has expired; with nobody else waiting the book is free for everyone.
        _clock.Now = expiresAt.Value.AddSeconds(1);
        Assert.Equal(1, await Reservations.ExpireHoldsAsync(Ct));
        Assert.Empty(await ActiveAsync());
        Assert.Contains(3, (await _db.Get<IBookService>().SearchAsync(new BookQuery(Availability: BookAvailability.Available), Ct)).Items.Select(b => b.Id));
        await Loans.LendAsync(3, 3, cancellationToken: Ct);
    }

    [Fact]
    public async Task ExpiredHold_ServesNextMemberWithFreshHold()
    {
        await Reservations.ReserveAsync(3, 1, Ct);
        _clock.AdvanceDays(1);
        await Reservations.ReserveAsync(3, 3, Ct);
        await Loans.ReturnAsync(3, Ct);

        _clock.AdvanceDays(3); // Member 1 did not come in time.
        var next = Assert.Single(await ActiveAsync());

        Assert.Equal(3, next.MemberId);
        Assert.Equal(ReservationStatus.Ready, next.Status);
        Assert.Equal(_clock.Now, next.ReadyAt);
        Assert.Equal(_clock.Now.AddDays(2), next.ExpiresAt);
    }

    [Fact]
    public async Task ExpiredHold_IsProcessedWhenLending()
    {
        await Reservations.ReserveAsync(3, 1, Ct);
        await Loans.ReturnAsync(3, Ct);
        _clock.AdvanceDays(5);

        await Loans.LendAsync(3, 3, cancellationToken: Ct); // No explicit expiry call needed.

        Assert.Empty(await ActiveAsync());
    }

    [Fact]
    public async Task CancellingTheHold_ServesNext_CancellingAWaitingOneDoesNot()
    {
        await Reservations.ReserveAsync(3, 1, Ct);
        await Reservations.ReserveAsync(3, 3, Ct);

        var waiting = (await ActiveAsync()).Single(r => r.MemberId == 3);
        await Reservations.CancelAsync(waiting.Id, Ct);
        Assert.Equal([1], (await ActiveAsync()).Select(r => r.MemberId));
        await Assert.ThrowsAsync<BusinessRuleException>(() => Reservations.CancelAsync(waiting.Id, Ct)); // Already cancelled.

        await Reservations.ReserveAsync(3, 3, Ct); // Allowed again after the cancel.
        await Loans.ReturnAsync(3, Ct);
        var hold = (await ActiveAsync()).Single(r => r.Status == ReservationStatus.Ready);
        Assert.Equal(1, hold.MemberId);

        await Reservations.CancelAsync(hold.Id, Ct);
        var served = Assert.Single(await ActiveAsync());
        Assert.Equal((3, ReservationStatus.Ready), (served.MemberId, served.Status));
    }

    [Fact]
    public async Task CancellingAHold_WhileBookIsOnLoanAgain_DoesNotServeNext()
    {
        await Reservations.ReserveAsync(3, 1, Ct);
        await Reservations.ReserveAsync(3, 3, Ct);
        await Loans.ReturnAsync(3, Ct);
        await Loans.LendAsync(3, 1, cancellationToken: Ct);

        var waiting = Assert.Single(await ActiveAsync());
        Assert.Equal(ReservationStatus.Waiting, waiting.Status); // Stays waiting until the book comes back.
    }

    [Fact]
    public async Task Reserve_RejectsBookOnShelf_OwnLoan_AndDuplicates()
    {
        await Assert.ThrowsAsync<BusinessRuleException>(() => Reservations.ReserveAsync(4, 1, Ct)); // On the shelf.
        await Assert.ThrowsAsync<BusinessRuleException>(() => Reservations.ReserveAsync(3, 2, Ct)); // Member 2 has it.

        await Reservations.ReserveAsync(3, 1, Ct);
        var duplicate = await Assert.ThrowsAsync<BusinessRuleException>(() => Reservations.ReserveAsync(3, 1, Ct));
        Assert.Equal("Bu üyenin bu kitap için zaten aktif bir rezervasyonu var.", duplicate.Message);
        Assert.Single(await ActiveAsync());
    }

    [Fact]
    public async Task Database_RejectsSecondActiveReservationOfSameMember_AndSecondHold()
    {
        await using (var context = _db.CreateContext())
        {
            context.Reservations.Add(new Reservation { BookId = 3, MemberId = 1, CreatedAt = Start, Status = ReservationStatus.Waiting });
            await context.SaveChangesAsync(Ct);
            context.Reservations.Add(new Reservation { BookId = 3, MemberId = 1, CreatedAt = Start, Status = ReservationStatus.Waiting });
            await Assert.ThrowsAsync<DbUpdateException>(() => context.SaveChangesAsync(Ct));
        }

        await using (var context = _db.CreateContext())
        {
            // Closed reservations do not count: the same member may reserve again.
            context.Reservations.Add(new Reservation { BookId = 3, MemberId = 3, CreatedAt = Start, Status = ReservationStatus.Cancelled });
            context.Reservations.Add(new Reservation { BookId = 3, MemberId = 3, CreatedAt = Start, Status = ReservationStatus.Waiting });
            await context.SaveChangesAsync(Ct);
        }

        await using (var context = _db.CreateContext())
        {
            context.Reservations.Add(new Reservation
            {
                BookId = 5, MemberId = 1, CreatedAt = Start, Status = ReservationStatus.Ready, ReadyAt = Start, ExpiresAt = Start,
            });
            context.Reservations.Add(new Reservation
            {
                BookId = 5, MemberId = 3, CreatedAt = Start, Status = ReservationStatus.Ready, ReadyAt = Start, ExpiresAt = Start,
            });
            await Assert.ThrowsAsync<DbUpdateException>(() => context.SaveChangesAsync(Ct));
        }
    }

    [Fact]
    public async Task MemberWithReservationHistory_CannotBeDeleted()
    {
        await Reservations.ReserveAsync(3, 3, Ct);
        await Reservations.CancelAsync(Assert.Single(await ActiveAsync()).Id, Ct);

        var error = await Assert.ThrowsAsync<BusinessRuleException>(() => _db.Get<IMemberService>().DeleteAsync(3, Ct));

        Assert.Equal("Bu üyenin rezervasyon kaydı bulunduğu için silinemez.", error.Message);
    }
}

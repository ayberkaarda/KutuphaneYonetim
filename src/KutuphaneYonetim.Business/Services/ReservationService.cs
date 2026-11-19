using KutuphaneYonetim.Business.Validation;
using KutuphaneYonetim.Domain.Entities;
using KutuphaneYonetim.Domain.Repositories;

namespace KutuphaneYonetim.Business.Services;

/// <summary>
/// Reservation queue for books that are on loan. When such a book is returned it is held for the first
/// member in the queue for <see cref="LibraryPolicy.ReservationHoldDays"/> days; nobody else can borrow it
/// meanwhile. A hold that is not picked up in time expires and the next member is served.
/// </summary>
public interface IReservationService
{
    /// <summary>Waiting and ready reservations with their queue positions (expired holds are processed first).</summary>
    Task<IReadOnlyList<ReservationDetail>> GetActiveAsync(CancellationToken cancellationToken = default);

    Task ReserveAsync(int bookId, int memberId, CancellationToken cancellationToken = default);

    Task CancelAsync(int reservationId, CancellationToken cancellationToken = default);

    /// <summary>Expires holds that were not picked up in time. Returns the number of expired reservations.</summary>
    Task<int> ExpireHoldsAsync(CancellationToken cancellationToken = default);
}

internal sealed class ReservationService(
    IBookRepository books,
    IMemberRepository members,
    ILoanRepository loans,
    IReservationRepository reservations,
    LibraryPolicy policy,
    TimeProvider timeProvider) : IReservationService
{
    private const string AlreadyReserved = "Bu üyenin bu kitap için zaten aktif bir rezervasyonu var.";

    public async Task<IReadOnlyList<ReservationDetail>> GetActiveAsync(CancellationToken cancellationToken = default)
    {
        await ExpireHoldsAsync(cancellationToken).ConfigureAwait(false);

        var active = await reservations.GetActiveDetailsAsync(cancellationToken).ConfigureAwait(false);
        foreach (var queue in active.GroupBy(r => r.BookId))
        {
            var position = 1;
            foreach (var reservation in queue.OrderBy(r => r.CreatedAt).ThenBy(r => r.Id))
            {
                reservation.QueuePosition = reservation.Status == ReservationStatus.Ready ? 0 : position++;
            }
        }

        return active;
    }

    public async Task ReserveAsync(int bookId, int memberId, CancellationToken cancellationToken = default)
    {
        var book = await books.GetByIdAsync(bookId, cancellationToken).ConfigureAwait(false)
            ?? throw new BusinessRuleException(Messages.BookNotFound);

        _ = await members.GetByIdAsync(memberId, cancellationToken).ConfigureAwait(false)
            ?? throw new BusinessRuleException(Messages.MemberNotFound);

        await ExpireHoldsAsync(cancellationToken).ConfigureAwait(false);

        var loan = await loans.GetActiveLoanForBookAsync(bookId, cancellationToken).ConfigureAwait(false);
        if (loan?.MemberId == memberId)
        {
            throw new BusinessRuleException("Bu kitap zaten bu üyede; kendi ödünç aldığı kitabı rezerve edemez.");
        }

        if (loan is null && book.IsAvailable
            && await reservations.GetHoldAsync(bookId, cancellationToken).ConfigureAwait(false) is null)
        {
            throw new BusinessRuleException("Bu kitap rafta. Rezervasyon yerine doğrudan ödünç verilebilir.");
        }

        if (await reservations.HasActiveAsync(bookId, memberId, cancellationToken).ConfigureAwait(false))
        {
            throw new BusinessRuleException(AlreadyReserved);
        }

        // The filtered unique index catches a duplicate made concurrently.
        _ = await reservations.AddAsync(bookId, memberId, Now(), cancellationToken).ConfigureAwait(false)
            ?? throw new BusinessRuleException(AlreadyReserved);
    }

    public async Task CancelAsync(int reservationId, CancellationToken cancellationToken = default)
    {
        var now = Now();
        if (!await reservations.CancelAsync(reservationId, now, HoldEnd(now), cancellationToken).ConfigureAwait(false))
        {
            throw new BusinessRuleException("Seçilen rezervasyon bulunamadı ya da artık aktif değil.");
        }
    }

    public Task<int> ExpireHoldsAsync(CancellationToken cancellationToken = default)
    {
        var now = Now();
        return reservations.ExpireHoldsAsync(now, HoldEnd(now), cancellationToken);
    }

    private DateTime HoldEnd(DateTime from) => from.AddDays(policy.ReservationHoldDays);

    private DateTime Now() => timeProvider.GetLocalNow().DateTime;
}

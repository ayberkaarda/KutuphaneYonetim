using KutuphaneYonetim.Domain.Entities;

namespace KutuphaneYonetim.Domain.Repositories;

/// <summary>Result of <see cref="IReservationRepository.AddAsync"/>.</summary>
public enum ReserveOutcome
{
    Added,
    BookNotFound,

    /// <summary>The book is on the shelf and not held: it can be lent right away instead.</summary>
    OnShelf,

    /// <summary>The member has the book on loan.</summary>
    BorrowedByMember,

    /// <summary>The member already has an active reservation for the book.</summary>
    AlreadyActive,
}

public interface IReservationRepository
{
    /// <summary>Waiting and ready reservations, ordered by book and then by queue order (created, id).</summary>
    Task<IReadOnlyList<ReservationDetail>> GetActiveDetailsAsync(CancellationToken cancellationToken = default);

    Task<bool> HasActiveAsync(int bookId, int memberId, CancellationToken cancellationToken = default);

    Task<bool> HasAnyForBookAsync(int bookId, CancellationToken cancellationToken = default);

    Task<bool> HasAnyForMemberAsync(int memberId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Adds a waiting reservation when the book is on loan to someone else or held for another member.
    /// The checks and the insert run in one transaction that is serialised with lending, returns,
    /// cancellations and hold expiry of the same book, so a book that comes back concurrently is either
    /// seen on the shelf (<see cref="ReserveOutcome.OnShelf"/>) or returned after the insert and served to
    /// the queue. A second active reservation of the same member is refused (also by a filtered unique index).
    /// </summary>
    Task<ReserveOutcome> AddAsync(int bookId, int memberId, DateTime createdAt, CancellationToken cancellationToken = default);

    /// <summary>
    /// Cancels an active reservation. When the book was held for it and is on the shelf, the next waiting
    /// member is served with a hold until <paramref name="nextExpiresAt"/>. Returns false when the
    /// reservation is not active.
    /// </summary>
    Task<bool> CancelAsync(int reservationId, DateTime now, DateTime nextExpiresAt, CancellationToken cancellationToken = default);

    /// <summary>
    /// Expires holds whose <see cref="Reservation.ExpiresAt"/> lies before <paramref name="now"/> and serves
    /// the next waiting member of each such book with a hold until <paramref name="nextExpiresAt"/>.
    /// Returns the number of expired reservations.
    /// </summary>
    Task<int> ExpireHoldsAsync(DateTime now, DateTime nextExpiresAt, CancellationToken cancellationToken = default);
}

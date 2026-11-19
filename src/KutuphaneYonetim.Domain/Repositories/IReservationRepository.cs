using KutuphaneYonetim.Domain.Entities;

namespace KutuphaneYonetim.Domain.Repositories;

public interface IReservationRepository
{
    /// <summary>Waiting and ready reservations, ordered by book and then by queue order (created, id).</summary>
    Task<IReadOnlyList<ReservationDetail>> GetActiveDetailsAsync(CancellationToken cancellationToken = default);

    /// <summary>The reservation the book is currently held for, or null.</summary>
    Task<Reservation?> GetHoldAsync(int bookId, CancellationToken cancellationToken = default);

    Task<bool> HasActiveAsync(int bookId, int memberId, CancellationToken cancellationToken = default);

    Task<bool> HasAnyForBookAsync(int bookId, CancellationToken cancellationToken = default);

    Task<bool> HasAnyForMemberAsync(int memberId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Adds a waiting reservation. Returns null when the member already has an active reservation for the
    /// book (also enforced by a filtered unique index).
    /// </summary>
    Task<int?> AddAsync(int bookId, int memberId, DateTime createdAt, CancellationToken cancellationToken = default);

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

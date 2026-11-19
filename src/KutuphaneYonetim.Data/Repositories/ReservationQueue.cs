using System.Linq.Expressions;
using KutuphaneYonetim.Data.Persistence;
using KutuphaneYonetim.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace KutuphaneYonetim.Data.Repositories;

/// <summary>Queue operations shared by the loan and reservation repositories. Callers own the transaction.</summary>
internal static class ReservationQueue
{
    /// <summary>Translatable form of <see cref="Reservation.IsActive"/>.</summary>
    public static Expression<Func<Reservation, bool>> Active { get; } =
        r => r.Status == ReservationStatus.Waiting || r.Status == ReservationStatus.Ready;

    /// <summary>
    /// Holds the book for the first waiting member (oldest reservation, then lowest id) until
    /// <paramref name="expiresAt"/>, unless the book is on loan or already held. Saves the change.
    /// </summary>
    public static async Task<Reservation?> ServeNextIfOnShelfAsync(
        LibraryDbContext db, int bookId, DateTime readyAt, DateTime expiresAt, CancellationToken cancellationToken)
    {
        var onLoan = await db.Loans
            .AnyAsync(l => l.BookId == bookId && l.ReturnDate == null, cancellationToken)
            .ConfigureAwait(false);
        var held = await db.Reservations
            .AnyAsync(r => r.BookId == bookId && r.Status == ReservationStatus.Ready, cancellationToken)
            .ConfigureAwait(false);
        if (onLoan || held)
        {
            return null;
        }

        var next = await db.Reservations
            .Where(r => r.BookId == bookId && r.Status == ReservationStatus.Waiting)
            .OrderBy(r => r.CreatedAt)
            .ThenBy(r => r.Id)
            .FirstOrDefaultAsync(cancellationToken)
            .ConfigureAwait(false);
        if (next is null)
        {
            return null;
        }

        next.Status = ReservationStatus.Ready;
        next.ReadyAt = readyAt;
        next.ExpiresAt = expiresAt;
        await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return next;
    }
}

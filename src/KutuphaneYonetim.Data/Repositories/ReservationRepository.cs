using KutuphaneYonetim.Data.Persistence;
using KutuphaneYonetim.Domain.Entities;
using KutuphaneYonetim.Domain.Repositories;
using Microsoft.EntityFrameworkCore;

namespace KutuphaneYonetim.Data.Repositories;

internal sealed class ReservationRepository(IDbContextFactory<LibraryDbContext> contextFactory) : IReservationRepository
{
    public async Task<IReadOnlyList<ReservationDetail>> GetActiveDetailsAsync(CancellationToken cancellationToken = default)
    {
        await using var db = await contextFactory.CreateDbContextAsync(cancellationToken).ConfigureAwait(false);

        var query =
            from reservation in db.Reservations.AsNoTracking().Where(ReservationQueue.Active)
            join book in db.Books on reservation.BookId equals book.Id
            join member in db.Members on reservation.MemberId equals member.Id
            orderby reservation.BookId, reservation.Status descending, reservation.CreatedAt, reservation.Id
            select new ReservationDetail
            {
                Id = reservation.Id,
                BookId = reservation.BookId,
                BookTitle = book.Title,
                MemberId = reservation.MemberId,
                MemberName = member.FirstName + " " + member.LastName,
                CreatedAt = reservation.CreatedAt,
                Status = reservation.Status,
                ReadyAt = reservation.ReadyAt,
                ExpiresAt = reservation.ExpiresAt,
            };

        return await query.ToListAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task<Reservation?> GetHoldAsync(int bookId, CancellationToken cancellationToken = default)
    {
        await using var db = await contextFactory.CreateDbContextAsync(cancellationToken).ConfigureAwait(false);
        return await db.Reservations
            .AsNoTracking()
            .SingleOrDefaultAsync(r => r.BookId == bookId && r.Status == ReservationStatus.Ready, cancellationToken)
            .ConfigureAwait(false);
    }

    public async Task<bool> HasActiveAsync(int bookId, int memberId, CancellationToken cancellationToken = default)
    {
        await using var db = await contextFactory.CreateDbContextAsync(cancellationToken).ConfigureAwait(false);
        return await db.Reservations
            .Where(ReservationQueue.Active)
            .AnyAsync(r => r.BookId == bookId && r.MemberId == memberId, cancellationToken)
            .ConfigureAwait(false);
    }

    public async Task<bool> HasAnyForBookAsync(int bookId, CancellationToken cancellationToken = default)
    {
        await using var db = await contextFactory.CreateDbContextAsync(cancellationToken).ConfigureAwait(false);
        return await db.Reservations.AnyAsync(r => r.BookId == bookId, cancellationToken).ConfigureAwait(false);
    }

    public async Task<bool> HasAnyForMemberAsync(int memberId, CancellationToken cancellationToken = default)
    {
        await using var db = await contextFactory.CreateDbContextAsync(cancellationToken).ConfigureAwait(false);
        return await db.Reservations.AnyAsync(r => r.MemberId == memberId, cancellationToken).ConfigureAwait(false);
    }

    public async Task<int?> AddAsync(int bookId, int memberId, DateTime createdAt, CancellationToken cancellationToken = default)
    {
        await using var db = await contextFactory.CreateDbContextAsync(cancellationToken).ConfigureAwait(false);

        var reservation = new Reservation
        {
            BookId = bookId,
            MemberId = memberId,
            CreatedAt = createdAt,
            Status = ReservationStatus.Waiting,
        };
        db.Reservations.Add(reservation);

        try
        {
            await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
            return reservation.Id;
        }
        catch (DbUpdateException)
        {
            // The filtered unique index rejected a second active reservation of the same member.
            if (await HasActiveAsync(bookId, memberId, cancellationToken).ConfigureAwait(false))
            {
                return null;
            }

            throw;
        }
    }

    public async Task<bool> CancelAsync(
        int reservationId, DateTime now, DateTime nextExpiresAt, CancellationToken cancellationToken = default)
    {
        await using var db = await contextFactory.CreateDbContextAsync(cancellationToken).ConfigureAwait(false);
        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken).ConfigureAwait(false);

        var reservation = await db.Reservations
            .Where(ReservationQueue.Active)
            .SingleOrDefaultAsync(r => r.Id == reservationId, cancellationToken)
            .ConfigureAwait(false);
        if (reservation is null)
        {
            return false;
        }

        var wasHeld = reservation.Status == ReservationStatus.Ready;
        reservation.Status = ReservationStatus.Cancelled;
        reservation.ClosedAt = now;

        try
        {
            await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (DbUpdateConcurrencyException)
        {
            // Cancelled, fulfilled or expired by another session in the meantime.
            await transaction.RollbackAsync(cancellationToken).ConfigureAwait(false);
            return false;
        }

        if (wasHeld)
        {
            await ReservationQueue.ServeNextIfOnShelfAsync(db, reservation.BookId, now, nextExpiresAt, cancellationToken)
                .ConfigureAwait(false);
        }

        await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
        return true;
    }

    public async Task<int> ExpireHoldsAsync(DateTime now, DateTime nextExpiresAt, CancellationToken cancellationToken = default)
    {
        await using var db = await contextFactory.CreateDbContextAsync(cancellationToken).ConfigureAwait(false);
        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken).ConfigureAwait(false);

        var expired = await db.Reservations
            .Where(r => r.Status == ReservationStatus.Ready && r.ExpiresAt < now)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
        if (expired.Count == 0)
        {
            return 0;
        }

        foreach (var reservation in expired)
        {
            reservation.Status = ReservationStatus.Expired;
            reservation.ClosedAt = now;
        }

        try
        {
            // Saved before serving the next members so the "one hold per book" index never sees two holds.
            await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (DbUpdateConcurrencyException)
        {
            // Another session is expiring the same holds; it also serves the queue.
            await transaction.RollbackAsync(cancellationToken).ConfigureAwait(false);
            return 0;
        }

        foreach (var bookId in expired.Select(r => r.BookId).Distinct())
        {
            await ReservationQueue.ServeNextIfOnShelfAsync(db, bookId, now, nextExpiresAt, cancellationToken)
                .ConfigureAwait(false);
        }

        await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
        return expired.Count;
    }
}

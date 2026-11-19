using KutuphaneYonetim.Data.Persistence;
using KutuphaneYonetim.Domain.Entities;
using KutuphaneYonetim.Domain.Repositories;
using Microsoft.EntityFrameworkCore;

namespace KutuphaneYonetim.Data.Repositories;

internal sealed class LoanRepository(IDbContextFactory<LibraryDbContext> contextFactory) : ILoanRepository
{
    private const int MaxPaymentAttempts = 3;

    public async Task<IReadOnlyList<LoanDetail>> GetAllDetailsAsync(CancellationToken cancellationToken = default)
    {
        await using var db = await contextFactory.CreateDbContextAsync(cancellationToken).ConfigureAwait(false);

        var query =
            from loan in db.Loans.AsNoTracking()
            join book in db.Books on loan.BookId equals book.Id
            join member in db.Members on loan.MemberId equals member.Id
            orderby loan.Id
            select new LoanDetail
            {
                Id = loan.Id,
                BookId = loan.BookId,
                BookTitle = book.Title,
                MemberId = loan.MemberId,
                MemberName = member.FirstName + " " + member.LastName,
                LoanDate = loan.LoanDate,
                DueDate = loan.DueDate,
                ReturnDate = loan.ReturnDate,
                FineAmount = loan.FineAmount,
                FinePaidDate = loan.FinePaidDate,
            };

        return await query.ToListAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task<Loan?> GetActiveLoanForBookAsync(int bookId, CancellationToken cancellationToken = default)
    {
        await using var db = await contextFactory.CreateDbContextAsync(cancellationToken).ConfigureAwait(false);
        return await db.Loans
            .AsNoTracking()
            .SingleOrDefaultAsync(l => l.BookId == bookId && l.ReturnDate == null, cancellationToken)
            .ConfigureAwait(false);
    }

    public async Task<bool> HasActiveLoanForBookAsync(int bookId, CancellationToken cancellationToken = default)
    {
        await using var db = await contextFactory.CreateDbContextAsync(cancellationToken).ConfigureAwait(false);
        return await db.Loans
            .AnyAsync(l => l.BookId == bookId && l.ReturnDate == null, cancellationToken)
            .ConfigureAwait(false);
    }

    public async Task<bool> HasActiveLoanForMemberAsync(int memberId, CancellationToken cancellationToken = default)
    {
        await using var db = await contextFactory.CreateDbContextAsync(cancellationToken).ConfigureAwait(false);
        return await db.Loans
            .AnyAsync(l => l.MemberId == memberId && l.ReturnDate == null, cancellationToken)
            .ConfigureAwait(false);
    }

    public async Task<bool> HasAnyLoanForBookAsync(int bookId, CancellationToken cancellationToken = default)
    {
        await using var db = await contextFactory.CreateDbContextAsync(cancellationToken).ConfigureAwait(false);
        return await db.Loans.AnyAsync(l => l.BookId == bookId, cancellationToken).ConfigureAwait(false);
    }

    public async Task<bool> HasAnyLoanForMemberAsync(int memberId, CancellationToken cancellationToken = default)
    {
        await using var db = await contextFactory.CreateDbContextAsync(cancellationToken).ConfigureAwait(false);
        return await db.Loans.AnyAsync(l => l.MemberId == memberId, cancellationToken).ConfigureAwait(false);
    }

    public async Task<LendOutcome> LendAsync(
        int bookId, int memberId, DateTime loanDate, DateTime dueDate, CancellationToken cancellationToken = default)
    {
        await using var db = await contextFactory.CreateDbContextAsync(cancellationToken).ConfigureAwait(false);
        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken).ConfigureAwait(false);

        // Holds cannot expire, be cancelled or be served between the checks below and the commit.
        await BookLock.AcquireAsync(db, bookId, cancellationToken).ConfigureAwait(false);

        var book = await db.Books.SingleOrDefaultAsync(b => b.Id == bookId, cancellationToken).ConfigureAwait(false);
        if (book is null)
        {
            return LendOutcome.BookNotFound;
        }

        var hasActiveLoan = await db.Loans
            .AnyAsync(l => l.BookId == bookId && l.ReturnDate == null, cancellationToken)
            .ConfigureAwait(false);
        if (hasActiveLoan)
        {
            return LendOutcome.BookOnLoan;
        }

        // A book held for a reservation goes to that member only; their reservation is fulfilled.
        var hold = await db.Reservations
            .SingleOrDefaultAsync(r => r.BookId == bookId && r.Status == ReservationStatus.Ready, cancellationToken)
            .ConfigureAwait(false);
        if (hold is not null && hold.MemberId != memberId)
        {
            return LendOutcome.HeldForAnotherMember;
        }

        var ownReservations = await db.Reservations
            .Where(ReservationQueue.Active)
            .Where(r => r.BookId == bookId && r.MemberId == memberId)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
        foreach (var reservation in ownReservations)
        {
            reservation.Status = ReservationStatus.Fulfilled;
            reservation.ClosedAt = loanDate;
        }

        db.Loans.Add(new Loan { BookId = bookId, MemberId = memberId, LoanDate = loanDate, DueDate = dueDate.Date });
        book.IsAvailable = false;

        try
        {
            await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
            await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
            return LendOutcome.Lent;
        }
        catch (DbUpdateException)
        {
            // Another session lent the same book concurrently: the book or reservation row version changed
            // (DbUpdateConcurrencyException) or the filtered unique index rejected the insert.
            await transaction.RollbackAsync(cancellationToken).ConfigureAwait(false);
            if (await HasActiveLoanForBookAsync(bookId, cancellationToken).ConfigureAwait(false))
            {
                return LendOutcome.BookOnLoan;
            }

            throw;
        }
    }

    public async Task<ReturnOutcome> ReturnAsync(
        int loanId,
        DateTime returnDate,
        decimal? fineAmount,
        DateTime holdExpiresAt,
        CancellationToken cancellationToken = default)
    {
        await using var db = await contextFactory.CreateDbContextAsync(cancellationToken).ConfigureAwait(false);
        var bookId = await db.Loans
            .Where(l => l.Id == loanId)
            .Select(l => (int?)l.BookId)
            .SingleOrDefaultAsync(cancellationToken)
            .ConfigureAwait(false);
        if (bookId is null)
        {
            return ReturnOutcome.NotOpen;
        }

        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken).ConfigureAwait(false);
        await BookLock.AcquireAsync(db, bookId.Value, cancellationToken).ConfigureAwait(false);

        var loan = await db.Loans
            .SingleOrDefaultAsync(l => l.Id == loanId && l.ReturnDate == null, cancellationToken)
            .ConfigureAwait(false);
        if (loan is null)
        {
            return ReturnOutcome.NotOpen;
        }

        loan.ReturnDate = returnDate;
        loan.FineAmount = fineAmount > 0 ? fineAmount : null;

        var book = await db.Books.SingleOrDefaultAsync(b => b.Id == loan.BookId, cancellationToken).ConfigureAwait(false);
        if (book is not null)
        {
            book.IsAvailable = true;
        }

        try
        {
            await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (DbUpdateConcurrencyException)
        {
            // The same loan was returned by another session in the meantime.
            await transaction.RollbackAsync(cancellationToken).ConfigureAwait(false);
            return ReturnOutcome.NotOpen;
        }

        var served = await ReservationQueue
            .ServeNextIfOnShelfAsync(db, loan.BookId, returnDate, holdExpiresAt, cancellationToken)
            .ConfigureAwait(false);

        await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
        return new ReturnOutcome(true, served?.MemberId);
    }

    public async Task<decimal> GetUnpaidFineTotalAsync(int memberId, CancellationToken cancellationToken = default)
    {
        await using var db = await contextFactory.CreateDbContextAsync(cancellationToken).ConfigureAwait(false);
        var total = await db.Loans
            .Where(l => l.MemberId == memberId && l.FineAmount > 0 && l.FinePaidDate == null)
            .SumAsync(l => l.FineAmount, cancellationToken)
            .ConfigureAwait(false);
        return total ?? 0m;
    }

    public async Task<decimal> PayFinesAsync(int memberId, DateTime paidDate, CancellationToken cancellationToken = default)
    {
        for (var attempt = 1; ; attempt++)
        {
            await using var db = await contextFactory.CreateDbContextAsync(cancellationToken).ConfigureAwait(false);
            await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken).ConfigureAwait(false);

            var unpaid = await db.Loans
                .Where(l => l.MemberId == memberId && l.FineAmount > 0 && l.FinePaidDate == null)
                .ToListAsync(cancellationToken)
                .ConfigureAwait(false);
            if (unpaid.Count == 0)
            {
                return 0m;
            }

            foreach (var loan in unpaid)
            {
                loan.FinePaidDate = paidDate;
            }

            try
            {
                // Every row is updated only if its row version is unchanged, so the sum below is exactly
                // what this call closed; fines recorded after the read stay unpaid.
                await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
                await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
                return unpaid.Sum(l => l.FineAmount ?? 0m);
            }
            catch (DbUpdateConcurrencyException) when (attempt < MaxPaymentAttempts)
            {
                // Another session paid (some of) these fines in the meantime: read again.
                await transaction.RollbackAsync(cancellationToken).ConfigureAwait(false);
            }
        }
    }
}

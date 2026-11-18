using KutuphaneYonetim.Data.Persistence;
using KutuphaneYonetim.Domain.Entities;
using KutuphaneYonetim.Domain.Repositories;
using Microsoft.EntityFrameworkCore;

namespace KutuphaneYonetim.Data.Repositories;

internal sealed class LoanRepository(IDbContextFactory<LibraryDbContext> contextFactory) : ILoanRepository
{
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
                ReturnDate = loan.ReturnDate,
            };

        return await query.ToListAsync(cancellationToken).ConfigureAwait(false);
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

    public async Task<int?> LendAsync(int bookId, int memberId, DateTime loanDate, CancellationToken cancellationToken = default)
    {
        await using var db = await contextFactory.CreateDbContextAsync(cancellationToken).ConfigureAwait(false);
        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken).ConfigureAwait(false);

        var book = await db.Books.SingleOrDefaultAsync(b => b.Id == bookId, cancellationToken).ConfigureAwait(false);
        if (book is null)
        {
            return null;
        }

        var hasActiveLoan = await db.Loans
            .AnyAsync(l => l.BookId == bookId && l.ReturnDate == null, cancellationToken)
            .ConfigureAwait(false);
        if (hasActiveLoan)
        {
            return null;
        }

        var loan = new Loan { BookId = bookId, MemberId = memberId, LoanDate = loanDate };
        db.Loans.Add(loan);
        book.IsAvailable = false;

        try
        {
            await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
            await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
            return loan.Id;
        }
        catch (DbUpdateException)
        {
            // Another session lent the same book concurrently: the book row version changed
            // (DbUpdateConcurrencyException) or the filtered unique index rejected the insert.
            await transaction.RollbackAsync(cancellationToken).ConfigureAwait(false);
            if (await HasActiveLoanForBookAsync(bookId, cancellationToken).ConfigureAwait(false))
            {
                return null;
            }

            throw;
        }
    }

    public async Task<bool> ReturnAsync(int bookId, DateTime returnDate, CancellationToken cancellationToken = default)
    {
        await using var db = await contextFactory.CreateDbContextAsync(cancellationToken).ConfigureAwait(false);
        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken).ConfigureAwait(false);

        var loan = await db.Loans
            .SingleOrDefaultAsync(l => l.BookId == bookId && l.ReturnDate == null, cancellationToken)
            .ConfigureAwait(false);
        if (loan is null)
        {
            return false;
        }

        loan.ReturnDate = returnDate;

        var book = await db.Books.SingleOrDefaultAsync(b => b.Id == bookId, cancellationToken).ConfigureAwait(false);
        if (book is not null)
        {
            book.IsAvailable = true;
        }

        await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
        return true;
    }
}

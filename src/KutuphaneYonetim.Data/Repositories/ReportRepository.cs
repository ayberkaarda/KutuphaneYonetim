using KutuphaneYonetim.Data.Persistence;
using KutuphaneYonetim.Domain.Entities;
using KutuphaneYonetim.Domain.Repositories;
using Microsoft.EntityFrameworkCore;

namespace KutuphaneYonetim.Data.Repositories;

internal sealed class ReportRepository(IDbContextFactory<LibraryDbContext> contextFactory) : IReportRepository
{
    public async Task<IReadOnlyList<LoanDetail>> GetOverdueLoansAsync(DateTime today, CancellationToken cancellationToken = default)
    {
        await using var db = await contextFactory.CreateDbContextAsync(cancellationToken).ConfigureAwait(false);
        var day = today.Date;

        return await Details(db, db.Loans.Where(l => l.ReturnDate == null && l.DueDate < day))
            .OrderBy(l => l.DueDate)
            .ThenBy(l => l.Id)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
    }

    public async Task<IReadOnlyList<BookLoanCount>> GetMostBorrowedAsync(
        DateTime start, DateTime endExclusive, int top, CancellationToken cancellationToken = default)
    {
        await using var db = await contextFactory.CreateDbContextAsync(cancellationToken).ConfigureAwait(false);

        var counts =
            from loan in db.Loans.AsNoTracking()
            where loan.LoanDate >= start && loan.LoanDate < endExclusive
            group loan by loan.BookId into g
            select new { BookId = g.Key, LoanCount = g.Count() };

        var query =
            from count in counts
            join book in db.Books on count.BookId equals book.Id
            orderby count.LoanCount descending, book.Title, book.Id
            select new BookLoanCount
            {
                BookId = book.Id,
                Title = book.Title,
                Author = book.Author,
                LoanCount = count.LoanCount,
            };

        return await query.Take(top).ToListAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task<IReadOnlyList<LoanDetail>> GetUnpaidFinesAsync(CancellationToken cancellationToken = default)
    {
        await using var db = await contextFactory.CreateDbContextAsync(cancellationToken).ConfigureAwait(false);

        return await Details(db, db.Loans.Where(l => l.FineAmount > 0 && l.FinePaidDate == null))
            .OrderBy(l => l.MemberName)
            .ThenBy(l => l.ReturnDate)
            .ThenBy(l => l.Id)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
    }

    private static IQueryable<LoanDetail> Details(LibraryDbContext db, IQueryable<Loan> loans) =>
        from loan in loans.AsNoTracking()
        join book in db.Books on loan.BookId equals book.Id
        join member in db.Members on loan.MemberId equals member.Id
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
}

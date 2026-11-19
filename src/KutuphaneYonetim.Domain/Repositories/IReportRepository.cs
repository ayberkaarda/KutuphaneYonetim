using KutuphaneYonetim.Domain.Entities;

namespace KutuphaneYonetim.Domain.Repositories;

/// <summary>Read-only queries for the reports screen.</summary>
public interface IReportRepository
{
    /// <summary>Open loans whose due date lies before <paramref name="today"/>, the most overdue first.</summary>
    Task<IReadOnlyList<LoanDetail>> GetOverdueLoansAsync(DateTime today, CancellationToken cancellationToken = default);

    /// <summary>
    /// Books ordered by how often they were lent in [<paramref name="start"/>, <paramref name="endExclusive"/>),
    /// then by title; books that were not lent in the range are left out.
    /// </summary>
    Task<IReadOnlyList<BookLoanCount>> GetMostBorrowedAsync(
        DateTime start, DateTime endExclusive, int top, CancellationToken cancellationToken = default);

    /// <summary>Returned loans with a fine that is not paid yet, ordered by member and return date.</summary>
    Task<IReadOnlyList<LoanDetail>> GetUnpaidFinesAsync(CancellationToken cancellationToken = default);
}

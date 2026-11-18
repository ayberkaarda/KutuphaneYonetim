using KutuphaneYonetim.Domain.Entities;

namespace KutuphaneYonetim.Domain.Repositories;

public interface ILoanRepository
{
    Task<IReadOnlyList<LoanDetail>> GetAllDetailsAsync(CancellationToken cancellationToken = default);

    Task<bool> HasActiveLoanForBookAsync(int bookId, CancellationToken cancellationToken = default);

    Task<bool> HasActiveLoanForMemberAsync(int memberId, CancellationToken cancellationToken = default);

    Task<bool> HasAnyLoanForBookAsync(int bookId, CancellationToken cancellationToken = default);

    Task<bool> HasAnyLoanForMemberAsync(int memberId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Records a new loan and marks the book as unavailable in a single transaction.
    /// Returns null when the book already has an active loan.
    /// </summary>
    Task<int?> LendAsync(int bookId, int memberId, DateTime loanDate, CancellationToken cancellationToken = default);

    /// <summary>
    /// Closes the active loan of the book and marks it available in a single transaction.
    /// Returns false when the book has no active loan.
    /// </summary>
    Task<bool> ReturnAsync(int bookId, DateTime returnDate, CancellationToken cancellationToken = default);
}

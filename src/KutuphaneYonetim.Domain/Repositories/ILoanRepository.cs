using KutuphaneYonetim.Domain.Entities;

namespace KutuphaneYonetim.Domain.Repositories;

/// <summary>Result of <see cref="ILoanRepository.LendAsync"/>.</summary>
public enum LendOutcome
{
    Lent,
    BookNotFound,
    BookOnLoan,
}

public interface ILoanRepository
{
    Task<IReadOnlyList<LoanDetail>> GetAllDetailsAsync(CancellationToken cancellationToken = default);

    /// <summary>The open (not yet returned) loan of the book, or null.</summary>
    Task<Loan?> GetActiveLoanForBookAsync(int bookId, CancellationToken cancellationToken = default);

    Task<bool> HasActiveLoanForBookAsync(int bookId, CancellationToken cancellationToken = default);

    Task<bool> HasActiveLoanForMemberAsync(int memberId, CancellationToken cancellationToken = default);

    Task<bool> HasAnyLoanForBookAsync(int bookId, CancellationToken cancellationToken = default);

    Task<bool> HasAnyLoanForMemberAsync(int memberId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Records a new loan and marks the book as unavailable in a single transaction.
    /// The book is re-checked inside the transaction, so it can never have two open loans.
    /// </summary>
    Task<LendOutcome> LendAsync(
        int bookId, int memberId, DateTime loanDate, DateTime dueDate, CancellationToken cancellationToken = default);

    /// <summary>
    /// Closes the loan if it is still open, stores the fine (null for none) and marks the book available,
    /// in a single transaction. Returns false when the loan is not open (any more).
    /// </summary>
    Task<bool> ReturnAsync(
        int loanId, DateTime returnDate, decimal? fineAmount, CancellationToken cancellationToken = default);

    /// <summary>Sum of the member's fines that are not paid yet (0 when there are none).</summary>
    Task<decimal> GetUnpaidFineTotalAsync(int memberId, CancellationToken cancellationToken = default);

    /// <summary>Marks all unpaid fines of the member as paid. Returns the number of loans updated.</summary>
    Task<int> PayFinesAsync(int memberId, DateTime paidDate, CancellationToken cancellationToken = default);
}

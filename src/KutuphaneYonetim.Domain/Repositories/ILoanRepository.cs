using KutuphaneYonetim.Domain.Entities;

namespace KutuphaneYonetim.Domain.Repositories;

/// <summary>Result of <see cref="ILoanRepository.LendAsync"/>.</summary>
public enum LendOutcome
{
    Lent,
    BookNotFound,
    BookOnLoan,

    /// <summary>The book is on the shelf but held for another member's reservation.</summary>
    HeldForAnotherMember,
}

/// <summary>Result of <see cref="ILoanRepository.ReturnAsync"/>.</summary>
/// <param name="Returned">False when the loan was not open (any more).</param>
/// <param name="HeldForMemberId">The member the book is now held for, when someone was waiting for it.</param>
public sealed record ReturnOutcome(bool Returned, int? HeldForMemberId = null)
{
    public static ReturnOutcome NotOpen { get; } = new(false);
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
    /// The book is re-checked inside the transaction, so it can never have two open loans, and a book held
    /// for a reservation is only lent to that member (whose reservation is then fulfilled).
    /// </summary>
    Task<LendOutcome> LendAsync(
        int bookId, int memberId, DateTime loanDate, DateTime dueDate, CancellationToken cancellationToken = default);

    /// <summary>
    /// Closes the loan if it is still open, stores the fine (null for none) and marks the book available,
    /// in a single transaction. When members are waiting for the book, it is held for the first of them
    /// until <paramref name="holdExpiresAt"/>.
    /// </summary>
    Task<ReturnOutcome> ReturnAsync(
        int loanId,
        DateTime returnDate,
        decimal? fineAmount,
        DateTime holdExpiresAt,
        CancellationToken cancellationToken = default);

    /// <summary>Sum of the member's fines that are not paid yet (0 when there are none).</summary>
    Task<decimal> GetUnpaidFineTotalAsync(int memberId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Marks the unpaid fines of the member as paid in a single transaction and returns the sum of exactly
    /// the fines it closed: 0 when nothing was left to pay (for example because a concurrent payment closed
    /// them first). A fine recorded concurrently is either included in the sum or left unpaid.
    /// </summary>
    Task<decimal> PayFinesAsync(int memberId, DateTime paidDate, CancellationToken cancellationToken = default);
}

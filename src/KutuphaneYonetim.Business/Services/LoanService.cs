using FluentValidation;
using KutuphaneYonetim.Business.Validation;
using KutuphaneYonetim.Domain.Entities;
using KutuphaneYonetim.Domain.Repositories;

namespace KutuphaneYonetim.Business.Services;

/// <summary>Outcome of a return: how late the book was and the fine recorded for it.</summary>
public sealed record ReturnReceipt(int OverdueDays, decimal Fine);

public interface ILoanService
{
    /// <summary>All loans; open loans carry their current overdue days and estimated fine.</summary>
    Task<IReadOnlyList<LoanDetail>> GetAllAsync(CancellationToken cancellationToken = default);

    /// <summary>Today plus the configured loan period.</summary>
    DateTime GetDefaultDueDate();

    /// <summary>Lends the book until <paramref name="dueDate"/> (default: <see cref="GetDefaultDueDate"/>).</summary>
    Task LendAsync(int bookId, int memberId, DateTime? dueDate = null, CancellationToken cancellationToken = default);

    /// <summary>Takes the book back and records a fine when it is returned after its due date.</summary>
    Task<ReturnReceipt> ReturnAsync(int bookId, CancellationToken cancellationToken = default);
}

internal sealed class LoanService(
    IBookRepository books,
    IMemberRepository members,
    ILoanRepository loans,
    IValidator<LoanInput> validator,
    LibraryPolicy policy,
    TimeProvider timeProvider) : ILoanService
{
    private const string NoOpenLoan = "Bu kitap için açık bir ödünç kaydı bulunamadı.";

    public async Task<IReadOnlyList<LoanDetail>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        var all = await loans.GetAllDetailsAsync(cancellationToken).ConfigureAwait(false);
        var now = Now();
        foreach (var loan in all)
        {
            loan.OverdueDays = FineCalculator.OverdueDays(loan.DueDate, loan.ReturnDate ?? now);
            loan.EstimatedFine = loan.IsActive ? FineCalculator.Fine(loan.OverdueDays, policy.DailyFine) : null;
        }

        return all;
    }

    public DateTime GetDefaultDueDate() => Now().Date.AddDays(policy.LoanPeriodDays);

    public async Task LendAsync(int bookId, int memberId, DateTime? dueDate = null, CancellationToken cancellationToken = default)
    {
        var now = Now();
        var due = (dueDate ?? now.Date.AddDays(policy.LoanPeriodDays)).Date;
        validator.ValidateOrThrow(new LoanInput(bookId, memberId, now, due));

        _ = await books.GetByIdAsync(bookId, cancellationToken).ConfigureAwait(false)
            ?? throw new BusinessRuleException(Messages.BookNotFound);

        _ = await members.GetByIdAsync(memberId, cancellationToken).ConfigureAwait(false)
            ?? throw new BusinessRuleException(Messages.MemberNotFound);

        var unpaid = await loans.GetUnpaidFineTotalAsync(memberId, cancellationToken).ConfigureAwait(false);
        if (unpaid > policy.MaxUnpaidFine)
        {
            throw new BusinessRuleException(
                "Bu üyenin ödenmemiş cezası izin verilen sınırın üzerinde. Ceza ödenmeden yeni kitap ödünç verilemez.");
        }

        // The repository re-checks inside a transaction, so two members can never hold the same book.
        var outcome = await loans
            .LendAsync(bookId, memberId, now, due, cancellationToken)
            .ConfigureAwait(false);

        switch (outcome)
        {
            case LendOutcome.Lent:
                return;
            case LendOutcome.BookNotFound:
                throw new BusinessRuleException(Messages.BookNotFound);
            default:
                throw new BusinessRuleException("Bu kitap şu anda başka bir üyede. Aynı kitap iki üyeye ödünç verilemez.");
        }
    }

    public async Task<ReturnReceipt> ReturnAsync(int bookId, CancellationToken cancellationToken = default)
    {
        var loan = await loans.GetActiveLoanForBookAsync(bookId, cancellationToken).ConfigureAwait(false)
            ?? throw new BusinessRuleException(NoOpenLoan);

        var now = Now();
        var overdueDays = FineCalculator.OverdueDays(loan.DueDate, now);
        var fine = FineCalculator.Fine(overdueDays, policy.DailyFine);

        var returned = await loans
            .ReturnAsync(loan.Id, now, fine > 0 ? fine : null, cancellationToken)
            .ConfigureAwait(false);
        if (!returned)
        {
            throw new BusinessRuleException(NoOpenLoan);
        }

        return new ReturnReceipt(overdueDays, fine);
    }

    private DateTime Now() => timeProvider.GetLocalNow().DateTime;
}

using FluentValidation;
using KutuphaneYonetim.Business.Validation;
using KutuphaneYonetim.Domain.Entities;
using KutuphaneYonetim.Domain.Repositories;

namespace KutuphaneYonetim.Business.Services;

/// <summary>
/// Outcome of a return: how late the book was and the fine recorded for it, and, when members were
/// waiting for the book, who it is now held for and until when.
/// </summary>
public sealed record ReturnReceipt(int OverdueDays, decimal Fine, string? HeldForMemberName = null, DateTime? HeldUntil = null);

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
    IReservationRepository reservations,
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

        // A hold that was not picked up in time must not block the book any longer.
        await reservations.ExpireHoldsAsync(now, HoldEnd(now), cancellationToken).ConfigureAwait(false);

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
            case LendOutcome.HeldForAnotherMember:
                throw new BusinessRuleException(
                    "Bu kitap rezervasyon sırasındaki başka bir üye için ayrıldı. Yalnızca o üyeye ödünç verilebilir.");
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

        var holdEnd = HoldEnd(now);
        var outcome = await loans
            .ReturnAsync(loan.Id, now, fine > 0 ? fine : null, holdEnd, cancellationToken)
            .ConfigureAwait(false);
        if (!outcome.Returned)
        {
            throw new BusinessRuleException(NoOpenLoan);
        }

        if (outcome.HeldForMemberId is not int heldFor)
        {
            return new ReturnReceipt(overdueDays, fine);
        }

        var member = await members.GetByIdAsync(heldFor, cancellationToken).ConfigureAwait(false);
        return new ReturnReceipt(overdueDays, fine, member?.FullName ?? string.Empty, holdEnd);
    }

    private DateTime HoldEnd(DateTime from) => from.AddDays(policy.ReservationHoldDays);

    private DateTime Now() => timeProvider.GetLocalNow().DateTime;
}

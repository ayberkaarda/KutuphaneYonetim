using FluentValidation;
using KutuphaneYonetim.Business.Validation;
using KutuphaneYonetim.Domain.Entities;
using KutuphaneYonetim.Domain.Repositories;

namespace KutuphaneYonetim.Business.Services;

public interface ILoanService
{
    Task<IReadOnlyList<LoanDetail>> GetAllAsync(CancellationToken cancellationToken = default);

    Task LendAsync(int bookId, int memberId, CancellationToken cancellationToken = default);

    Task ReturnAsync(int bookId, CancellationToken cancellationToken = default);
}

internal sealed class LoanService(
    IBookRepository books,
    IMemberRepository members,
    ILoanRepository loans,
    IValidator<LoanInput> validator,
    TimeProvider timeProvider) : ILoanService
{
    public Task<IReadOnlyList<LoanDetail>> GetAllAsync(CancellationToken cancellationToken = default) =>
        loans.GetAllDetailsAsync(cancellationToken);

    public async Task LendAsync(int bookId, int memberId, CancellationToken cancellationToken = default)
    {
        validator.ValidateOrThrow(new LoanInput(bookId, memberId));

        _ = await books.GetByIdAsync(bookId, cancellationToken).ConfigureAwait(false)
            ?? throw new BusinessRuleException(Messages.BookNotFound);

        _ = await members.GetByIdAsync(memberId, cancellationToken).ConfigureAwait(false)
            ?? throw new BusinessRuleException(Messages.MemberNotFound);

        // The repository re-checks inside a transaction, so two members can never hold the same book.
        var loanId = await loans
            .LendAsync(bookId, memberId, Now(), cancellationToken)
            .ConfigureAwait(false);

        if (loanId is null)
        {
            throw new BusinessRuleException("Bu kitap şu anda başka bir üyede. Aynı kitap iki üyeye ödünç verilemez.");
        }
    }

    public async Task ReturnAsync(int bookId, CancellationToken cancellationToken = default)
    {
        var returned = await loans.ReturnAsync(bookId, Now(), cancellationToken).ConfigureAwait(false);
        if (!returned)
        {
            throw new BusinessRuleException("Bu kitap için açık bir ödünç kaydı bulunamadı.");
        }
    }

    private DateTime Now() => timeProvider.GetLocalNow().DateTime;
}

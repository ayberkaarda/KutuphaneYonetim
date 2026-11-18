using FluentValidation;
using KutuphaneYonetim.Business.Validation;
using KutuphaneYonetim.Domain.Entities;
using KutuphaneYonetim.Domain.Repositories;

namespace KutuphaneYonetim.Business.Services;

public interface IMemberService
{
    Task<IReadOnlyList<Member>> GetAllAsync(CancellationToken cancellationToken = default);

    Task<Member> AddAsync(string firstName, string lastName, CancellationToken cancellationToken = default);

    Task DeleteAsync(int memberId, CancellationToken cancellationToken = default);
}

internal sealed class MemberService(
    IMemberRepository members,
    ILoanRepository loans,
    IValidator<MemberInput> validator) : IMemberService
{
    public Task<IReadOnlyList<Member>> GetAllAsync(CancellationToken cancellationToken = default) =>
        members.GetAllAsync(cancellationToken);

    public async Task<Member> AddAsync(string firstName, string lastName, CancellationToken cancellationToken = default)
    {
        var input = MemberInput.Create(firstName, lastName);
        validator.ValidateOrThrow(input);

        var member = new Member { FirstName = input.FirstName, LastName = input.LastName };

        member.Id = await members.AddAsync(member, cancellationToken).ConfigureAwait(false);
        return member;
    }

    public async Task DeleteAsync(int memberId, CancellationToken cancellationToken = default)
    {
        _ = await members.GetByIdAsync(memberId, cancellationToken).ConfigureAwait(false)
            ?? throw new BusinessRuleException(Messages.MemberNotFound);

        if (await loans.HasActiveLoanForMemberAsync(memberId, cancellationToken).ConfigureAwait(false))
        {
            throw new BusinessRuleException("Bu üyenin iade etmediği kitap bulunduğu için silinemez.");
        }

        if (await loans.HasAnyLoanForMemberAsync(memberId, cancellationToken).ConfigureAwait(false))
        {
            throw new BusinessRuleException("Bu üyenin ödünç geçmişi bulunduğu için silinemez.");
        }

        await members.DeleteAsync(memberId, cancellationToken).ConfigureAwait(false);
    }
}

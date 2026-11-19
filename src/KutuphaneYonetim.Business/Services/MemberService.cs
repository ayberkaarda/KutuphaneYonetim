using FluentValidation;
using KutuphaneYonetim.Business.Validation;
using KutuphaneYonetim.Domain;
using KutuphaneYonetim.Domain.Entities;
using KutuphaneYonetim.Domain.Repositories;

namespace KutuphaneYonetim.Business.Services;

public interface IMemberService
{
    Task<IReadOnlyList<Member>> GetAllAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Searches first and last names with a filter (unpaid fines, open loans), one page at a time. Each row
    /// carries the open loan count and unpaid fine total of the member.
    /// </summary>
    Task<PagedResult<MemberSummary>> SearchAsync(MemberQuery query, CancellationToken cancellationToken = default);

    Task<Member> AddAsync(string firstName, string lastName, CancellationToken cancellationToken = default);

    Task DeleteAsync(int memberId, CancellationToken cancellationToken = default);

    /// <summary>Marks every unpaid fine of the member as paid and returns the amount that was paid.</summary>
    Task<decimal> PayFinesAsync(int memberId, CancellationToken cancellationToken = default);
}

internal sealed class MemberService(
    IMemberRepository members,
    ILoanRepository loans,
    IValidator<MemberInput> validator,
    TimeProvider timeProvider) : IMemberService
{
    public Task<IReadOnlyList<Member>> GetAllAsync(CancellationToken cancellationToken = default) =>
        members.GetAllAsync(cancellationToken);

    public Task<PagedResult<MemberSummary>> SearchAsync(MemberQuery query, CancellationToken cancellationToken = default)
    {
        var normalized = SearchPaging.Normalize(query);
        return SearchPaging.SearchAsync(
            normalized,
            normalized.Page,
            q => members.SearchAsync(q, cancellationToken),
            (q, page) => q with { Page = page });
    }

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

    public async Task<decimal> PayFinesAsync(int memberId, CancellationToken cancellationToken = default)
    {
        _ = await members.GetByIdAsync(memberId, cancellationToken).ConfigureAwait(false)
            ?? throw new BusinessRuleException(Messages.MemberNotFound);

        var unpaid = await loans.GetUnpaidFineTotalAsync(memberId, cancellationToken).ConfigureAwait(false);
        if (unpaid <= 0)
        {
            throw new BusinessRuleException("Bu üyenin ödenmemiş cezası bulunmuyor.");
        }

        await loans.PayFinesAsync(memberId, timeProvider.GetLocalNow().DateTime, cancellationToken).ConfigureAwait(false);
        return unpaid;
    }
}

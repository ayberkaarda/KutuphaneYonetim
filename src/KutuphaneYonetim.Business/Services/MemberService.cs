using KutuphaneYonetim.Domain.Entities;
using KutuphaneYonetim.Domain.Repositories;

namespace KutuphaneYonetim.Business.Services;

public interface IMemberService
{
    Task<IReadOnlyList<Member>> GetAllAsync(CancellationToken cancellationToken = default);

    Task<Member> AddAsync(string firstName, string lastName, CancellationToken cancellationToken = default);

    Task DeleteAsync(int memberId, CancellationToken cancellationToken = default);
}

internal sealed class MemberService(IMemberRepository members, ILoanRepository loans) : IMemberService
{
    internal const int MaxNameLength = 50;

    public Task<IReadOnlyList<Member>> GetAllAsync(CancellationToken cancellationToken = default) =>
        members.GetAllAsync(cancellationToken);

    public async Task<Member> AddAsync(string firstName, string lastName, CancellationToken cancellationToken = default)
    {
        var member = new Member
        {
            FirstName = Validation.Required(firstName, "Ad", MaxNameLength),
            LastName = Validation.Required(lastName, "Soyad", MaxNameLength),
        };

        member.Id = await members.AddAsync(member, cancellationToken).ConfigureAwait(false);
        return member;
    }

    public async Task DeleteAsync(int memberId, CancellationToken cancellationToken = default)
    {
        _ = await members.GetByIdAsync(memberId, cancellationToken).ConfigureAwait(false)
            ?? throw new BusinessRuleException("Seçilen üye bulunamadı.");

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

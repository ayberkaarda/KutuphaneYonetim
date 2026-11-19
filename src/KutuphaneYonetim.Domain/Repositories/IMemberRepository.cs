using KutuphaneYonetim.Domain.Entities;

namespace KutuphaneYonetim.Domain.Repositories;

public interface IMemberRepository
{
    Task<IReadOnlyList<Member>> GetAllAsync(CancellationToken cancellationToken = default);

    /// <summary>All members with their open loan count and unpaid fine total, ordered by id.</summary>
    Task<IReadOnlyList<MemberSummary>> GetSummariesAsync(CancellationToken cancellationToken = default);

    Task<Member?> GetByIdAsync(int id, CancellationToken cancellationToken = default);

    Task<int> AddAsync(Member member, CancellationToken cancellationToken = default);

    Task<bool> DeleteAsync(int id, CancellationToken cancellationToken = default);
}

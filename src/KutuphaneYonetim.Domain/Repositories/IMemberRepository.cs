using KutuphaneYonetim.Domain.Entities;

namespace KutuphaneYonetim.Domain.Repositories;

public interface IMemberRepository
{
    Task<IReadOnlyList<Member>> GetAllAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Members matching the query with their open loan count and unpaid fine total, ordered by id. The
    /// text is matched as a substring of the first name, the last name or "first last".
    /// </summary>
    Task<PagedResult<MemberSummary>> SearchAsync(MemberQuery query, CancellationToken cancellationToken = default);

    Task<Member?> GetByIdAsync(int id, CancellationToken cancellationToken = default);

    Task<int> AddAsync(Member member, CancellationToken cancellationToken = default);

    Task<bool> DeleteAsync(int id, CancellationToken cancellationToken = default);
}

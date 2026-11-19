using KutuphaneYonetim.Domain.Entities;

namespace KutuphaneYonetim.Domain.Repositories;

public interface IBookRepository
{
    Task<IReadOnlyList<Book>> GetAllAsync(CancellationToken cancellationToken = default);

    Task<IReadOnlyList<Book>> GetAvailableAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Books matching the query, ordered by id. The text is matched as a substring of the title, the
    /// author or the ISBN; the query is expected to be normalised by the caller (trimmed, valid page).
    /// </summary>
    Task<PagedResult<Book>> SearchAsync(BookQuery query, CancellationToken cancellationToken = default);

    Task<Book?> GetByIdAsync(int id, CancellationToken cancellationToken = default);

    Task<int> AddAsync(Book book, CancellationToken cancellationToken = default);

    Task<bool> DeleteAsync(int id, CancellationToken cancellationToken = default);
}

using FluentValidation;
using KutuphaneYonetim.Business.Validation;
using KutuphaneYonetim.Domain;
using KutuphaneYonetim.Domain.Entities;
using KutuphaneYonetim.Domain.Repositories;

namespace KutuphaneYonetim.Business.Services;

public interface IBookService
{
    Task<IReadOnlyList<Book>> GetAllAsync(CancellationToken cancellationToken = default);

    Task<IReadOnlyList<Book>> GetAvailableAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Searches title, author and ISBN (case-insensitive on SQL Server) with an availability filter, one
    /// page at a time. The page is moved back to the last page when it lies beyond the result.
    /// </summary>
    Task<PagedResult<Book>> SearchAsync(BookQuery query, CancellationToken cancellationToken = default);

    Task<Book> AddAsync(string title, string author, string? isbn = null, CancellationToken cancellationToken = default);

    Task DeleteAsync(int bookId, CancellationToken cancellationToken = default);
}

internal sealed class BookService(
    IBookRepository books,
    ILoanRepository loans,
    IValidator<BookInput> validator) : IBookService
{
    public Task<IReadOnlyList<Book>> GetAllAsync(CancellationToken cancellationToken = default) =>
        books.GetAllAsync(cancellationToken);

    public Task<IReadOnlyList<Book>> GetAvailableAsync(CancellationToken cancellationToken = default) =>
        books.GetAvailableAsync(cancellationToken);

    public Task<PagedResult<Book>> SearchAsync(BookQuery query, CancellationToken cancellationToken = default)
    {
        var normalized = SearchPaging.Normalize(query);
        return SearchPaging.SearchAsync(
            normalized,
            normalized.Page,
            q => books.SearchAsync(q, cancellationToken),
            (q, page) => q with { Page = page });
    }

    public async Task<Book> AddAsync(string title, string author, string? isbn = null, CancellationToken cancellationToken = default)
    {
        var input = BookInput.Create(title, author, isbn);
        validator.ValidateOrThrow(input);

        var book = new Book { Title = input.Title, Author = input.Author, Isbn = input.Isbn, IsAvailable = true };

        book.Id = await books.AddAsync(book, cancellationToken).ConfigureAwait(false);
        return book;
    }

    public async Task DeleteAsync(int bookId, CancellationToken cancellationToken = default)
    {
        _ = await books.GetByIdAsync(bookId, cancellationToken).ConfigureAwait(false)
            ?? throw new BusinessRuleException(Messages.BookNotFound);

        if (await loans.HasActiveLoanForBookAsync(bookId, cancellationToken).ConfigureAwait(false))
        {
            throw new BusinessRuleException("Bu kitap şu anda ödünçte olduğu için silinemez. Önce iade alınmalıdır.");
        }

        if (await loans.HasAnyLoanForBookAsync(bookId, cancellationToken).ConfigureAwait(false))
        {
            throw new BusinessRuleException("Bu kitabın ödünç geçmişi bulunduğu için silinemez.");
        }

        await books.DeleteAsync(bookId, cancellationToken).ConfigureAwait(false);
    }
}

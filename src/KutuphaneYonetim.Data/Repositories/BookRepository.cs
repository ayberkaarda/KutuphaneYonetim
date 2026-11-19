using KutuphaneYonetim.Data.Persistence;
using KutuphaneYonetim.Domain;
using KutuphaneYonetim.Domain.Entities;
using KutuphaneYonetim.Domain.Repositories;
using Microsoft.EntityFrameworkCore;

namespace KutuphaneYonetim.Data.Repositories;

internal sealed class BookRepository(IDbContextFactory<LibraryDbContext> contextFactory) : IBookRepository
{
    public async Task<IReadOnlyList<Book>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        await using var db = await contextFactory.CreateDbContextAsync(cancellationToken).ConfigureAwait(false);
        return await db.Books
            .AsNoTracking()
            .OrderBy(b => b.Id)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
    }

    public async Task<IReadOnlyList<Book>> GetAvailableAsync(CancellationToken cancellationToken = default)
    {
        await using var db = await contextFactory.CreateDbContextAsync(cancellationToken).ConfigureAwait(false);
        return await db.Books
            .AsNoTracking()
            .Where(b => b.IsAvailable)
            .OrderBy(b => b.Title)
            .ThenBy(b => b.Id)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
    }

    public async Task<PagedResult<Book>> SearchAsync(BookQuery query, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(query);

        await using var db = await contextFactory.CreateDbContextAsync(cancellationToken).ConfigureAwait(false);
        var books = db.Books.AsNoTracking();

        if (!string.IsNullOrEmpty(query.Text))
        {
            var pattern = SqlLike.Contains(query.Text);
            books = books.Where(b =>
                EF.Functions.Like(b.Title, pattern, SqlLike.Escape)
                || EF.Functions.Like(b.Author, pattern, SqlLike.Escape)
                || (b.Isbn != null && EF.Functions.Like(b.Isbn, pattern, SqlLike.Escape)));
        }

        books = query.Availability switch
        {
            BookAvailability.Available => books.Where(b => b.IsAvailable
                && !db.Reservations.Any(r => r.BookId == b.Id && r.Status == ReservationStatus.Ready)),
            BookAvailability.OnLoan => books.Where(b => !b.IsAvailable),
            BookAvailability.Reserved => books.Where(b => b.IsAvailable
                && db.Reservations.Any(r => r.BookId == b.Id && r.Status == ReservationStatus.Ready)),
            _ => books,
        };

        var total = await books.CountAsync(cancellationToken).ConfigureAwait(false);
        var items = await books
            .OrderBy(b => b.Id)
            .Skip(SqlLike.Skip(query.Page, query.PageSize))
            .Take(query.PageSize)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        return new PagedResult<Book>(items, total, query.Page, query.PageSize);
    }

    public async Task<Book?> GetByIdAsync(int id, CancellationToken cancellationToken = default)
    {
        await using var db = await contextFactory.CreateDbContextAsync(cancellationToken).ConfigureAwait(false);
        return await db.Books
            .AsNoTracking()
            .SingleOrDefaultAsync(b => b.Id == id, cancellationToken)
            .ConfigureAwait(false);
    }

    public async Task<int> AddAsync(Book book, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(book);

        await using var db = await contextFactory.CreateDbContextAsync(cancellationToken).ConfigureAwait(false);
        var entity = new Book { Title = book.Title, Author = book.Author, Isbn = book.Isbn, IsAvailable = book.IsAvailable };
        db.Books.Add(entity);
        await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return entity.Id;
    }

    public async Task<bool> DeleteAsync(int id, CancellationToken cancellationToken = default)
    {
        await using var db = await contextFactory.CreateDbContextAsync(cancellationToken).ConfigureAwait(false);
        var affected = await db.Books
            .Where(b => b.Id == id)
            .ExecuteDeleteAsync(cancellationToken)
            .ConfigureAwait(false);
        return affected > 0;
    }
}

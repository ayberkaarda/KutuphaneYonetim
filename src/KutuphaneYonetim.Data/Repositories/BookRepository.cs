using Dapper;
using KutuphaneYonetim.Domain.Entities;
using KutuphaneYonetim.Domain.Repositories;

namespace KutuphaneYonetim.Data.Repositories;

internal sealed class BookRepository(ISqlConnectionFactory connectionFactory) : IBookRepository
{
    public async Task<IReadOnlyList<Book>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        const string sql = """
            SELECT kitap_id AS Id, ad AS Title, yazar AS Author, CAST(ISNULL(durum, 1) AS bit) AS IsAvailable
            FROM dbo.Kitaplar
            ORDER BY kitap_id;
            """;

        await using var connection = await connectionFactory.OpenAsync(cancellationToken).ConfigureAwait(false);
        var books = await connection
            .QueryAsync<Book>(new CommandDefinition(sql, cancellationToken: cancellationToken))
            .ConfigureAwait(false);
        return books.AsList();
    }

    public async Task<IReadOnlyList<Book>> GetAvailableAsync(CancellationToken cancellationToken = default)
    {
        const string sql = """
            SELECT kitap_id AS Id, ad AS Title, yazar AS Author, CAST(ISNULL(durum, 1) AS bit) AS IsAvailable
            FROM dbo.Kitaplar
            WHERE ISNULL(durum, 1) = 1
            ORDER BY ad, kitap_id;
            """;

        await using var connection = await connectionFactory.OpenAsync(cancellationToken).ConfigureAwait(false);
        var books = await connection
            .QueryAsync<Book>(new CommandDefinition(sql, cancellationToken: cancellationToken))
            .ConfigureAwait(false);
        return books.AsList();
    }

    public async Task<Book?> GetByIdAsync(int id, CancellationToken cancellationToken = default)
    {
        const string sql = """
            SELECT kitap_id AS Id, ad AS Title, yazar AS Author, CAST(ISNULL(durum, 1) AS bit) AS IsAvailable
            FROM dbo.Kitaplar
            WHERE kitap_id = @Id;
            """;

        await using var connection = await connectionFactory.OpenAsync(cancellationToken).ConfigureAwait(false);
        return await connection
            .QuerySingleOrDefaultAsync<Book>(new CommandDefinition(sql, new { Id = id }, cancellationToken: cancellationToken))
            .ConfigureAwait(false);
    }

    public async Task<int> AddAsync(Book book, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(book);

        const string sql = """
            INSERT INTO dbo.Kitaplar (ad, yazar, durum)
            OUTPUT INSERTED.kitap_id
            VALUES (@Title, @Author, @IsAvailable);
            """;

        await using var connection = await connectionFactory.OpenAsync(cancellationToken).ConfigureAwait(false);
        var parameters = new { book.Title, book.Author, book.IsAvailable };
        return await connection
            .ExecuteScalarAsync<int>(new CommandDefinition(sql, parameters, cancellationToken: cancellationToken))
            .ConfigureAwait(false);
    }

    public async Task<bool> DeleteAsync(int id, CancellationToken cancellationToken = default)
    {
        const string sql = "DELETE FROM dbo.Kitaplar WHERE kitap_id = @Id;";

        await using var connection = await connectionFactory.OpenAsync(cancellationToken).ConfigureAwait(false);
        var affected = await connection
            .ExecuteAsync(new CommandDefinition(sql, new { Id = id }, cancellationToken: cancellationToken))
            .ConfigureAwait(false);
        return affected > 0;
    }
}

using System.Data;
using Dapper;
using KutuphaneYonetim.Domain.Entities;
using KutuphaneYonetim.Domain.Repositories;
using Microsoft.Data.SqlClient;

namespace KutuphaneYonetim.Data.Repositories;

internal sealed class LoanRepository(ISqlConnectionFactory connectionFactory) : ILoanRepository
{
    private const int DuplicateKeyError = 2627;
    private const int UniqueIndexViolation = 2601;

    public async Task<IReadOnlyList<LoanDetail>> GetAllDetailsAsync(CancellationToken cancellationToken = default)
    {
        const string sql = """
            SELECT o.odunc_id AS Id,
                   o.kitap_id AS BookId,
                   ISNULL(k.ad, N'') AS BookTitle,
                   o.uye_id AS MemberId,
                   LTRIM(RTRIM(CONCAT(u.ad, N' ', u.soyad))) AS MemberName,
                   o.odunc_tarihi AS LoanDate,
                   o.iade_tarihi AS ReturnDate
            FROM dbo.OduncIslemleri AS o
            LEFT JOIN dbo.Kitaplar AS k ON k.kitap_id = o.kitap_id
            LEFT JOIN dbo.Uyeler AS u ON u.uye_id = o.uye_id
            ORDER BY o.odunc_id;
            """;

        await using var connection = await connectionFactory.OpenAsync(cancellationToken).ConfigureAwait(false);
        var loans = await connection
            .QueryAsync<LoanDetail>(new CommandDefinition(sql, cancellationToken: cancellationToken))
            .ConfigureAwait(false);
        return loans.AsList();
    }

    public Task<bool> HasActiveLoanForBookAsync(int bookId, CancellationToken cancellationToken = default)
    {
        const string sql = """
            SELECT CASE WHEN EXISTS (
                SELECT 1 FROM dbo.OduncIslemleri WHERE kitap_id = @Id AND iade_tarihi IS NULL
            ) THEN 1 ELSE 0 END;
            """;
        return ExistsAsync(sql, bookId, cancellationToken);
    }

    public Task<bool> HasActiveLoanForMemberAsync(int memberId, CancellationToken cancellationToken = default)
    {
        const string sql = """
            SELECT CASE WHEN EXISTS (
                SELECT 1 FROM dbo.OduncIslemleri WHERE uye_id = @Id AND iade_tarihi IS NULL
            ) THEN 1 ELSE 0 END;
            """;
        return ExistsAsync(sql, memberId, cancellationToken);
    }

    public Task<bool> HasAnyLoanForBookAsync(int bookId, CancellationToken cancellationToken = default)
    {
        const string sql = """
            SELECT CASE WHEN EXISTS (
                SELECT 1 FROM dbo.OduncIslemleri WHERE kitap_id = @Id
            ) THEN 1 ELSE 0 END;
            """;
        return ExistsAsync(sql, bookId, cancellationToken);
    }

    public Task<bool> HasAnyLoanForMemberAsync(int memberId, CancellationToken cancellationToken = default)
    {
        const string sql = """
            SELECT CASE WHEN EXISTS (
                SELECT 1 FROM dbo.OduncIslemleri WHERE uye_id = @Id
            ) THEN 1 ELSE 0 END;
            """;
        return ExistsAsync(sql, memberId, cancellationToken);
    }

    public async Task<int?> LendAsync(int bookId, int memberId, DateTime loanDate, CancellationToken cancellationToken = default)
    {
        // UPDLOCK + HOLDLOCK serialises concurrent attempts to lend the same book;
        // the filtered unique index UX_OduncIslemleri_active_kitap is the final guard.
        const string sql = """
            IF EXISTS (SELECT 1 FROM dbo.OduncIslemleri WITH (UPDLOCK, HOLDLOCK)
                       WHERE kitap_id = @BookId AND iade_tarihi IS NULL)
            BEGIN
                SELECT CAST(NULL AS int);
                RETURN;
            END

            INSERT INTO dbo.OduncIslemleri (kitap_id, uye_id, odunc_tarihi)
            VALUES (@BookId, @MemberId, @LoanDate);

            DECLARE @LoanId int = CAST(SCOPE_IDENTITY() AS int);

            UPDATE dbo.Kitaplar SET durum = 0 WHERE kitap_id = @BookId;

            SELECT @LoanId;
            """;

        await using var connection = await connectionFactory.OpenAsync(cancellationToken).ConfigureAwait(false);
        await using var transaction = await connection
            .BeginTransactionAsync(IsolationLevel.ReadCommitted, cancellationToken)
            .ConfigureAwait(false);

        var parameters = new DynamicParameters();
        parameters.Add("BookId", bookId, DbType.Int32);
        parameters.Add("MemberId", memberId, DbType.Int32);
        parameters.Add("LoanDate", loanDate, DbType.DateTime);

        try
        {
            var loanId = await connection
                .ExecuteScalarAsync<int?>(new CommandDefinition(sql, parameters, transaction, cancellationToken: cancellationToken))
                .ConfigureAwait(false);

            await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
            return loanId;
        }
        catch (SqlException ex) when (ex.Number is DuplicateKeyError or UniqueIndexViolation)
        {
            // Another session lent the same book between our check and insert.
            return null;
        }
    }

    public async Task<bool> ReturnAsync(int bookId, DateTime returnDate, CancellationToken cancellationToken = default)
    {
        const string sql = """
            UPDATE dbo.OduncIslemleri
            SET iade_tarihi = @ReturnDate
            WHERE kitap_id = @BookId AND iade_tarihi IS NULL;

            DECLARE @Closed int = @@ROWCOUNT;

            IF @Closed > 0
                UPDATE dbo.Kitaplar SET durum = 1 WHERE kitap_id = @BookId;

            SELECT @Closed;
            """;

        await using var connection = await connectionFactory.OpenAsync(cancellationToken).ConfigureAwait(false);
        await using var transaction = await connection
            .BeginTransactionAsync(IsolationLevel.ReadCommitted, cancellationToken)
            .ConfigureAwait(false);

        var parameters = new DynamicParameters();
        parameters.Add("BookId", bookId, DbType.Int32);
        parameters.Add("ReturnDate", returnDate, DbType.DateTime);

        var closed = await connection
            .ExecuteScalarAsync<int>(new CommandDefinition(sql, parameters, transaction, cancellationToken: cancellationToken))
            .ConfigureAwait(false);

        await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
        return closed > 0;
    }

    private async Task<bool> ExistsAsync(string sql, int id, CancellationToken cancellationToken)
    {
        await using var connection = await connectionFactory.OpenAsync(cancellationToken).ConfigureAwait(false);
        var result = await connection
            .ExecuteScalarAsync<int>(new CommandDefinition(sql, new { Id = id }, cancellationToken: cancellationToken))
            .ConfigureAwait(false);
        return result == 1;
    }
}

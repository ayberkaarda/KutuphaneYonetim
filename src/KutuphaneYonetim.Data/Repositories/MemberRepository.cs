using Dapper;
using KutuphaneYonetim.Domain.Entities;
using KutuphaneYonetim.Domain.Repositories;

namespace KutuphaneYonetim.Data.Repositories;

internal sealed class MemberRepository(ISqlConnectionFactory connectionFactory) : IMemberRepository
{
    public async Task<IReadOnlyList<Member>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        const string sql = """
            SELECT uye_id AS Id, ad AS FirstName, soyad AS LastName
            FROM dbo.Uyeler
            ORDER BY uye_id;
            """;

        await using var connection = await connectionFactory.OpenAsync(cancellationToken).ConfigureAwait(false);
        var members = await connection
            .QueryAsync<Member>(new CommandDefinition(sql, cancellationToken: cancellationToken))
            .ConfigureAwait(false);
        return members.AsList();
    }

    public async Task<Member?> GetByIdAsync(int id, CancellationToken cancellationToken = default)
    {
        const string sql = """
            SELECT uye_id AS Id, ad AS FirstName, soyad AS LastName
            FROM dbo.Uyeler
            WHERE uye_id = @Id;
            """;

        await using var connection = await connectionFactory.OpenAsync(cancellationToken).ConfigureAwait(false);
        return await connection
            .QuerySingleOrDefaultAsync<Member>(new CommandDefinition(sql, new { Id = id }, cancellationToken: cancellationToken))
            .ConfigureAwait(false);
    }

    public async Task<int> AddAsync(Member member, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(member);

        const string sql = """
            INSERT INTO dbo.Uyeler (ad, soyad)
            OUTPUT INSERTED.uye_id
            VALUES (@FirstName, @LastName);
            """;

        await using var connection = await connectionFactory.OpenAsync(cancellationToken).ConfigureAwait(false);
        var parameters = new { member.FirstName, member.LastName };
        return await connection
            .ExecuteScalarAsync<int>(new CommandDefinition(sql, parameters, cancellationToken: cancellationToken))
            .ConfigureAwait(false);
    }

    public async Task<bool> DeleteAsync(int id, CancellationToken cancellationToken = default)
    {
        const string sql = "DELETE FROM dbo.Uyeler WHERE uye_id = @Id;";

        await using var connection = await connectionFactory.OpenAsync(cancellationToken).ConfigureAwait(false);
        var affected = await connection
            .ExecuteAsync(new CommandDefinition(sql, new { Id = id }, cancellationToken: cancellationToken))
            .ConfigureAwait(false);
        return affected > 0;
    }
}

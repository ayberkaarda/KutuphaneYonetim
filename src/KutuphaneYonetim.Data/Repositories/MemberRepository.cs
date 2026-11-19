using KutuphaneYonetim.Data.Persistence;
using KutuphaneYonetim.Domain;
using KutuphaneYonetim.Domain.Entities;
using KutuphaneYonetim.Domain.Repositories;
using Microsoft.EntityFrameworkCore;

namespace KutuphaneYonetim.Data.Repositories;

internal sealed class MemberRepository(IDbContextFactory<LibraryDbContext> contextFactory) : IMemberRepository
{
    public async Task<IReadOnlyList<Member>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        await using var db = await contextFactory.CreateDbContextAsync(cancellationToken).ConfigureAwait(false);
        return await db.Members
            .AsNoTracking()
            .OrderBy(m => m.Id)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
    }

    public async Task<PagedResult<MemberSummary>> SearchAsync(MemberQuery query, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(query);

        await using var db = await contextFactory.CreateDbContextAsync(cancellationToken).ConfigureAwait(false);
        var members = db.Members.AsNoTracking();

        if (!string.IsNullOrEmpty(query.Text))
        {
            var pattern = SqlLike.Contains(query.Text);
            members = members.Where(m =>
                EF.Functions.Like(m.FirstName, pattern, SqlLike.Escape)
                || EF.Functions.Like(m.LastName, pattern, SqlLike.Escape)
                || EF.Functions.Like(m.FirstName + " " + m.LastName, pattern, SqlLike.Escape));
        }

        members = query.Filter switch
        {
            MemberFilter.WithUnpaidFine => members.Where(m =>
                db.Loans.Any(l => l.MemberId == m.Id && l.FineAmount > 0 && l.FinePaidDate == null)),
            MemberFilter.WithActiveLoan => members.Where(m =>
                db.Loans.Any(l => l.MemberId == m.Id && l.ReturnDate == null)),
            _ => members,
        };

        var total = await members.CountAsync(cancellationToken).ConfigureAwait(false);
        var items = await members
            .OrderBy(m => m.Id)
            .Skip(SqlLike.Skip(query.Page, query.PageSize))
            .Take(query.PageSize)
            .Select(m => new MemberSummary
            {
                Id = m.Id,
                FirstName = m.FirstName,
                LastName = m.LastName,
                ActiveLoanCount = db.Loans.Count(l => l.MemberId == m.Id && l.ReturnDate == null),
                UnpaidFine = db.Loans
                    .Where(l => l.MemberId == m.Id && l.FineAmount > 0 && l.FinePaidDate == null)
                    .Sum(l => l.FineAmount) ?? 0m,
            })
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        return new PagedResult<MemberSummary>(items, total, query.Page, query.PageSize);
    }

    public async Task<Member?> GetByIdAsync(int id, CancellationToken cancellationToken = default)
    {
        await using var db = await contextFactory.CreateDbContextAsync(cancellationToken).ConfigureAwait(false);
        return await db.Members
            .AsNoTracking()
            .SingleOrDefaultAsync(m => m.Id == id, cancellationToken)
            .ConfigureAwait(false);
    }

    public async Task<int> AddAsync(Member member, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(member);

        await using var db = await contextFactory.CreateDbContextAsync(cancellationToken).ConfigureAwait(false);
        var entity = new Member { FirstName = member.FirstName, LastName = member.LastName };
        db.Members.Add(entity);
        await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return entity.Id;
    }

    public async Task<bool> DeleteAsync(int id, CancellationToken cancellationToken = default)
    {
        await using var db = await contextFactory.CreateDbContextAsync(cancellationToken).ConfigureAwait(false);
        var affected = await db.Members
            .Where(m => m.Id == id)
            .ExecuteDeleteAsync(cancellationToken)
            .ConfigureAwait(false);
        return affected > 0;
    }
}

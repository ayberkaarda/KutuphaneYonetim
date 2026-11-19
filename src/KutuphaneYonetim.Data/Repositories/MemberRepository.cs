using KutuphaneYonetim.Data.Persistence;
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

    public async Task<IReadOnlyList<MemberSummary>> GetSummariesAsync(CancellationToken cancellationToken = default)
    {
        await using var db = await contextFactory.CreateDbContextAsync(cancellationToken).ConfigureAwait(false);
        return await db.Members
            .AsNoTracking()
            .OrderBy(m => m.Id)
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

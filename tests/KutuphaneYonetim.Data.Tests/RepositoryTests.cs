using System.Data.Common;
using KutuphaneYonetim.Domain.Entities;
using KutuphaneYonetim.Domain.Repositories;
using Microsoft.EntityFrameworkCore;

namespace KutuphaneYonetim.Data.Tests;

/// <summary>Repository behaviour on SQLite in-memory (no SQL Server needed).</summary>
public sealed class RepositoryTests : IAsyncLifetime
{
    private static readonly DateTime LoanDate = new(2026, 4, 1, 10, 0, 0, DateTimeKind.Unspecified);

    private readonly SqliteDatabase _db = new();

    public ValueTask InitializeAsync() => _db.InitializeAsync();

    public ValueTask DisposeAsync() => _db.DisposeAsync();

    private IBookRepository Books => _db.Get<IBookRepository>();

    private IMemberRepository Members => _db.Get<IMemberRepository>();

    private ILoanRepository Loans => _db.Get<ILoanRepository>();

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Initialize_CreatesSchemaWithSampleData()
    {
        Assert.Equal(8, (await Books.GetAllAsync(Ct)).Count);
        Assert.Equal(3, (await Members.GetAllAsync(Ct)).Count);
        Assert.Equal(2, (await Loans.GetAllDetailsAsync(Ct)).Count);
    }

    [Fact]
    public async Task Books_AddGetDelete_RoundTrip()
    {
        var id = await Books.AddAsync(new Book { Title = "Yeni", Author = "Yazar", IsAvailable = true }, Ct);

        var stored = await Books.GetByIdAsync(id, Ct);
        Assert.NotNull(stored);
        Assert.Equal("Yeni", stored.Title);
        Assert.Equal("Yazar", stored.Author);
        Assert.True(stored.IsAvailable);

        Assert.True(await Books.DeleteAsync(id, Ct));
        Assert.Null(await Books.GetByIdAsync(id, Ct));
        Assert.False(await Books.DeleteAsync(id, Ct));
    }

    [Fact]
    public async Task Books_GetAvailable_ExcludesBooksOnLoan()
    {
        var available = await Books.GetAvailableAsync(Ct);

        Assert.Equal(7, available.Count);
        Assert.DoesNotContain(available, b => b.Id == 3);
        Assert.Equal(available.Select(b => b.Title).Order(StringComparer.Ordinal), available.Select(b => b.Title), StringComparer.Ordinal);
    }

    [Fact]
    public async Task Members_AddGetDelete_RoundTrip()
    {
        var id = await Members.AddAsync(new Member { FirstName = "Ada", LastName = "Kaya" }, Ct);

        var stored = await Members.GetByIdAsync(id, Ct);
        Assert.NotNull(stored);
        Assert.Equal("Ada Kaya", stored.FullName);

        Assert.True(await Members.DeleteAsync(id, Ct));
        Assert.Null(await Members.GetByIdAsync(id, Ct));
    }

    [Fact]
    public async Task Lend_AvailableBook_CreatesOpenLoanAndMarksBookUnavailable()
    {
        var loanId = await Loans.LendAsync(4, 3, LoanDate, Ct);

        Assert.NotNull(loanId);
        Assert.False((await Books.GetByIdAsync(4, Ct))!.IsAvailable);
        Assert.True(await Loans.HasActiveLoanForBookAsync(4, Ct));
        Assert.True(await Loans.HasActiveLoanForMemberAsync(3, Ct));

        var detail = Assert.Single(await Loans.GetAllDetailsAsync(Ct), l => l.Id == loanId);
        Assert.Equal("Kürk Mantolu Madonna", detail.BookTitle);
        Assert.Equal("Test Okur", detail.MemberName);
        Assert.Equal(LoanDate, detail.LoanDate);
        Assert.True(detail.IsActive);
    }

    [Fact]
    public async Task Lend_BookAlreadyOnLoan_ReturnsNullAndKeepsSingleOpenLoan()
    {
        Assert.NotNull(await Loans.LendAsync(4, 1, LoanDate, Ct));

        Assert.Null(await Loans.LendAsync(4, 2, LoanDate, Ct));
        Assert.Null(await Loans.LendAsync(3, 1, LoanDate, Ct)); // Seeded open loan.

        var open = (await Loans.GetAllDetailsAsync(Ct)).Where(l => l.BookId == 4 && l.IsActive);
        Assert.Equal(1, Assert.Single(open).MemberId);
    }

    [Fact]
    public async Task Lend_UnknownBook_ReturnsNull()
    {
        Assert.Null(await Loans.LendAsync(999, 1, LoanDate, Ct));
    }

    [Fact]
    public async Task Return_OpenLoan_SetsReturnDateAndMakesBookAvailable()
    {
        var returnDate = LoanDate.AddDays(7);

        Assert.True(await Loans.ReturnAsync(3, returnDate, Ct));

        Assert.True((await Books.GetByIdAsync(3, Ct))!.IsAvailable);
        Assert.False(await Loans.HasActiveLoanForBookAsync(3, Ct));
        Assert.Equal(returnDate, Assert.Single(await Loans.GetAllDetailsAsync(Ct), l => l.Id == 2).ReturnDate);

        // The book can be lent again after the return.
        Assert.NotNull(await Loans.LendAsync(3, 1, returnDate, Ct));
    }

    [Fact]
    public async Task Return_WithoutOpenLoan_ReturnsFalse()
    {
        Assert.False(await Loans.ReturnAsync(4, LoanDate, Ct));
        Assert.False(await Loans.ReturnAsync(1, LoanDate, Ct)); // Loan history exists but is closed.
    }

    [Fact]
    public async Task LoanHistoryQueries_DistinguishOpenAndClosedLoans()
    {
        Assert.True(await Loans.HasAnyLoanForBookAsync(1, Ct));
        Assert.False(await Loans.HasActiveLoanForBookAsync(1, Ct));
        Assert.True(await Loans.HasAnyLoanForMemberAsync(1, Ct));
        Assert.False(await Loans.HasActiveLoanForMemberAsync(1, Ct));
        Assert.False(await Loans.HasAnyLoanForBookAsync(4, Ct));
        Assert.False(await Loans.HasAnyLoanForMemberAsync(3, Ct));
    }

    [Fact]
    public async Task Database_RejectsSecondOpenLoanForSameBook()
    {
        await using var context = _db.CreateContext();
        context.Loans.Add(new Loan { BookId = 3, MemberId = 1, LoanDate = LoanDate });

        await Assert.ThrowsAsync<DbUpdateException>(() => context.SaveChangesAsync(Ct));
    }

    [Fact]
    public async Task Database_RefusesToDeleteBookOrMemberWithLoans()
    {
        // Bulk deletes bypass the change tracker, so the provider error surfaces directly.
        await Assert.ThrowsAnyAsync<DbException>(() => Books.DeleteAsync(1, Ct));
        await Assert.ThrowsAnyAsync<DbException>(() => Members.DeleteAsync(1, Ct));
        Assert.NotNull(await Books.GetByIdAsync(1, Ct));
    }
}

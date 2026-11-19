using System.Data.Common;
using KutuphaneYonetim.Domain;
using KutuphaneYonetim.Domain.Entities;
using KutuphaneYonetim.Domain.Repositories;
using Microsoft.EntityFrameworkCore;

namespace KutuphaneYonetim.Data.Tests;

/// <summary>Repository behaviour on SQLite in-memory (no SQL Server needed).</summary>
public sealed class RepositoryTests : IAsyncLifetime
{
    private static readonly DateTime LoanDate = new(2026, 4, 1, 10, 0, 0, DateTimeKind.Unspecified);
    private static readonly DateTime DueDate = LoanDate.Date.AddDays(14);

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
        Assert.Equal(LendOutcome.Lent, await Loans.LendAsync(4, 3, LoanDate, DueDate.AddHours(9), Ct));

        Assert.False((await Books.GetByIdAsync(4, Ct))!.IsAvailable);
        Assert.True(await Loans.HasActiveLoanForBookAsync(4, Ct));
        Assert.True(await Loans.HasActiveLoanForMemberAsync(3, Ct));

        var detail = Assert.Single(await Loans.GetAllDetailsAsync(Ct), l => l.BookId == 4);
        Assert.Equal("Kürk Mantolu Madonna", detail.BookTitle);
        Assert.Equal("Test Okur", detail.MemberName);
        Assert.Equal(LoanDate, detail.LoanDate);
        Assert.Equal(DueDate, detail.DueDate); // Stored without the time of day.
        Assert.Null(detail.FineAmount);
        Assert.True(detail.IsActive);

        var active = await Loans.GetActiveLoanForBookAsync(4, Ct);
        Assert.NotNull(active);
        Assert.Equal(3, active.MemberId);
    }

    [Fact]
    public async Task Lend_BookAlreadyOnLoan_ReturnsNullAndKeepsSingleOpenLoan()
    {
        Assert.Equal(LendOutcome.Lent, await Loans.LendAsync(4, 1, LoanDate, DueDate, Ct));

        Assert.Equal(LendOutcome.BookOnLoan, await Loans.LendAsync(4, 2, LoanDate, DueDate, Ct));
        Assert.Equal(LendOutcome.BookOnLoan, await Loans.LendAsync(3, 1, LoanDate, DueDate, Ct)); // Seeded open loan.

        var open = (await Loans.GetAllDetailsAsync(Ct)).Where(l => l.BookId == 4 && l.IsActive);
        Assert.Equal(1, Assert.Single(open).MemberId);
    }

    [Fact]
    public async Task Lend_UnknownBook_ReportsNotFound()
    {
        Assert.Equal(LendOutcome.BookNotFound, await Loans.LendAsync(999, 1, LoanDate, DueDate, Ct));
    }

    [Fact]
    public async Task Return_OpenLoan_SetsReturnDateAndMakesBookAvailable()
    {
        var returnDate = LoanDate.AddDays(7);

        Assert.True(await Loans.ReturnAsync(2, returnDate, null, Ct)); // Seeded open loan of book 3.

        Assert.True((await Books.GetByIdAsync(3, Ct))!.IsAvailable);
        Assert.False(await Loans.HasActiveLoanForBookAsync(3, Ct));
        var returned = Assert.Single(await Loans.GetAllDetailsAsync(Ct), l => l.Id == 2);
        Assert.Equal(returnDate, returned.ReturnDate);
        Assert.Null(returned.FineAmount);

        // The book can be lent again after the return.
        Assert.Equal(LendOutcome.Lent, await Loans.LendAsync(3, 1, returnDate, DueDate, Ct));
    }

    [Fact]
    public async Task Return_WithoutOpenLoan_ReturnsFalse()
    {
        Assert.False(await Loans.ReturnAsync(999, LoanDate, null, Ct));
        Assert.False(await Loans.ReturnAsync(1, LoanDate, null, Ct)); // Loan exists but is closed.
        Assert.Null(await Loans.GetActiveLoanForBookAsync(1, Ct));
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

    [Fact]
    public async Task Return_WithFine_StoresItAsUnpaidUntilPaid()
    {
        Assert.Equal(LendOutcome.Lent, await Loans.LendAsync(4, 3, LoanDate, DueDate, Ct));
        var loanId = (await Loans.GetActiveLoanForBookAsync(4, Ct))!.Id;

        Assert.True(await Loans.ReturnAsync(loanId, DueDate.AddDays(3), 7.50m, Ct));
        Assert.True(await Loans.ReturnAsync(2, LoanDate.AddDays(30), 2.25m, Ct)); // Member 2.

        Assert.Equal(7.50m, Assert.Single(await Loans.GetAllDetailsAsync(Ct), l => l.Id == loanId).FineAmount);
        Assert.Equal(7.50m, await Loans.GetUnpaidFineTotalAsync(3, Ct));
        Assert.Equal(2.25m, await Loans.GetUnpaidFineTotalAsync(2, Ct));
        Assert.Equal(0m, await Loans.GetUnpaidFineTotalAsync(1, Ct));

        var summaries = (await Members.SearchAsync(new MemberQuery(), Ct)).Items;
        Assert.Equal([0m, 2.25m, 7.50m], summaries.Select(m => m.UnpaidFine));

        var paidDate = DueDate.AddDays(4);
        Assert.Equal(1, await Loans.PayFinesAsync(3, paidDate, Ct));
        Assert.Equal(0m, await Loans.GetUnpaidFineTotalAsync(3, Ct));
        Assert.Equal(paidDate, Assert.Single(await Loans.GetAllDetailsAsync(Ct), l => l.Id == loanId).FinePaidDate);
        Assert.Equal(0, await Loans.PayFinesAsync(3, paidDate, Ct)); // Nothing left to pay.
        Assert.Equal(2.25m, await Loans.GetUnpaidFineTotalAsync(2, Ct)); // Other members are untouched.
    }

    [Fact]
    public async Task Return_ZeroFine_IsStoredAsNoFine()
    {
        Assert.True(await Loans.ReturnAsync(2, LoanDate, 0m, Ct));

        Assert.Null(Assert.Single(await Loans.GetAllDetailsAsync(Ct), l => l.Id == 2).FineAmount);
    }

    [Fact]
    public async Task MemberSummaries_CountOpenLoans()
    {
        Assert.Equal(LendOutcome.Lent, await Loans.LendAsync(4, 2, LoanDate, DueDate, Ct));

        var summaries = (await Members.SearchAsync(new MemberQuery(), Ct)).Items;

        Assert.Equal([1, 2, 3], summaries.Select(m => m.Id));
        Assert.Equal([0, 2, 0], summaries.Select(m => m.ActiveLoanCount)); // Member 2 also has the seeded open loan.
        Assert.Equal("Deneme Kullanıcı", summaries[1].FullName);
    }

    [Fact]
    public async Task SeededLoans_HaveNoDueDateOrFine()
    {
        var all = await Loans.GetAllDetailsAsync(Ct);

        Assert.All(all, l => Assert.Null(l.DueDate));
        Assert.All(all, l => Assert.Null(l.FineAmount));
    }

    [Fact]
    public async Task Database_RejectsNegativeFine()
    {
        await using var context = _db.CreateContext();
        var loan = await context.Loans.SingleAsync(l => l.Id == 1, Ct);
        loan.FineAmount = -1m;

        await Assert.ThrowsAsync<DbUpdateException>(() => context.SaveChangesAsync(Ct));
    }
}

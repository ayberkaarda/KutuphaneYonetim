using KutuphaneYonetim.Business.Services;
using KutuphaneYonetim.Domain;
using KutuphaneYonetim.Domain.Entities;
using KutuphaneYonetim.Domain.Repositories;

namespace KutuphaneYonetim.Data.Tests;

/// <summary>Book and member search, filters and paging on SQLite in-memory (seeded with 8 books, 3 members).</summary>
public sealed class SearchTests : IAsyncLifetime
{
    private static readonly DateTime LoanDate = new(2026, 4, 1, 10, 0, 0, DateTimeKind.Unspecified);
    private static readonly DateTime HoldEnd = LoanDate.AddDays(60);

    private readonly SqliteDatabase _db = new();

    public ValueTask InitializeAsync() => _db.InitializeAsync();

    public ValueTask DisposeAsync() => _db.DisposeAsync();

    private IBookRepository Books => _db.Get<IBookRepository>();

    private IMemberRepository Members => _db.Get<IMemberRepository>();

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private async Task<int[]> BookIdsAsync(BookQuery query) =>
        (await Books.SearchAsync(query, Ct)).Items.Select(b => b.Id).ToArray();

    private async Task<int[]> MemberIdsAsync(MemberQuery query) =>
        (await Members.SearchAsync(query, Ct)).Items.Select(m => m.Id).ToArray();

    private async Task ExpectBooksAsync(BookQuery query, params int[] expected) =>
        Assert.Equal(expected, await BookIdsAsync(query));

    private async Task ExpectMembersAsync(MemberQuery query, params int[] expected) =>
        Assert.Equal(expected, await MemberIdsAsync(query));

    [Fact]
    public async Task Books_EmptyQuery_ReturnsAllOrderedById()
    {
        var result = await Books.SearchAsync(new BookQuery(), Ct);

        Assert.Equal(8, result.TotalCount);
        Assert.Equal([1, 2, 3, 4, 5, 6, 7, 8], result.Items.Select(b => b.Id));
    }

    [Theory]
    [InlineData("orwell", new[] { 1, 2 })] // Author, other case.
    [InlineData("Madonna", new[] { 4 })] // Part of the title.
    [InlineData("ce", new[] { 5, 8 })] // "Suç ve Ceza" and "İnce Memed".
    [InlineData("yok böyle bir kitap", new int[0])]
    public async Task Books_TextMatchesTitleOrAuthor(string text, int[] expected)
    {
        Assert.Equal(expected, await BookIdsAsync(new BookQuery(text)));
    }

    [Fact]
    public async Task Books_AvailabilityFilter()
    {
        await ExpectBooksAsync(new BookQuery(Availability: BookAvailability.OnLoan), 3);
        await ExpectBooksAsync(new BookQuery(Availability: BookAvailability.Available), 1, 2, 4, 5, 6, 7, 8);
        Assert.Empty(await BookIdsAsync(new BookQuery("Coelho", BookAvailability.Available)));
        await ExpectBooksAsync(new BookQuery("Coelho", BookAvailability.OnLoan), 3);
    }

    [Fact]
    public async Task Books_TextMatchesIsbn_AndBooksWithoutIsbnAreKept()
    {
        var id = await Books.AddAsync(new Book { Title = "Yeni", Author = "Yazar", Isbn = "9789750719387", IsAvailable = true }, Ct);

        Assert.Equal([id], await BookIdsAsync(new BookQuery("975071")));
        Assert.Equal(9, (await Books.SearchAsync(new BookQuery(), Ct)).TotalCount);
    }

    [Fact]
    public async Task Books_NumericHyphenatedTitle_RemainsSearchable()
    {
        var service = _db.Get<IBookService>();
        var titled = await service.AddAsync("1914-1918 Savaş Günlükleri", "Yazar", cancellationToken: Ct);
        var withIsbn = await service.AddAsync("Yeni", "Yazar", "9789750719387", Ct);

        // Text that looks like an ISBN still finds titles and authors as typed, and ISBNs without separators.
        Assert.Equal([titled.Id], (await service.SearchAsync(new BookQuery("1914-1918"), Ct)).Items.Select(b => b.Id));
        Assert.Equal([withIsbn.Id], (await service.SearchAsync(new BookQuery("978-975-07"), Ct)).Items.Select(b => b.Id));
    }

    [Fact]
    public async Task Books_WildcardCharactersAreMatchedLiterally()
    {
        var percent = await Books.AddAsync(new Book { Title = "Yüzde 100% Roman", Author = "Yazar", IsAvailable = true }, Ct);
        var underscore = await Books.AddAsync(new Book { Title = "Alt_Çizgi", Author = "Yazar", IsAvailable = true }, Ct);
        var bracket = await Books.AddAsync(new Book { Title = "[Köşeli] Ayraç", Author = "Yazar", IsAvailable = true }, Ct);

        Assert.Equal([percent], await BookIdsAsync(new BookQuery("%")));
        Assert.Equal([underscore], await BookIdsAsync(new BookQuery("_")));
        Assert.Equal([bracket], await BookIdsAsync(new BookQuery("[K")));
        Assert.Equal([percent], await BookIdsAsync(new BookQuery("0% R")));
    }

    [Fact]
    public async Task Books_Paging()
    {
        var page3 = await Books.SearchAsync(new BookQuery(Page: 3, PageSize: 3), Ct);

        Assert.Equal([7, 8], page3.Items.Select(b => b.Id));
        Assert.Equal(8, page3.TotalCount);
        Assert.Equal(3, page3.PageCount);
        Assert.Empty((await Books.SearchAsync(new BookQuery(Page: 4, PageSize: 3), Ct)).Items);
    }

    [Fact]
    public async Task BookService_PageBeyondTheEnd_AndHyphenatedIsbn()
    {
        var service = _db.Get<IBookService>();
        var book = await service.AddAsync("ISBN'li", "Yazar", "978-975-07-1938-7", Ct);

        var result = await service.SearchAsync(new BookQuery(Page: 99, PageSize: 4), Ct);
        Assert.Equal(3, result.Page);
        Assert.Equal([9], result.Items.Select(b => b.Id));

        Assert.Equal([book.Id], (await service.SearchAsync(new BookQuery("978-975-07"), Ct)).Items.Select(b => b.Id));
    }

    [Theory]
    [InlineData("deneme", new[] { 2 })]
    [InlineData("Okur", new[] { 3 })]
    [InlineData("Örnek Üye", new[] { 1 })] // First and last name together.
    [InlineData("e", new[] { 1, 2, 3 })]
    [InlineData("Zeynep", new int[0])]
    public async Task Members_TextMatchesNames(string text, int[] expected)
    {
        Assert.Equal(expected, await MemberIdsAsync(new MemberQuery(text)));
    }

    [Fact]
    public async Task Members_Filters()
    {
        var loans = _db.Get<ILoanRepository>();
        Assert.Equal(LendOutcome.Lent, await loans.LendAsync(4, 1, LoanDate, LoanDate.Date, Ct));
        Assert.True((await loans.ReturnAsync((await loans.GetActiveLoanForBookAsync(4, Ct))!.Id, LoanDate.AddDays(2), 5m, HoldEnd, Ct)).Returned);

        await ExpectMembersAsync(new MemberQuery(Filter: MemberFilter.WithActiveLoan), 2); // Seeded open loan.
        await ExpectMembersAsync(new MemberQuery(Filter: MemberFilter.WithUnpaidFine), 1);
        Assert.Empty(await MemberIdsAsync(new MemberQuery("Okur", MemberFilter.WithUnpaidFine)));

        await loans.PayFinesAsync(1, LoanDate.AddDays(3), Ct);
        Assert.Empty(await MemberIdsAsync(new MemberQuery(Filter: MemberFilter.WithUnpaidFine)));
    }

    [Fact]
    public async Task Members_Paging()
    {
        var page = await Members.SearchAsync(new MemberQuery(Page: 2, PageSize: 2), Ct);

        Assert.Equal([3], page.Items.Select(m => m.Id));
        Assert.Equal(3, page.TotalCount);
        Assert.Equal(2, page.PageCount);
    }
}

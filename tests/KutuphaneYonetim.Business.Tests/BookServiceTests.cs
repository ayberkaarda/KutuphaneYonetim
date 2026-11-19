using KutuphaneYonetim.Business.Services;
using KutuphaneYonetim.Business.Validation;
using KutuphaneYonetim.Domain;
using KutuphaneYonetim.Domain.Entities;
using KutuphaneYonetim.Domain.Repositories;
using Moq;

namespace KutuphaneYonetim.Business.Tests;

public sealed class BookServiceTests
{
    // Strict mocks: any repository call that a test did not expect fails the test.
    private readonly Mock<IBookRepository> _books = new(MockBehavior.Strict);
    private readonly Mock<ILoanRepository> _loans = new(MockBehavior.Strict);
    private readonly BookService _service;

    public BookServiceTests()
    {
        _service = new BookService(_books.Object, _loans.Object, new BookInputValidator());
    }

    [Fact]
    public async Task AddAsync_ValidInput_TrimsAndStoresAvailableBook()
    {
        Book? stored = null;
        _books.Setup(b => b.AddAsync(It.IsAny<Book>(), It.IsAny<CancellationToken>()))
            .Callback<Book, CancellationToken>((book, _) => stored = book)
            .ReturnsAsync(42);

        var result = await _service.AddAsync("  Simyacı ", " Paulo Coelho  ", cancellationToken: TestContext.Current.CancellationToken);

        Assert.Equal(42, result.Id);
        Assert.NotNull(stored);
        Assert.Equal("Simyacı", stored.Title);
        Assert.Equal("Paulo Coelho", stored.Author);
        Assert.True(stored.IsAvailable);
    }

    [Theory]
    [InlineData("", "Yazar", "Kitap adı alanı boş bırakılamaz.")]
    [InlineData("   ", "Yazar", "Kitap adı alanı boş bırakılamaz.")]
    [InlineData(null, "Yazar", "Kitap adı alanı boş bırakılamaz.")]
    [InlineData("Kitap", "", "Yazar alanı boş bırakılamaz.")]
    [InlineData("", "", "Kitap adı alanı boş bırakılamaz.")]
    public async Task AddAsync_MissingField_IsRejectedWithoutTouchingTheDatabase(
        string? title, string author, string expectedMessage)
    {
        var error = await Assert.ThrowsAsync<BusinessRuleException>(
            () => _service.AddAsync(title!, author, cancellationToken: TestContext.Current.CancellationToken));

        Assert.Equal(expectedMessage, error.Message);
        _books.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task AddAsync_TitleLongerThanLimit_IsRejected()
    {
        var error = await Assert.ThrowsAsync<BusinessRuleException>(
            () => _service.AddAsync(new string('a', 101), "Yazar", cancellationToken: TestContext.Current.CancellationToken));

        Assert.Equal("Kitap adı en fazla 100 karakter olabilir.", error.Message);
    }

    [Fact]
    public async Task AddAsync_AuthorLongerThanLimit_IsRejected()
    {
        var error = await Assert.ThrowsAsync<BusinessRuleException>(
            () => _service.AddAsync("Kitap", new string('a', 101), cancellationToken: TestContext.Current.CancellationToken));

        Assert.Equal("Yazar en fazla 100 karakter olabilir.", error.Message);
    }

    [Fact]
    public async Task AddAsync_LengthIsCheckedAfterTrimming()
    {
        _books.Setup(b => b.AddAsync(It.IsAny<Book>(), It.IsAny<CancellationToken>())).ReturnsAsync(1);

        var book = await _service.AddAsync(" " + new string('a', 100) + " ", "Yazar", cancellationToken: TestContext.Current.CancellationToken);

        Assert.Equal(100, book.Title.Length);
    }

    [Fact]
    public async Task DeleteAsync_UnknownBook_IsRejected()
    {
        _books.Setup(b => b.GetByIdAsync(7, It.IsAny<CancellationToken>())).ReturnsAsync((Book?)null);

        var error = await Assert.ThrowsAsync<BusinessRuleException>(
            () => _service.DeleteAsync(7, TestContext.Current.CancellationToken));

        Assert.Equal("Seçilen kitap bulunamadı.", error.Message);
    }

    [Fact]
    public async Task DeleteAsync_BookOnLoan_IsRejected()
    {
        _books.Setup(b => b.GetByIdAsync(7, It.IsAny<CancellationToken>())).ReturnsAsync(new Book { Id = 7 });
        _loans.Setup(l => l.HasActiveLoanForBookAsync(7, It.IsAny<CancellationToken>())).ReturnsAsync(true);

        var error = await Assert.ThrowsAsync<BusinessRuleException>(
            () => _service.DeleteAsync(7, TestContext.Current.CancellationToken));

        Assert.Equal("Bu kitap şu anda ödünçte olduğu için silinemez. Önce iade alınmalıdır.", error.Message);
        _books.Verify(b => b.DeleteAsync(It.IsAny<int>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task DeleteAsync_BookWithLoanHistory_IsRejected()
    {
        _books.Setup(b => b.GetByIdAsync(7, It.IsAny<CancellationToken>())).ReturnsAsync(new Book { Id = 7 });
        _loans.Setup(l => l.HasActiveLoanForBookAsync(7, It.IsAny<CancellationToken>())).ReturnsAsync(false);
        _loans.Setup(l => l.HasAnyLoanForBookAsync(7, It.IsAny<CancellationToken>())).ReturnsAsync(true);

        var error = await Assert.ThrowsAsync<BusinessRuleException>(
            () => _service.DeleteAsync(7, TestContext.Current.CancellationToken));

        Assert.Equal("Bu kitabın ödünç geçmişi bulunduğu için silinemez.", error.Message);
        _books.Verify(b => b.DeleteAsync(It.IsAny<int>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task DeleteAsync_BookWithoutLoans_IsDeleted()
    {
        _books.Setup(b => b.GetByIdAsync(7, It.IsAny<CancellationToken>())).ReturnsAsync(new Book { Id = 7 });
        _loans.Setup(l => l.HasActiveLoanForBookAsync(7, It.IsAny<CancellationToken>())).ReturnsAsync(false);
        _loans.Setup(l => l.HasAnyLoanForBookAsync(7, It.IsAny<CancellationToken>())).ReturnsAsync(false);
        _books.Setup(b => b.DeleteAsync(7, It.IsAny<CancellationToken>())).ReturnsAsync(true);

        await _service.DeleteAsync(7, TestContext.Current.CancellationToken);

        _books.Verify(b => b.DeleteAsync(7, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Theory]
    [InlineData("978-975-07-1938-7", "9789750719387")]
    [InlineData(" 0 306 40615 2 ", "0306406152")]
    [InlineData("080442957x", "080442957X")]
    public async Task AddAsync_ValidIsbn_IsStoredWithoutSeparators(string isbn, string expected)
    {
        Book? stored = null;
        _books.Setup(b => b.AddAsync(It.IsAny<Book>(), It.IsAny<CancellationToken>()))
            .Callback<Book, CancellationToken>((book, _) => stored = book)
            .ReturnsAsync(1);

        await _service.AddAsync("Kitap", "Yazar", isbn, TestContext.Current.CancellationToken);

        Assert.Equal(expected, stored!.Isbn);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public async Task AddAsync_WithoutIsbn_StoresNull(string? isbn)
    {
        Book? stored = null;
        _books.Setup(b => b.AddAsync(It.IsAny<Book>(), It.IsAny<CancellationToken>()))
            .Callback<Book, CancellationToken>((book, _) => stored = book)
            .ReturnsAsync(1);

        await _service.AddAsync("Kitap", "Yazar", isbn, TestContext.Current.CancellationToken);

        Assert.Null(stored!.Isbn);
    }

    [Theory]
    [InlineData("9789750719386")] // Wrong check digit.
    [InlineData("978975071938")] // 12 digits.
    [InlineData("97897507193877")] // 14 digits.
    [InlineData("0306406153")]
    [InlineData("X306406152")]
    [InlineData("97897507193A7")]
    public async Task AddAsync_InvalidIsbn_IsRejected(string isbn)
    {
        var error = await Assert.ThrowsAsync<BusinessRuleException>(
            () => _service.AddAsync("Kitap", "Yazar", isbn, TestContext.Current.CancellationToken));

        Assert.StartsWith("ISBN geçersiz.", error.Message, StringComparison.Ordinal);
        _books.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task SearchAsync_NormalisesTextAndPaging()
    {
        BookQuery? sent = null;
        _books.Setup(b => b.SearchAsync(It.IsAny<BookQuery>(), It.IsAny<CancellationToken>()))
            .Callback<BookQuery, CancellationToken>((q, _) => sent = q)
            .ReturnsAsync(new PagedResult<Book>([], 0, 1, 200));

        await _service.SearchAsync(
            new BookQuery("  orwell  ", BookAvailability.OnLoan, Page: 0, PageSize: 5000), TestContext.Current.CancellationToken);

        Assert.Equal(new BookQuery("orwell", BookAvailability.OnLoan, 1, 200), sent);
    }

    [Theory]
    [InlineData("   ", null)]
    [InlineData("978-975 07", "97897507")] // Looks like an ISBN: separators are dropped.
    [InlineData("1984", "1984")]
    [InlineData("Suç ve Ceza", "Suç ve Ceza")]
    public async Task SearchAsync_TextNormalisation(string text, string? expected)
    {
        BookQuery? sent = null;
        _books.Setup(b => b.SearchAsync(It.IsAny<BookQuery>(), It.IsAny<CancellationToken>()))
            .Callback<BookQuery, CancellationToken>((q, _) => sent = q)
            .ReturnsAsync(new PagedResult<Book>([], 0, 1, 50));

        await _service.SearchAsync(new BookQuery(text), TestContext.Current.CancellationToken);

        Assert.Equal(expected, sent!.Text);
    }

    [Fact]
    public async Task SearchAsync_TextIsCutToMaximumLength()
    {
        BookQuery? sent = null;
        _books.Setup(b => b.SearchAsync(It.IsAny<BookQuery>(), It.IsAny<CancellationToken>()))
            .Callback<BookQuery, CancellationToken>((q, _) => sent = q)
            .ReturnsAsync(new PagedResult<Book>([], 0, 1, 50));

        await _service.SearchAsync(new BookQuery(new string('a', 150)), TestContext.Current.CancellationToken);

        Assert.Equal(100, sent!.Text!.Length);
    }

    [Fact]
    public async Task SearchAsync_PageBeyondTheEnd_ReturnsLastPage()
    {
        var lastPage = new PagedResult<Book>([new Book { Id = 9 }], 21, 3, 10);
        _books.Setup(b => b.SearchAsync(It.Is<BookQuery>(q => q.Page == 7), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new PagedResult<Book>([], 21, 7, 10));
        _books.Setup(b => b.SearchAsync(It.Is<BookQuery>(q => q.Page == 3), It.IsAny<CancellationToken>()))
            .ReturnsAsync(lastPage);

        var result = await _service.SearchAsync(new BookQuery(Page: 7, PageSize: 10), TestContext.Current.CancellationToken);

        Assert.Same(lastPage, result);
    }

    [Fact]
    public async Task SearchAsync_EmptyResult_StaysOnFirstPage()
    {
        _books.Setup(b => b.SearchAsync(It.IsAny<BookQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new PagedResult<Book>([], 0, 1, 50));

        var result = await _service.SearchAsync(new BookQuery("yok"), TestContext.Current.CancellationToken);

        Assert.Equal(1, result.PageCount);
        _books.Verify(b => b.SearchAsync(It.IsAny<BookQuery>(), It.IsAny<CancellationToken>()), Times.Once);
    }
}

using KutuphaneYonetim.Business.Services;
using KutuphaneYonetim.Business.Validation;
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

        var result = await _service.AddAsync("  Simyacı ", " Paulo Coelho  ", TestContext.Current.CancellationToken);

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
            () => _service.AddAsync(title!, author, TestContext.Current.CancellationToken));

        Assert.Equal(expectedMessage, error.Message);
        _books.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task AddAsync_TitleLongerThanLimit_IsRejected()
    {
        var error = await Assert.ThrowsAsync<BusinessRuleException>(
            () => _service.AddAsync(new string('a', 101), "Yazar", TestContext.Current.CancellationToken));

        Assert.Equal("Kitap adı en fazla 100 karakter olabilir.", error.Message);
    }

    [Fact]
    public async Task AddAsync_AuthorLongerThanLimit_IsRejected()
    {
        var error = await Assert.ThrowsAsync<BusinessRuleException>(
            () => _service.AddAsync("Kitap", new string('a', 101), TestContext.Current.CancellationToken));

        Assert.Equal("Yazar en fazla 100 karakter olabilir.", error.Message);
    }

    [Fact]
    public async Task AddAsync_LengthIsCheckedAfterTrimming()
    {
        _books.Setup(b => b.AddAsync(It.IsAny<Book>(), It.IsAny<CancellationToken>())).ReturnsAsync(1);

        var book = await _service.AddAsync(" " + new string('a', 100) + " ", "Yazar", TestContext.Current.CancellationToken);

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
}

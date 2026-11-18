using KutuphaneYonetim.Business.Services;
using KutuphaneYonetim.Business.Validation;
using KutuphaneYonetim.Domain.Entities;
using KutuphaneYonetim.Domain.Repositories;
using Moq;

namespace KutuphaneYonetim.Business.Tests;

public sealed class LoanServiceTests
{
    private static readonly DateTimeOffset Now = new(2026, 3, 14, 9, 30, 0, TimeSpan.Zero);

    private readonly Mock<IBookRepository> _books = new(MockBehavior.Strict);
    private readonly Mock<IMemberRepository> _members = new(MockBehavior.Strict);
    private readonly Mock<ILoanRepository> _loans = new(MockBehavior.Strict);
    private readonly LoanService _service;

    public LoanServiceTests()
    {
        _service = new LoanService(
            _books.Object, _members.Object, _loans.Object, new LoanInputValidator(), new FixedTimeProvider(Now));
    }

    private void GivenBookAndMember(int bookId, int memberId)
    {
        _books.Setup(b => b.GetByIdAsync(bookId, It.IsAny<CancellationToken>())).ReturnsAsync(new Book { Id = bookId });
        _members.Setup(m => m.GetByIdAsync(memberId, It.IsAny<CancellationToken>())).ReturnsAsync(new Member { Id = memberId });
    }

    [Fact]
    public async Task LendAsync_AvailableBook_CreatesLoanWithCurrentTime()
    {
        GivenBookAndMember(2, 4);
        _loans.Setup(l => l.LendAsync(2, 4, Now.UtcDateTime, It.IsAny<CancellationToken>())).ReturnsAsync(10);

        await _service.LendAsync(2, 4, TestContext.Current.CancellationToken);

        _loans.Verify(l => l.LendAsync(2, 4, Now.UtcDateTime, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task LendAsync_BookAlreadyOnLoan_IsRejected()
    {
        GivenBookAndMember(2, 4);
        _loans.Setup(l => l.LendAsync(2, 4, It.IsAny<DateTime>(), It.IsAny<CancellationToken>())).ReturnsAsync((int?)null);

        var error = await Assert.ThrowsAsync<BusinessRuleException>(
            () => _service.LendAsync(2, 4, TestContext.Current.CancellationToken));

        Assert.Equal("Bu kitap şu anda başka bir üyede. Aynı kitap iki üyeye ödünç verilemez.", error.Message);
    }

    [Fact]
    public async Task LendAsync_UnknownBook_IsRejected()
    {
        _books.Setup(b => b.GetByIdAsync(2, It.IsAny<CancellationToken>())).ReturnsAsync((Book?)null);

        var error = await Assert.ThrowsAsync<BusinessRuleException>(
            () => _service.LendAsync(2, 4, TestContext.Current.CancellationToken));

        Assert.Equal("Seçilen kitap bulunamadı.", error.Message);
        _loans.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task LendAsync_UnknownMember_IsRejected()
    {
        _books.Setup(b => b.GetByIdAsync(2, It.IsAny<CancellationToken>())).ReturnsAsync(new Book { Id = 2 });
        _members.Setup(m => m.GetByIdAsync(4, It.IsAny<CancellationToken>())).ReturnsAsync((Member?)null);

        var error = await Assert.ThrowsAsync<BusinessRuleException>(
            () => _service.LendAsync(2, 4, TestContext.Current.CancellationToken));

        Assert.Equal("Seçilen üye bulunamadı.", error.Message);
        _loans.VerifyNoOtherCalls();
    }

    [Theory]
    [InlineData(0, 4, "Seçilen kitap bulunamadı.")]
    [InlineData(-1, 4, "Seçilen kitap bulunamadı.")]
    [InlineData(2, 0, "Seçilen üye bulunamadı.")]
    public async Task LendAsync_NonPositiveIdsThatDoNotExist_AreRejectedAsNotFound(int bookId, int memberId, string expectedMessage)
    {
        _books.Setup(b => b.GetByIdAsync(bookId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(bookId == 2 ? new Book { Id = bookId } : null);
        _members.Setup(m => m.GetByIdAsync(memberId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((Member?)null);

        var error = await Assert.ThrowsAsync<BusinessRuleException>(
            () => _service.LendAsync(bookId, memberId, TestContext.Current.CancellationToken));

        Assert.Equal(expectedMessage, error.Message);
        _loans.VerifyNoOtherCalls();
    }

    [Theory]
    [InlineData(0, 0)]
    [InlineData(-5, -7)]
    public async Task LendAsync_ExistingLegacyRecordsWithNonPositiveIds_CanBeLent(int bookId, int memberId)
    {
        GivenBookAndMember(bookId, memberId);
        _loans.Setup(l => l.LendAsync(bookId, memberId, Now.UtcDateTime, It.IsAny<CancellationToken>())).ReturnsAsync(10);

        await _service.LendAsync(bookId, memberId, TestContext.Current.CancellationToken);

        _loans.Verify(l => l.LendAsync(bookId, memberId, Now.UtcDateTime, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task ReturnAsync_OpenLoan_IsClosedWithCurrentTime()
    {
        _loans.Setup(l => l.ReturnAsync(2, Now.UtcDateTime, It.IsAny<CancellationToken>())).ReturnsAsync(true);

        await _service.ReturnAsync(2, TestContext.Current.CancellationToken);

        _loans.Verify(l => l.ReturnAsync(2, Now.UtcDateTime, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task ReturnAsync_NoOpenLoan_IsRejected()
    {
        _loans.Setup(l => l.ReturnAsync(2, It.IsAny<DateTime>(), It.IsAny<CancellationToken>())).ReturnsAsync(false);

        var error = await Assert.ThrowsAsync<BusinessRuleException>(
            () => _service.ReturnAsync(2, TestContext.Current.CancellationToken));

        Assert.Equal("Bu kitap için açık bir ödünç kaydı bulunamadı.", error.Message);
    }

    /// <summary>A clock frozen at a given instant, in UTC so the local time is predictable.</summary>
    private sealed class FixedTimeProvider(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;

        public override TimeZoneInfo LocalTimeZone => TimeZoneInfo.Utc;
    }
}

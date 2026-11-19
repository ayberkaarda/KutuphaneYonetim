using KutuphaneYonetim.Business.Services;
using KutuphaneYonetim.Domain.Entities;
using KutuphaneYonetim.Domain.Repositories;
using Moq;

namespace KutuphaneYonetim.Business.Tests;

public sealed class ReservationServiceTests
{
    private static readonly DateTimeOffset Now = new(2026, 3, 14, 9, 30, 0, TimeSpan.Zero);
    private static readonly DateTime LocalNow = Now.UtcDateTime;

    private readonly Mock<IBookRepository> _books = new(MockBehavior.Strict);
    private readonly Mock<IMemberRepository> _members = new(MockBehavior.Strict);
    private readonly Mock<IReservationRepository> _reservations = new(MockBehavior.Strict);
    private readonly LibraryPolicy _policy = new() { ReservationHoldDays = 3 };
    private readonly ReservationService _service;

    public ReservationServiceTests()
    {
        _service = new ReservationService(
            _books.Object, _members.Object, _reservations.Object, _policy, new FixedTimeProvider(Now));
        _reservations.Setup(r => r.ExpireHoldsAsync(LocalNow, LocalNow.AddDays(3), It.IsAny<CancellationToken>())).ReturnsAsync(0);
        _reservations
            .Setup(r => r.ExpireHoldsForBookAsync(It.IsAny<int>(), LocalNow, LocalNow.AddDays(3), It.IsAny<CancellationToken>()))
            .ReturnsAsync(0);
    }

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private void GivenBook(int bookId, ReserveOutcome outcome)
    {
        _books.Setup(b => b.GetByIdAsync(bookId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Book { Id = bookId });
        _members.Setup(m => m.GetByIdAsync(It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((int id, CancellationToken _) => new Member { Id = id });
        _reservations.Setup(r => r.AddAsync(bookId, It.IsAny<int>(), LocalNow, It.IsAny<CancellationToken>()))
            .ReturnsAsync(outcome);
    }

    [Fact]
    public async Task ReserveAsync_Added_UsesCurrentTime()
    {
        GivenBook(2, ReserveOutcome.Added);

        await _service.ReserveAsync(2, 4, Ct);

        _reservations.Verify(r => r.AddAsync(2, 4, LocalNow, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Theory]
    [InlineData(ReserveOutcome.OnShelf, "Bu kitap rafta. Rezervasyon yerine doğrudan ödünç verilebilir.")]
    [InlineData(ReserveOutcome.BorrowedByMember, "Bu kitap zaten bu üyede; kendi ödünç aldığı kitabı rezerve edemez.")]
    [InlineData(ReserveOutcome.AlreadyActive, "Bu üyenin bu kitap için zaten aktif bir rezervasyonu var.")]
    [InlineData(ReserveOutcome.BookNotFound, "Seçilen kitap bulunamadı.")]
    public async Task ReserveAsync_RefusedByRepository_ShowsReason(ReserveOutcome outcome, string expectedMessage)
    {
        GivenBook(2, outcome);

        var error = await Assert.ThrowsAsync<BusinessRuleException>(() => _service.ReserveAsync(2, 4, Ct));

        Assert.Equal(expectedMessage, error.Message);
    }

    [Fact]
    public async Task ReserveAsync_UnknownBookOrMember_IsRejected()
    {
        _books.Setup(b => b.GetByIdAsync(2, It.IsAny<CancellationToken>())).ReturnsAsync((Book?)null);
        Assert.Equal(
            "Seçilen kitap bulunamadı.",
            (await Assert.ThrowsAsync<BusinessRuleException>(() => _service.ReserveAsync(2, 4, Ct))).Message);

        _books.Setup(b => b.GetByIdAsync(2, It.IsAny<CancellationToken>())).ReturnsAsync(new Book { Id = 2 });
        _members.Setup(m => m.GetByIdAsync(4, It.IsAny<CancellationToken>())).ReturnsAsync((Member?)null);
        Assert.Equal(
            "Seçilen üye bulunamadı.",
            (await Assert.ThrowsAsync<BusinessRuleException>(() => _service.ReserveAsync(2, 4, Ct))).Message);
    }

    [Fact]
    public async Task ReserveAsync_ProcessesExpiredHoldsOfThatBookFirst()
    {
        // The stale hold is expired first, so the book is seen as on the shelf afterwards. Only this book
        // is expired: other books' locks are never waited for.
        var calls = new List<string>();
        _reservations.Setup(r => r.ExpireHoldsForBookAsync(2, LocalNow, LocalNow.AddDays(3), It.IsAny<CancellationToken>()))
            .Callback(() => calls.Add("expire"))
            .ReturnsAsync(1);
        GivenBook(2, ReserveOutcome.OnShelf);
        _reservations.Setup(r => r.AddAsync(2, 4, LocalNow, It.IsAny<CancellationToken>()))
            .Callback(() => calls.Add("add"))
            .ReturnsAsync(ReserveOutcome.OnShelf);

        await Assert.ThrowsAsync<BusinessRuleException>(() => _service.ReserveAsync(2, 4, Ct));

        Assert.Equal(["expire", "add"], calls);
        _reservations.Verify(
            r => r.ExpireHoldsAsync(It.IsAny<DateTime>(), It.IsAny<DateTime>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task GetActiveAsync_NumbersEachQueue_HeldMemberIsZero_SameTimeOrderedById()
    {
        var sameTime = LocalNow.AddDays(-2);
        _reservations.Setup(r => r.GetActiveDetailsAsync(It.IsAny<CancellationToken>())).ReturnsAsync(
        [
            new ReservationDetail { Id = 11, BookId = 1, CreatedAt = sameTime, Status = ReservationStatus.Waiting },
            new ReservationDetail { Id = 10, BookId = 1, CreatedAt = sameTime, Status = ReservationStatus.Waiting },
            new ReservationDetail { Id = 5, BookId = 1, CreatedAt = sameTime.AddDays(-1), Status = ReservationStatus.Ready },
            new ReservationDetail { Id = 20, BookId = 2, CreatedAt = sameTime, Status = ReservationStatus.Waiting }, // Alone.
        ]);

        var active = await _service.GetActiveAsync(Ct);

        Assert.Equal(2, active.Single(r => r.Id == 11).QueuePosition);
        Assert.Equal(1, active.Single(r => r.Id == 10).QueuePosition);
        Assert.Equal(0, active.Single(r => r.Id == 5).QueuePosition);
        Assert.Equal(1, active.Single(r => r.Id == 20).QueuePosition);
        _reservations.Verify(r => r.ExpireHoldsAsync(LocalNow, LocalNow.AddDays(3), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task CancelAsync_ServesNextMemberWithConfiguredHold()
    {
        _policy.ReservationHoldDays = 1;
        _reservations.Setup(r => r.CancelAsync(8, LocalNow, LocalNow.AddDays(1), It.IsAny<CancellationToken>())).ReturnsAsync(true);

        await _service.CancelAsync(8, Ct);

        _reservations.Verify(r => r.CancelAsync(8, LocalNow, LocalNow.AddDays(1), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task CancelAsync_InactiveReservation_IsRejected()
    {
        _reservations.Setup(r => r.CancelAsync(8, It.IsAny<DateTime>(), It.IsAny<DateTime>(), It.IsAny<CancellationToken>())).ReturnsAsync(false);

        var error = await Assert.ThrowsAsync<BusinessRuleException>(() => _service.CancelAsync(8, Ct));

        Assert.Equal("Seçilen rezervasyon bulunamadı ya da artık aktif değil.", error.Message);
    }
}

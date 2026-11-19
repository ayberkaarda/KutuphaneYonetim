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
    private readonly Mock<ILoanRepository> _loans = new(MockBehavior.Strict);
    private readonly Mock<IReservationRepository> _reservations = new(MockBehavior.Strict);
    private readonly LibraryPolicy _policy = new() { ReservationHoldDays = 3 };
    private readonly ReservationService _service;

    public ReservationServiceTests()
    {
        _service = new ReservationService(
            _books.Object, _members.Object, _loans.Object, _reservations.Object, _policy, new FixedTimeProvider(Now));
        _reservations.Setup(r => r.ExpireHoldsAsync(LocalNow, LocalNow.AddDays(3), It.IsAny<CancellationToken>())).ReturnsAsync(0);
    }

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private void GivenBook(int bookId, bool onShelf, int? borrowerId = null, int? heldForMemberId = null)
    {
        _books.Setup(b => b.GetByIdAsync(bookId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Book { Id = bookId, IsAvailable = onShelf });
        _members.Setup(m => m.GetByIdAsync(It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((int id, CancellationToken _) => new Member { Id = id });
        _loans.Setup(l => l.GetActiveLoanForBookAsync(bookId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(borrowerId is int borrower ? new Loan { Id = 50, BookId = bookId, MemberId = borrower } : null);
        _reservations.Setup(r => r.GetHoldAsync(bookId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(heldForMemberId is int held
                ? new Reservation { Id = 60, BookId = bookId, MemberId = held, Status = ReservationStatus.Ready }
                : null);
    }

    [Fact]
    public async Task ReserveAsync_BookOnLoan_AddsWaitingReservationWithCurrentTime()
    {
        GivenBook(2, onShelf: false, borrowerId: 9);
        _reservations.Setup(r => r.HasActiveAsync(2, 4, It.IsAny<CancellationToken>())).ReturnsAsync(false);
        _reservations.Setup(r => r.AddAsync(2, 4, LocalNow, It.IsAny<CancellationToken>())).ReturnsAsync(1);

        await _service.ReserveAsync(2, 4, Ct);

        _reservations.Verify(r => r.AddAsync(2, 4, LocalNow, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task ReserveAsync_BookHeldForSomeoneElse_JoinsTheQueue()
    {
        GivenBook(2, onShelf: true, heldForMemberId: 9);
        _reservations.Setup(r => r.HasActiveAsync(2, 4, It.IsAny<CancellationToken>())).ReturnsAsync(false);
        _reservations.Setup(r => r.AddAsync(2, 4, LocalNow, It.IsAny<CancellationToken>())).ReturnsAsync(1);

        await _service.ReserveAsync(2, 4, Ct);

        _reservations.Verify(r => r.AddAsync(2, 4, LocalNow, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task ReserveAsync_BookOnTheShelf_IsRejected()
    {
        GivenBook(2, onShelf: true);

        var error = await Assert.ThrowsAsync<BusinessRuleException>(() => _service.ReserveAsync(2, 4, Ct));

        Assert.Equal("Bu kitap rafta. Rezervasyon yerine doğrudan ödünç verilebilir.", error.Message);
        _reservations.Verify(r => r.AddAsync(It.IsAny<int>(), It.IsAny<int>(), It.IsAny<DateTime>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task ReserveAsync_BorrowerReservesOwnBook_IsRejected()
    {
        GivenBook(2, onShelf: false, borrowerId: 4);

        var error = await Assert.ThrowsAsync<BusinessRuleException>(() => _service.ReserveAsync(2, 4, Ct));

        Assert.StartsWith("Bu kitap zaten bu üyede", error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ReserveAsync_SecondActiveReservationOfSameMember_IsRejected()
    {
        GivenBook(2, onShelf: false, borrowerId: 9);
        _reservations.Setup(r => r.HasActiveAsync(2, 4, It.IsAny<CancellationToken>())).ReturnsAsync(true);

        var error = await Assert.ThrowsAsync<BusinessRuleException>(() => _service.ReserveAsync(2, 4, Ct));

        Assert.Equal("Bu üyenin bu kitap için zaten aktif bir rezervasyonu var.", error.Message);
        _reservations.Verify(r => r.AddAsync(It.IsAny<int>(), It.IsAny<int>(), It.IsAny<DateTime>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task ReserveAsync_DuplicateCaughtByUniqueIndex_IsRejected()
    {
        GivenBook(2, onShelf: false, borrowerId: 9);
        _reservations.Setup(r => r.HasActiveAsync(2, 4, It.IsAny<CancellationToken>())).ReturnsAsync(false);
        _reservations.Setup(r => r.AddAsync(2, 4, LocalNow, It.IsAny<CancellationToken>())).ReturnsAsync((int?)null);

        var error = await Assert.ThrowsAsync<BusinessRuleException>(() => _service.ReserveAsync(2, 4, Ct));

        Assert.Equal("Bu üyenin bu kitap için zaten aktif bir rezervasyonu var.", error.Message);
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
    public async Task ReserveAsync_ProcessesExpiredHoldsFirst()
    {
        // The stale hold is expired first, so the book is seen as on the shelf afterwards.
        _reservations.Setup(r => r.ExpireHoldsAsync(LocalNow, LocalNow.AddDays(3), It.IsAny<CancellationToken>())).ReturnsAsync(1);
        GivenBook(2, onShelf: true);

        await Assert.ThrowsAsync<BusinessRuleException>(() => _service.ReserveAsync(2, 4, Ct));

        _reservations.Verify(r => r.ExpireHoldsAsync(LocalNow, LocalNow.AddDays(3), It.IsAny<CancellationToken>()), Times.Once);
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

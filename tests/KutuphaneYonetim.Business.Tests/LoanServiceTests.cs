using KutuphaneYonetim.Business.Services;
using KutuphaneYonetim.Business.Validation;
using KutuphaneYonetim.Domain.Entities;
using KutuphaneYonetim.Domain.Repositories;
using Moq;

namespace KutuphaneYonetim.Business.Tests;

public sealed class LoanServiceTests
{
    private static readonly DateTimeOffset Now = new(2026, 3, 14, 9, 30, 0, TimeSpan.Zero);
    private static readonly DateTime Today = Now.UtcDateTime.Date;

    private readonly Mock<IBookRepository> _books = new(MockBehavior.Strict);
    private readonly Mock<IMemberRepository> _members = new(MockBehavior.Strict);
    private readonly Mock<ILoanRepository> _loans = new(MockBehavior.Strict);
    private readonly Mock<IReservationRepository> _reservations = new(MockBehavior.Strict);
    private readonly LibraryPolicy _policy = new() { LoanPeriodDays = 14, DailyFine = 2.50m, MaxUnpaidFine = 0m, ReservationHoldDays = 3 };
    private readonly LoanService _service;

    public LoanServiceTests()
    {
        _service = new LoanService(
            _books.Object, _members.Object, _loans.Object, _reservations.Object, new LoanInputValidator(), _policy, new FixedTimeProvider(Now));
    }

    private static readonly DateTime HoldEnd = Now.UtcDateTime.AddDays(3);

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private void GivenBookAndMember(int bookId, int memberId, decimal unpaidFine = 0m)
    {
        _books.Setup(b => b.GetByIdAsync(bookId, It.IsAny<CancellationToken>())).ReturnsAsync(new Book { Id = bookId });
        _members.Setup(m => m.GetByIdAsync(memberId, It.IsAny<CancellationToken>())).ReturnsAsync(new Member { Id = memberId });
        _loans.Setup(l => l.GetUnpaidFineTotalAsync(memberId, It.IsAny<CancellationToken>())).ReturnsAsync(unpaidFine);
        _reservations.Setup(r => r.ExpireHoldsAsync(Now.UtcDateTime, HoldEnd, It.IsAny<CancellationToken>())).ReturnsAsync(0);
    }

    private void GivenLendSucceeds(int bookId, int memberId, DateTime dueDate) =>
        _loans.Setup(l => l.LendAsync(bookId, memberId, Now.UtcDateTime, dueDate, It.IsAny<CancellationToken>()))
            .ReturnsAsync(LendOutcome.Lent);

    private void GivenOpenLoan(int bookId, DateTime? dueDate, int loanId = 10) =>
        _loans.Setup(l => l.GetActiveLoanForBookAsync(bookId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Loan { Id = loanId, BookId = bookId, MemberId = 4, LoanDate = Today.AddDays(-20), DueDate = dueDate });

    [Fact]
    public async Task LendAsync_WithoutDueDate_UsesConfiguredLoanPeriod()
    {
        GivenBookAndMember(2, 4);
        GivenLendSucceeds(2, 4, Today.AddDays(14));

        await _service.LendAsync(2, 4, cancellationToken: Ct);

        _loans.Verify(l => l.LendAsync(2, 4, Now.UtcDateTime, Today.AddDays(14), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public void GetDefaultDueDate_IsTodayPlusLoanPeriodWithoutTime()
    {
        _policy.LoanPeriodDays = 21;

        Assert.Equal(Today.AddDays(21), _service.GetDefaultDueDate());
    }

    [Fact]
    public async Task LendAsync_GivenDueDate_IsStoredWithoutTimeOfDay()
    {
        GivenBookAndMember(2, 4);
        GivenLendSucceeds(2, 4, Today.AddDays(3));

        await _service.LendAsync(2, 4, Today.AddDays(3).AddHours(17), Ct);

        _loans.Verify(l => l.LendAsync(2, 4, Now.UtcDateTime, Today.AddDays(3), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task LendAsync_DueDateToday_IsAcceptedAsSameDayLoan()
    {
        GivenBookAndMember(2, 4);
        GivenLendSucceeds(2, 4, Today);

        await _service.LendAsync(2, 4, Today, Ct);

        _loans.Verify(l => l.LendAsync(2, 4, Now.UtcDateTime, Today, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task LendAsync_DueDateInThePast_IsRejectedWithoutTouchingTheDatabase()
    {
        var error = await Assert.ThrowsAsync<BusinessRuleException>(() => _service.LendAsync(2, 4, Today.AddDays(-1), Ct));

        Assert.Equal("Teslim tarihi ödünç tarihinden önce olamaz.", error.Message);
        _books.VerifyNoOtherCalls();
        _loans.VerifyNoOtherCalls();
    }

    [Theory]
    [InlineData(365, true)]
    [InlineData(366, false)]
    public async Task LendAsync_LoanLengthLimit(int days, bool accepted)
    {
        GivenBookAndMember(2, 4);
        GivenLendSucceeds(2, 4, Today.AddDays(days));

        var lend = () => _service.LendAsync(2, 4, Today.AddDays(days), Ct);

        if (accepted)
        {
            await lend();
        }
        else
        {
            var error = await Assert.ThrowsAsync<BusinessRuleException>(lend);
            Assert.Equal("Teslim tarihi en fazla 365 gün sonrası olabilir.", error.Message);
        }
    }

    [Theory]
    [InlineData(0, 0, true)] // No fine, default threshold.
    [InlineData(0, 0.01, false)] // Any unpaid fine blocks with the default threshold.
    [InlineData(5, 5, true)] // Exactly at the threshold: still allowed.
    [InlineData(5, 5.01, false)] // Just above the threshold.
    public async Task LendAsync_UnpaidFineThreshold(decimal threshold, decimal unpaid, bool allowed)
    {
        _policy.MaxUnpaidFine = threshold;
        GivenBookAndMember(2, 4, unpaid);
        GivenLendSucceeds(2, 4, Today.AddDays(14));

        var lend = () => _service.LendAsync(2, 4, cancellationToken: Ct);

        if (allowed)
        {
            await lend();
            _loans.Verify(l => l.LendAsync(2, 4, It.IsAny<DateTime>(), It.IsAny<DateTime>(), It.IsAny<CancellationToken>()), Times.Once);
        }
        else
        {
            var error = await Assert.ThrowsAsync<BusinessRuleException>(lend);
            Assert.StartsWith("Bu üyenin ödenmemiş cezası", error.Message, StringComparison.Ordinal);
            _loans.Verify(l => l.LendAsync(It.IsAny<int>(), It.IsAny<int>(), It.IsAny<DateTime>(), It.IsAny<DateTime>(), It.IsAny<CancellationToken>()), Times.Never);
        }
    }

    [Fact]
    public async Task LendAsync_BookAlreadyOnLoan_IsRejected()
    {
        GivenBookAndMember(2, 4);
        _loans.Setup(l => l.LendAsync(2, 4, It.IsAny<DateTime>(), It.IsAny<DateTime>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(LendOutcome.BookOnLoan);

        var error = await Assert.ThrowsAsync<BusinessRuleException>(() => _service.LendAsync(2, 4, cancellationToken: Ct));

        Assert.Equal("Bu kitap şu anda başka bir üyede. Aynı kitap iki üyeye ödünç verilemez.", error.Message);
    }

    [Fact]
    public async Task LendAsync_BookDeletedMeanwhile_IsRejectedAsNotFound()
    {
        GivenBookAndMember(2, 4);
        _loans.Setup(l => l.LendAsync(2, 4, It.IsAny<DateTime>(), It.IsAny<DateTime>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(LendOutcome.BookNotFound);

        var error = await Assert.ThrowsAsync<BusinessRuleException>(() => _service.LendAsync(2, 4, cancellationToken: Ct));

        Assert.Equal("Seçilen kitap bulunamadı.", error.Message);
    }

    [Fact]
    public async Task LendAsync_UnknownBook_IsRejected()
    {
        _books.Setup(b => b.GetByIdAsync(2, It.IsAny<CancellationToken>())).ReturnsAsync((Book?)null);

        var error = await Assert.ThrowsAsync<BusinessRuleException>(() => _service.LendAsync(2, 4, cancellationToken: Ct));

        Assert.Equal("Seçilen kitap bulunamadı.", error.Message);
        _loans.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task LendAsync_UnknownMember_IsRejected()
    {
        _books.Setup(b => b.GetByIdAsync(2, It.IsAny<CancellationToken>())).ReturnsAsync(new Book { Id = 2 });
        _members.Setup(m => m.GetByIdAsync(4, It.IsAny<CancellationToken>())).ReturnsAsync((Member?)null);

        var error = await Assert.ThrowsAsync<BusinessRuleException>(() => _service.LendAsync(2, 4, cancellationToken: Ct));

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
            () => _service.LendAsync(bookId, memberId, cancellationToken: Ct));

        Assert.Equal(expectedMessage, error.Message);
        _loans.VerifyNoOtherCalls();
    }

    [Theory]
    [InlineData(0, 0)]
    [InlineData(-5, -7)]
    public async Task LendAsync_ExistingLegacyRecordsWithNonPositiveIds_CanBeLent(int bookId, int memberId)
    {
        GivenBookAndMember(bookId, memberId);
        GivenLendSucceeds(bookId, memberId, Today.AddDays(14));

        await _service.LendAsync(bookId, memberId, cancellationToken: Ct);

        _loans.Verify(l => l.LendAsync(bookId, memberId, Now.UtcDateTime, Today.AddDays(14), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Theory]
    [InlineData(0, 0, null)] // Returned on the due date.
    [InlineData(-3, 0, null)] // Returned early.
    [InlineData(1, 1, 2.50)] // One day late.
    [InlineData(10, 10, 25.00)]
    public async Task ReturnAsync_StoresFineForDaysAfterDueDate(int daysLate, int expectedDays, double? expectedFine)
    {
        var fine = (decimal?)expectedFine;
        GivenOpenLoan(2, Today.AddDays(-daysLate));
        _loans.Setup(l => l.ReturnAsync(10, Now.UtcDateTime, fine, HoldEnd, It.IsAny<CancellationToken>())).ReturnsAsync(new ReturnOutcome(true));

        var receipt = await _service.ReturnAsync(2, Ct);

        Assert.Equal(expectedDays, receipt.OverdueDays);
        Assert.Equal(fine ?? 0m, receipt.Fine);
        _loans.Verify(l => l.ReturnAsync(10, Now.UtcDateTime, fine, HoldEnd, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task ReturnAsync_DailyFineIsConfigurable()
    {
        _policy.DailyFine = 1.75m;
        GivenOpenLoan(2, Today.AddDays(-3));
        _loans.Setup(l => l.ReturnAsync(10, Now.UtcDateTime, 5.25m, HoldEnd, It.IsAny<CancellationToken>())).ReturnsAsync(new ReturnOutcome(true));

        var receipt = await _service.ReturnAsync(2, Ct);

        Assert.Equal(5.25m, receipt.Fine);
    }

    [Fact]
    public async Task ReturnAsync_ZeroDailyFine_RecordsNoFine()
    {
        _policy.DailyFine = 0m;
        GivenOpenLoan(2, Today.AddDays(-30));
        _loans.Setup(l => l.ReturnAsync(10, Now.UtcDateTime, null, HoldEnd, It.IsAny<CancellationToken>())).ReturnsAsync(new ReturnOutcome(true));

        var receipt = await _service.ReturnAsync(2, Ct);

        Assert.Equal(30, receipt.OverdueDays);
        Assert.Equal(0m, receipt.Fine);
    }

    [Fact]
    public async Task ReturnAsync_LegacyLoanWithoutDueDate_HasNoFine()
    {
        GivenOpenLoan(2, dueDate: null);
        _loans.Setup(l => l.ReturnAsync(10, Now.UtcDateTime, null, HoldEnd, It.IsAny<CancellationToken>())).ReturnsAsync(new ReturnOutcome(true));

        var receipt = await _service.ReturnAsync(2, Ct);

        Assert.Equal(new ReturnReceipt(0, 0m), receipt);
    }

    [Fact]
    public async Task ReturnAsync_NoOpenLoan_IsRejected()
    {
        _loans.Setup(l => l.GetActiveLoanForBookAsync(2, It.IsAny<CancellationToken>())).ReturnsAsync((Loan?)null);

        var error = await Assert.ThrowsAsync<BusinessRuleException>(() => _service.ReturnAsync(2, Ct));

        Assert.Equal("Bu kitap için açık bir ödünç kaydı bulunamadı.", error.Message);
    }

    [Fact]
    public async Task ReturnAsync_ReturnedConcurrently_IsRejected()
    {
        GivenOpenLoan(2, Today);
        _loans.Setup(l => l.ReturnAsync(10, It.IsAny<DateTime>(), It.IsAny<decimal?>(), It.IsAny<DateTime>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(ReturnOutcome.NotOpen);

        var error = await Assert.ThrowsAsync<BusinessRuleException>(() => _service.ReturnAsync(2, Ct));

        Assert.Equal("Bu kitap için açık bir ödünç kaydı bulunamadı.", error.Message);
    }

    [Fact]
    public async Task LendAsync_BookHeldForAnotherMember_IsRejected()
    {
        GivenBookAndMember(2, 4);
        _loans.Setup(l => l.LendAsync(2, 4, It.IsAny<DateTime>(), It.IsAny<DateTime>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(LendOutcome.HeldForAnotherMember);

        var error = await Assert.ThrowsAsync<BusinessRuleException>(() => _service.LendAsync(2, 4, cancellationToken: Ct));

        Assert.StartsWith("Bu kitap rezervasyon sırasındaki başka bir üye için ayrıldı.", error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task LendAsync_ExpiresStaleHoldsBeforeLending()
    {
        var sequence = new MockSequence();
        GivenBookAndMember(2, 4);
        _reservations.InSequence(sequence)
            .Setup(r => r.ExpireHoldsAsync(Now.UtcDateTime, HoldEnd, It.IsAny<CancellationToken>())).ReturnsAsync(1);
        _loans.InSequence(sequence)
            .Setup(l => l.LendAsync(2, 4, Now.UtcDateTime, Today.AddDays(14), It.IsAny<CancellationToken>())).ReturnsAsync(LendOutcome.Lent);

        await _service.LendAsync(2, 4, cancellationToken: Ct);

        _reservations.Verify(r => r.ExpireHoldsAsync(Now.UtcDateTime, HoldEnd, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task ReturnAsync_WithWaitingMember_ReportsWhoTheBookIsHeldForAndUntilWhen()
    {
        _policy.ReservationHoldDays = 5;
        GivenOpenLoan(2, Today);
        _loans.Setup(l => l.ReturnAsync(10, Now.UtcDateTime, null, Now.UtcDateTime.AddDays(5), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ReturnOutcome(true, 7));
        _members.Setup(m => m.GetByIdAsync(7, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Member { Id = 7, FirstName = "Ayşe", LastName = "Kaya" });

        var receipt = await _service.ReturnAsync(2, Ct);

        Assert.Equal(new ReturnReceipt(0, 0m, "Ayşe Kaya", Now.UtcDateTime.AddDays(5)), receipt);
    }

    [Fact]
    public async Task GetAllAsync_AddsOverdueDaysAndEstimatedFineForOpenLoans()
    {
        _loans.Setup(l => l.GetAllDetailsAsync(It.IsAny<CancellationToken>())).ReturnsAsync(
        [
            new LoanDetail { Id = 1, DueDate = Today.AddDays(-4) }, // Open, 4 days late.
            new LoanDetail { Id = 2, DueDate = Today.AddDays(2) }, // Open, not due yet.
            new LoanDetail { Id = 3, DueDate = Today.AddDays(-10), ReturnDate = Today.AddDays(-7), FineAmount = 7.50m },
            new LoanDetail { Id = 4, DueDate = null }, // Legacy open loan.
        ]);

        var all = await _service.GetAllAsync(Ct);

        Assert.Equal([4, 0, 3, 0], all.Select(l => l.OverdueDays));
        Assert.Equal([10.00m, 0m, null, 0m], all.Select(l => l.EstimatedFine));
    }
}

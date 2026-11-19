using KutuphaneYonetim.Business.Reports;
using KutuphaneYonetim.Domain.Entities;
using KutuphaneYonetim.Domain.Repositories;
using Moq;

namespace KutuphaneYonetim.Business.Tests;

public sealed class ReportServiceTests
{
    private static readonly DateTimeOffset Now = new(2026, 3, 14, 9, 30, 0, TimeSpan.Zero);
    private static readonly DateTime Today = Now.UtcDateTime.Date;

    private readonly Mock<IReportRepository> _reports = new(MockBehavior.Strict);
    private readonly ReportService _service;

    public ReportServiceTests()
    {
        _service = new ReportService(_reports.Object, new LibraryPolicy { DailyFine = 2.50m }, new FixedTimeProvider(Now));
    }

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task GetOverdueLoansAsync_AsksForTodayAndAddsDaysAndFine()
    {
        _reports.Setup(r => r.GetOverdueLoansAsync(Today, It.IsAny<CancellationToken>())).ReturnsAsync(
        [
            new LoanDetail { Id = 1, DueDate = Today.AddDays(-1) },
            new LoanDetail { Id = 2, DueDate = Today.AddDays(-12) },
        ]);

        var overdue = await _service.GetOverdueLoansAsync(Ct);

        Assert.Equal([1, 12], overdue.Select(l => l.OverdueDays));
        Assert.Equal([2.50m, 30.00m], overdue.Select(l => l.EstimatedFine));
    }

    [Fact]
    public async Task GetMostBorrowedAsync_IncludesTheWholeEndDay()
    {
        _reports.Setup(r => r.GetMostBorrowedAsync(Today.AddDays(-7), Today.AddDays(1), 5, It.IsAny<CancellationToken>()))
            .ReturnsAsync([]);

        await _service.GetMostBorrowedAsync(Today.AddDays(-7).AddHours(15), Today.AddHours(8), 5, Ct);

        _reports.Verify(r => r.GetMostBorrowedAsync(Today.AddDays(-7), Today.AddDays(1), 5, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task GetMostBorrowedAsync_SameDayRange_IsOneDay()
    {
        _reports.Setup(r => r.GetMostBorrowedAsync(Today, Today.AddDays(1), 10, It.IsAny<CancellationToken>())).ReturnsAsync([]);

        await _service.GetMostBorrowedAsync(Today, Today, cancellationToken: Ct);

        _reports.Verify(r => r.GetMostBorrowedAsync(Today, Today.AddDays(1), 10, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task GetMostBorrowedAsync_StartAfterEnd_IsRejected()
    {
        var error = await Assert.ThrowsAsync<BusinessRuleException>(
            () => _service.GetMostBorrowedAsync(Today.AddDays(1), Today, cancellationToken: Ct));

        Assert.Equal("Başlangıç tarihi bitiş tarihinden sonra olamaz.", error.Message);
        _reports.VerifyNoOtherCalls();
    }

    [Theory]
    [InlineData(0)]
    [InlineData(101)]
    public async Task GetMostBorrowedAsync_TopOutOfRange_IsRejected(int top)
    {
        await Assert.ThrowsAsync<BusinessRuleException>(() => _service.GetMostBorrowedAsync(Today, Today, top, Ct));

        _reports.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task GetUnpaidFinesAsync_AddsDaysLateAtReturn()
    {
        _reports.Setup(r => r.GetUnpaidFinesAsync(It.IsAny<CancellationToken>())).ReturnsAsync(
        [
            new LoanDetail { Id = 4, DueDate = Today.AddDays(-10), ReturnDate = Today.AddDays(-7).AddHours(5), FineAmount = 7.50m },
        ]);

        var fines = await _service.GetUnpaidFinesAsync(Ct);

        Assert.Equal(3, Assert.Single(fines).OverdueDays);
    }

    [Fact]
    public void ReportTables_FormatWithTurkishConventions()
    {
        var overdue = ReportTables.OverdueLoans(
        [
            new LoanDetail
            {
                Id = 7,
                BookTitle = "Simyacı",
                MemberName = "Deneme Kullanıcı",
                LoanDate = new DateTime(2026, 2, 1, 14, 5, 0, DateTimeKind.Unspecified),
                DueDate = new DateTime(2026, 2, 15, 0, 0, 0, DateTimeKind.Unspecified),
                OverdueDays = 27,
                EstimatedFine = 1234.5m,
            },
        ]);

        Assert.Equal(7, overdue.Headers.Count);
        Assert.Equal(["7", "Simyacı", "Deneme Kullanıcı", "01.02.2026 14:05", "15.02.2026", "27", "1234,50"], Assert.Single(overdue.Rows));

        var top = ReportTables.MostBorrowed([new BookLoanCount { BookId = 3, Title = "1984", Author = "George Orwell", LoanCount = 4 }]);
        Assert.Equal(["1", "3", "1984", "George Orwell", "4"], Assert.Single(top.Rows));

        var fines = ReportTables.UnpaidFines([new LoanDetail { Id = 2, DueDate = null, ReturnDate = null, FineAmount = null }]);
        Assert.Equal(["2", "", "", "", "", "0", ""], Assert.Single(fines.Rows));
    }
}

using KutuphaneYonetim.Business;
using KutuphaneYonetim.Business.Services;

namespace KutuphaneYonetim.Data.Tests;

/// <summary>Due dates, late fines and the unpaid fine threshold end to end on SQLite in-memory.</summary>
public sealed class FineIntegrationTests : IAsyncLifetime
{
    private static readonly DateTime Start = new(2026, 6, 1, 10, 0, 0, DateTimeKind.Unspecified);

    private readonly ManualClock _clock = new(Start);
    private readonly SqliteDatabase _db;

    public FineIntegrationTests()
    {
        _db = new SqliteDatabase(_clock, new LibraryPolicy { LoanPeriodDays = 7, DailyFine = 1.50m, MaxUnpaidFine = 3m });
    }

    public ValueTask InitializeAsync() => _db.InitializeAsync();

    public ValueTask DisposeAsync() => _db.DisposeAsync();

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private ILoanService Loans => _db.Get<ILoanService>();

    private IMemberService Members => _db.Get<IMemberService>();

    [Fact]
    public async Task LateReturn_RecordsFine_AndBlocksBorrowingAboveThresholdUntilPaid()
    {
        await Loans.LendAsync(4, 3, cancellationToken: Ct); // Due 2026-06-08.
        Assert.Equal(Start.Date.AddDays(7), (await Loans.GetAllAsync(Ct)).Single(l => l.BookId == 4 && l.IsActive).DueDate);

        _clock.AdvanceDays(9); // Two days late.
        var open = (await Loans.GetAllAsync(Ct)).Single(l => l.BookId == 4 && l.IsActive);
        Assert.Equal(2, open.OverdueDays);
        Assert.Equal(3.00m, open.EstimatedFine);

        var receipt = await Loans.ReturnAsync(4, Ct);
        Assert.Equal(new ReturnReceipt(2, 3.00m), receipt);

        // 3.00 equals the threshold: the member may still borrow.
        await Loans.LendAsync(5, 3, _clock.Now.Date, Ct); // Due today.
        _clock.AdvanceDays(1);
        Assert.Equal(new ReturnReceipt(1, 1.50m), await Loans.ReturnAsync(5, Ct));

        // 4.50 is above the threshold.
        Assert.Equal(4.50m, (await Members.GetSummariesAsync(Ct)).Single(m => m.Id == 3).UnpaidFine);
        var refused = await Assert.ThrowsAsync<BusinessRuleException>(() => Loans.LendAsync(6, 3, cancellationToken: Ct));
        Assert.StartsWith("Bu üyenin ödenmemiş cezası", refused.Message, StringComparison.Ordinal);

        Assert.Equal(4.50m, await Members.PayFinesAsync(3, Ct));
        Assert.Equal(0m, (await Members.GetSummariesAsync(Ct)).Single(m => m.Id == 3).UnpaidFine);
        await Loans.LendAsync(6, 3, cancellationToken: Ct);
    }

    [Fact]
    public async Task SameDayReturn_OfSameDayLoan_HasNoFine()
    {
        await Loans.LendAsync(4, 1, Start.Date, Ct);
        _clock.Now = Start.Date.AddHours(23).AddMinutes(59);

        Assert.Equal(new ReturnReceipt(0, 0m), await Loans.ReturnAsync(4, Ct));
        Assert.Null((await Loans.GetAllAsync(Ct)).Single(l => l.BookId == 4).FineAmount);
    }

    [Fact]
    public async Task SeededLegacyLoan_WithoutDueDate_IsReturnedWithoutFine()
    {
        _clock.AdvanceDays(400);

        Assert.Equal(new ReturnReceipt(0, 0m), await Loans.ReturnAsync(3, Ct));
    }
}

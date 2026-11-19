using System.Text;
using KutuphaneYonetim.Business;
using KutuphaneYonetim.Business.Reports;
using KutuphaneYonetim.Business.Services;

namespace KutuphaneYonetim.Data.Tests;

/// <summary>Reports and CSV export end to end on SQLite in-memory.</summary>
public sealed class ReportTests : IAsyncLifetime
{
    private static readonly DateTime Start = new(2026, 6, 1, 10, 0, 0, DateTimeKind.Unspecified);

    private readonly ManualClock _clock = new(Start);
    private readonly SqliteDatabase _db;

    public ReportTests()
    {
        _db = new SqliteDatabase(_clock, new LibraryPolicy { DailyFine = 2m, MaxUnpaidFine = 1000m });
    }

    public ValueTask InitializeAsync() => _db.InitializeAsync();

    public ValueTask DisposeAsync() => _db.DisposeAsync();

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private ILoanService Loans => _db.Get<ILoanService>();

    private IReportService Reports => _db.Get<IReportService>();

    [Fact]
    public async Task Overdue_ListsOpenLoansPastDueDate_DueTodayIsNotOverdue()
    {
        await Loans.LendAsync(4, 1, Start.Date.AddDays(1), Ct); // Due 2026-06-02.
        await Loans.LendAsync(5, 3, Start.Date.AddDays(3), Ct); // Due 2026-06-04.
        await Loans.LendAsync(6, 1, Start.Date.AddDays(1), Ct);
        _clock.AdvanceDays(1);
        await Loans.ReturnAsync(6, Ct); // Returned on time: not in the report.

        _clock.Now = Start.Date.AddDays(4).AddHours(9); // 2026-06-05.
        var overdue = await Reports.GetOverdueLoansAsync(Ct);

        Assert.Equal([4, 5], overdue.Select(l => l.BookId)); // Most overdue first; seeded loan 2 has no due date.
        Assert.Equal([3, 1], overdue.Select(l => l.OverdueDays));
        Assert.Equal([6m, 2m], overdue.Select(l => l.EstimatedFine));

        _clock.Now = Start.Date.AddDays(3).AddHours(23); // 2026-06-04: book 5 is due today.
        Assert.Equal([4], (await Reports.GetOverdueLoansAsync(Ct)).Select(l => l.BookId));
    }

    [Fact]
    public async Task MostBorrowed_CountsLoansInsideRange_BothEndsIncluded()
    {
        // Book 4 three times, book 5 once (before the range), book 1 twice; the seeded loans are from 2025.
        foreach (var (book, day) in new[] { (4, 0), (1, 1), (4, 2), (1, 3), (4, 5) })
        {
            _clock.Now = Start.AddDays(day);
            await Loans.LendAsync(book, 3, cancellationToken: Ct);
            await Loans.ReturnAsync(book, Ct);
        }

        _clock.Now = Start.AddDays(-3);
        await Loans.LendAsync(5, 3, cancellationToken: Ct);
        await Loans.ReturnAsync(5, Ct);

        var all = await Reports.GetMostBorrowedAsync(Start.Date, Start.Date.AddDays(5), cancellationToken: Ct);
        Assert.Equal([(4, 3), (1, 2)], all.Select(b => (b.BookId, b.LoanCount)));

        // The last day counts in full; the first day of a later range does not include earlier loans.
        Assert.Equal([(4, 1)], (await Reports.GetMostBorrowedAsync(Start.Date.AddDays(5), Start.Date.AddDays(5), cancellationToken: Ct)).Select(b => (b.BookId, b.LoanCount)));
        Assert.Equal([4], (await Reports.GetMostBorrowedAsync(Start.Date, Start.Date.AddDays(5), top: 1, cancellationToken: Ct)).Select(b => b.BookId));
        Assert.Empty(await Reports.GetMostBorrowedAsync(Start.Date.AddDays(10), Start.Date.AddDays(20), cancellationToken: Ct));
    }

    [Fact]
    public async Task MostBorrowed_TiesAreOrderedByTitle()
    {
        await Loans.LendAsync(6, 3, cancellationToken: Ct); // Sefiller
        await Loans.LendAsync(1, 3, cancellationToken: Ct); // 1984

        var top = await Reports.GetMostBorrowedAsync(Start.Date, Start.Date, cancellationToken: Ct);

        Assert.Equal(["1984", "Sefiller"], top.Select(b => b.Title));
    }

    [Fact]
    public async Task UnpaidFines_ListsOnlyUnpaid_AndExportsAsShown()
    {
        await Loans.LendAsync(4, 1, Start.Date, Ct);
        await Loans.LendAsync(5, 3, Start.Date, Ct);
        _clock.AdvanceDays(2);
        await Loans.ReturnAsync(4, Ct); // 2 days: 4 TL for member 1.
        _clock.AdvanceDays(1);
        await Loans.ReturnAsync(5, Ct); // 3 days: 6 TL for member 3.
        await _db.Get<IMemberService>().PayFinesAsync(3, Ct);

        var fines = await Reports.GetUnpaidFinesAsync(Ct);
        var row = Assert.Single(fines);
        Assert.Equal((4, 2, 4m), (row.BookId, row.OverdueDays, row.FineAmount));

        Assert.Equal(
            "\uFEFFİşlem No;Üye;Kitap;Teslim Tarihi;İade Tarihi;Gecikme (Gün);Ceza (TL)\r\n"
            + $"{row.Id};Örnek Üye;Kürk Mantolu Madonna;01.06.2026;03.06.2026 10:00;2;4,00\r\n",
            await ExportAsync(ReportTables.UnpaidFines(fines)));
    }

    [Fact]
    public async Task Export_UserEnteredFormula_IsNeutralised()
    {
        var book = await _db.Get<IBookService>().AddAsync("=HYPERLINK(\"http://x\";\"tıkla\")", "@yazar", cancellationToken: Ct);
        await Loans.LendAsync(book.Id, 1, cancellationToken: Ct);

        var table = ReportTables.MostBorrowed(await Reports.GetMostBorrowedAsync(Start.Date, Start.Date, cancellationToken: Ct));
        var csv = await ExportAsync(table);

        Assert.Contains("\"'=HYPERLINK(\"\"http://x\"\";\"\"tıkla\"\")\";'@yazar;", csv, StringComparison.Ordinal);
    }

    /// <summary>Exports through the registered exporter and returns the file content, BOM included.</summary>
    private async Task<string> ExportAsync(ReportTable table)
    {
        var path = Path.Combine(Path.GetTempPath(), "kutuphane-report-" + Guid.NewGuid().ToString("N") + ".csv");
        try
        {
            await _db.Get<ICsvExporter>().ExportAsync(table, path, Ct);
            return Encoding.UTF8.GetString(await File.ReadAllBytesAsync(path, Ct));
        }
        finally
        {
            File.Delete(path);
        }
    }
}

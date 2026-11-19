using KutuphaneYonetim.Business.Services;
using KutuphaneYonetim.Domain.Entities;
using KutuphaneYonetim.Domain.Repositories;

namespace KutuphaneYonetim.Business.Reports;

public interface IReportService
{
    /// <summary>Open loans past their due date (a book due today is not overdue yet), with days and estimated fine.</summary>
    Task<IReadOnlyList<LoanDetail>> GetOverdueLoansAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// The <paramref name="top"/> books lent most often between <paramref name="firstDay"/> and
    /// <paramref name="lastDay"/>, both days included.
    /// </summary>
    Task<IReadOnlyList<BookLoanCount>> GetMostBorrowedAsync(
        DateTime firstDay, DateTime lastDay, int top = ReportService.DefaultTop, CancellationToken cancellationToken = default);

    /// <summary>Fines that are not paid yet, with the days the book was late.</summary>
    Task<IReadOnlyList<LoanDetail>> GetUnpaidFinesAsync(CancellationToken cancellationToken = default);
}

internal sealed class ReportService(
    IReportRepository reports,
    LibraryPolicy policy,
    TimeProvider timeProvider) : IReportService
{
    public const int DefaultTop = 10;
    public const int MaxTop = 100;

    public async Task<IReadOnlyList<LoanDetail>> GetOverdueLoansAsync(CancellationToken cancellationToken = default)
    {
        var now = timeProvider.GetLocalNow().DateTime;
        var overdue = await reports.GetOverdueLoansAsync(now.Date, cancellationToken).ConfigureAwait(false);
        foreach (var loan in overdue)
        {
            loan.OverdueDays = FineCalculator.OverdueDays(loan.DueDate, now);
            loan.EstimatedFine = FineCalculator.Fine(loan.OverdueDays, policy.DailyFine);
        }

        return overdue;
    }

    public Task<IReadOnlyList<BookLoanCount>> GetMostBorrowedAsync(
        DateTime firstDay, DateTime lastDay, int top = DefaultTop, CancellationToken cancellationToken = default)
    {
        if (firstDay.Date > lastDay.Date)
        {
            throw new BusinessRuleException("Başlangıç tarihi bitiş tarihinden sonra olamaz.");
        }

        if (top is < 1 or > MaxTop)
        {
            throw new BusinessRuleException("Listelenecek kitap sayısı 1 ile 100 arasında olmalıdır.");
        }

        return reports.GetMostBorrowedAsync(firstDay.Date, lastDay.Date.AddDays(1), top, cancellationToken);
    }

    public async Task<IReadOnlyList<LoanDetail>> GetUnpaidFinesAsync(CancellationToken cancellationToken = default)
    {
        var fines = await reports.GetUnpaidFinesAsync(cancellationToken).ConfigureAwait(false);
        foreach (var loan in fines)
        {
            loan.OverdueDays = FineCalculator.OverdueDays(loan.DueDate, loan.ReturnDate ?? loan.LoanDate);
        }

        return fines;
    }
}

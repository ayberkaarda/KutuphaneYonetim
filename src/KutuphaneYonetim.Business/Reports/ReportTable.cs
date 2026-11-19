using System.Globalization;
using KutuphaneYonetim.Domain.Entities;

namespace KutuphaneYonetim.Business.Reports;

/// <summary>A report as text cells: what the screen shows and what is exported to CSV.</summary>
public sealed record ReportTable(IReadOnlyList<string> Headers, IReadOnlyList<IReadOnlyList<string>> Rows);

/// <summary>Builds the report tables with Turkish headers and Turkish number and date formats.</summary>
public static class ReportTables
{
    private static readonly CultureInfo Turkish = CultureInfo.GetCultureInfo("tr-TR");

    public static ReportTable OverdueLoans(IEnumerable<LoanDetail> loans)
    {
        ArgumentNullException.ThrowIfNull(loans);

        return new ReportTable(
            ["İşlem No", "Kitap", "Üye", "Ödünç Tarihi", "Teslim Tarihi", "Gecikme (Gün)", "Biriken Ceza (TL)"],
            loans.Select(l => Row(
                Number(l.Id),
                l.BookTitle,
                l.MemberName,
                DateTimeText(l.LoanDate),
                DateText(l.DueDate),
                Number(l.OverdueDays),
                Money(l.EstimatedFine))).ToList());
    }

    public static ReportTable MostBorrowed(IEnumerable<BookLoanCount> books)
    {
        ArgumentNullException.ThrowIfNull(books);

        return new ReportTable(
            ["Sıra", "Kitap No", "Kitap", "Yazar", "Ödünç Sayısı"],
            books.Select((b, index) => Row(
                Number(index + 1),
                Number(b.BookId),
                b.Title,
                b.Author,
                Number(b.LoanCount))).ToList());
    }

    public static ReportTable UnpaidFines(IEnumerable<LoanDetail> loans)
    {
        ArgumentNullException.ThrowIfNull(loans);

        return new ReportTable(
            ["İşlem No", "Üye", "Kitap", "Teslim Tarihi", "İade Tarihi", "Gecikme (Gün)", "Ceza (TL)"],
            loans.Select(l => Row(
                Number(l.Id),
                l.MemberName,
                l.BookTitle,
                DateText(l.DueDate),
                DateTimeText(l.ReturnDate),
                Number(l.OverdueDays),
                Money(l.FineAmount))).ToList());
    }

    private static string[] Row(params string[] cells) => cells;

    private static string Number(int value) => value.ToString(Turkish);

    private static string Money(decimal? value) => value is { } amount ? amount.ToString("0.00", Turkish) : string.Empty;

    private static string DateText(DateTime? value) => value?.ToString("dd.MM.yyyy", Turkish) ?? string.Empty;

    private static string DateTimeText(DateTime? value) => value?.ToString("dd.MM.yyyy HH:mm", Turkish) ?? string.Empty;
}

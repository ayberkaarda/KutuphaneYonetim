namespace KutuphaneYonetim.Business.Services;

/// <summary>Late return arithmetic. Only calendar days count; the time of day is ignored.</summary>
internal static class FineCalculator
{
    /// <summary>
    /// Whole days between the due date and <paramref name="at"/>; 0 when the book is returned on or before
    /// the due date, or when the loan has no due date (loans recorded before due dates existed).
    /// </summary>
    public static int OverdueDays(DateTime? dueDate, DateTime at) =>
        dueDate is { } due ? Math.Max(0, (at.Date - due.Date).Days) : 0;

    /// <summary>Fine for <paramref name="overdueDays"/> days, rounded to kuruş.</summary>
    public static decimal Fine(int overdueDays, decimal dailyFine) =>
        overdueDays <= 0 ? 0m : decimal.Round(overdueDays * dailyFine, 2, MidpointRounding.AwayFromZero);
}

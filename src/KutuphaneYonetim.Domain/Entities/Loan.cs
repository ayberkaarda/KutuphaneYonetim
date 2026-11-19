namespace KutuphaneYonetim.Domain.Entities;

/// <summary>A loan of a book to a member (table: OduncIslemleri).</summary>
public sealed class Loan
{
    public int Id { get; set; }

    public int BookId { get; set; }

    public int MemberId { get; set; }

    public DateTime LoanDate { get; set; }

    /// <summary>
    /// Date (without time) by which the book has to be returned. Null for loans recorded before due
    /// dates existed; such loans are never overdue.
    /// </summary>
    public DateTime? DueDate { get; set; }

    /// <summary>Null while the book has not been returned yet.</summary>
    public DateTime? ReturnDate { get; set; }

    /// <summary>Late return fine fixed when the book was returned; null when there was no fine.</summary>
    public decimal? FineAmount { get; set; }

    /// <summary>When the fine was paid; null while it is still outstanding (or there is no fine).</summary>
    public DateTime? FinePaidDate { get; set; }

    public bool IsActive => ReturnDate is null;

    public bool HasUnpaidFine => FineAmount > 0 && FinePaidDate is null;
}

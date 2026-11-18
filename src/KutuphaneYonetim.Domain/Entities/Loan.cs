namespace KutuphaneYonetim.Domain.Entities;

/// <summary>A loan of a book to a member (table: OduncIslemleri).</summary>
public sealed class Loan
{
    public int Id { get; set; }

    public int BookId { get; set; }

    public int MemberId { get; set; }

    public DateTime LoanDate { get; set; }

    /// <summary>Null while the book has not been returned yet.</summary>
    public DateTime? ReturnDate { get; set; }

    public bool IsActive => ReturnDate is null;
}

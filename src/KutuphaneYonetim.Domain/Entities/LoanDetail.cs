namespace KutuphaneYonetim.Domain.Entities;

/// <summary>Read model for listing loans together with book and member names.</summary>
public sealed class LoanDetail
{
    public int Id { get; set; }

    public int BookId { get; set; }

    public string BookTitle { get; set; } = string.Empty;

    public int MemberId { get; set; }

    public string MemberName { get; set; } = string.Empty;

    public DateTime LoanDate { get; set; }

    public DateTime? DueDate { get; set; }

    public DateTime? ReturnDate { get; set; }

    /// <summary>Fine recorded at return time (see <see cref="Loan.FineAmount"/>).</summary>
    public decimal? FineAmount { get; set; }

    public DateTime? FinePaidDate { get; set; }

    /// <summary>Days past the due date: up to the return date, or up to now while the loan is open.</summary>
    public int OverdueDays { get; set; }

    /// <summary>Fine the member would owe if an open loan were returned now; null for returned loans.</summary>
    public decimal? EstimatedFine { get; set; }

    public bool IsActive => ReturnDate is null;
}

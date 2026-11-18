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

    public DateTime? ReturnDate { get; set; }

    public bool IsActive => ReturnDate is null;
}

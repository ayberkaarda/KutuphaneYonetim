namespace KutuphaneYonetim.Domain.Entities;

/// <summary>Read model for the most borrowed books report.</summary>
public sealed class BookLoanCount
{
    public int BookId { get; set; }

    public string Title { get; set; } = string.Empty;

    public string Author { get; set; } = string.Empty;

    public int LoanCount { get; set; }
}

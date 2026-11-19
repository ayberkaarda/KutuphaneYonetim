namespace KutuphaneYonetim.Domain.Entities;

/// <summary>Read model for the member list: the member with open loan count and unpaid fines.</summary>
public sealed class MemberSummary
{
    public int Id { get; set; }

    public string FirstName { get; set; } = string.Empty;

    public string LastName { get; set; } = string.Empty;

    public string FullName => $"{FirstName} {LastName}".Trim();

    public int ActiveLoanCount { get; set; }

    public decimal UnpaidFine { get; set; }
}

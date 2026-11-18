namespace KutuphaneYonetim.Domain.Entities;

/// <summary>A library member (table: Uyeler).</summary>
public sealed class Member
{
    public int Id { get; set; }

    public string FirstName { get; set; } = string.Empty;

    public string LastName { get; set; } = string.Empty;

    public string FullName => $"{FirstName} {LastName}".Trim();
}

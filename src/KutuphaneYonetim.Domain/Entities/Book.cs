namespace KutuphaneYonetim.Domain.Entities;

/// <summary>A book in the library catalogue (table: Kitaplar).</summary>
public sealed class Book
{
    public int Id { get; set; }

    public string Title { get; set; } = string.Empty;

    public string Author { get; set; } = string.Empty;

    /// <summary>True when the book is on the shelf; false while it is on loan.</summary>
    public bool IsAvailable { get; set; } = true;
}

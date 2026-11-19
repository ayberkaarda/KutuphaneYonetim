namespace KutuphaneYonetim.Domain.Entities;

/// <summary>A book in the library catalogue (table: Kitaplar).</summary>
public sealed class Book
{
    public int Id { get; set; }

    public string Title { get; set; } = string.Empty;

    public string Author { get; set; } = string.Empty;

    /// <summary>ISBN-10 or ISBN-13 without separators; null when unknown (books added before ISBNs existed).</summary>
    public string? Isbn { get; set; }

    /// <summary>True when the book is on the shelf; false while it is on loan.</summary>
    public bool IsAvailable { get; set; } = true;
}

namespace KutuphaneYonetim.Business.Validation;

/// <summary>Book data entered by the user. Text values are trimmed on creation.</summary>
internal sealed record BookInput(string Title, string Author)
{
    public static BookInput Create(string? title, string? author) =>
        new(title?.Trim() ?? string.Empty, author?.Trim() ?? string.Empty);
}

/// <summary>Member data entered by the user. Text values are trimmed on creation.</summary>
internal sealed record MemberInput(string FirstName, string LastName)
{
    public static MemberInput Create(string? firstName, string? lastName) =>
        new(firstName?.Trim() ?? string.Empty, lastName?.Trim() ?? string.Empty);
}

/// <summary>A request to lend a book to a member.</summary>
internal sealed record LoanInput(int BookId, int MemberId);

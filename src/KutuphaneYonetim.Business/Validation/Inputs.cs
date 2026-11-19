namespace KutuphaneYonetim.Business.Validation;

/// <summary>
/// Book data entered by the user. Text values are trimmed on creation; the ISBN loses its spaces and
/// hyphens and becomes null when empty.
/// </summary>
internal sealed record BookInput(string Title, string Author, string? Isbn = null)
{
    public static BookInput Create(string? title, string? author, string? isbn = null) =>
        new(title?.Trim() ?? string.Empty, author?.Trim() ?? string.Empty, IsbnFormat.Normalize(isbn));
}

/// <summary>ISBN-10 / ISBN-13 normalisation and check digit validation.</summary>
internal static class IsbnFormat
{
    /// <summary>Removes spaces and hyphens and upper-cases a trailing x; null for empty input.</summary>
    public static string? Normalize(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        var compact = new string(value.Where(c => c is not (' ' or '-')).ToArray());
        return compact.ToUpperInvariant();
    }

    /// <summary>True for a normalised ISBN-10 or ISBN-13 with a correct check digit.</summary>
    public static bool IsValid(string value)
    {
        ArgumentNullException.ThrowIfNull(value);

        return value.Length switch
        {
            10 => IsValidIsbn10(value),
            13 => value.All(char.IsAsciiDigit) && Isbn13CheckDigit(value) == value[12] - '0',
            _ => false,
        };
    }

    private static bool IsValidIsbn10(string value)
    {
        var sum = 0;
        for (var i = 0; i < 10; i++)
        {
            int digit;
            if (char.IsAsciiDigit(value[i]))
            {
                digit = value[i] - '0';
            }
            else if (i == 9 && value[i] == 'X')
            {
                digit = 10;
            }
            else
            {
                return false;
            }

            sum += (10 - i) * digit;
        }

        return sum % 11 == 0;
    }

    private static int Isbn13CheckDigit(string value)
    {
        var sum = 0;
        for (var i = 0; i < 12; i++)
        {
            sum += (value[i] - '0') * (i % 2 == 0 ? 1 : 3);
        }

        return (10 - (sum % 10)) % 10;
    }
}

/// <summary>Member data entered by the user. Text values are trimmed on creation.</summary>
internal sealed record MemberInput(string FirstName, string LastName)
{
    public static MemberInput Create(string? firstName, string? lastName) =>
        new(firstName?.Trim() ?? string.Empty, lastName?.Trim() ?? string.Empty);
}

/// <summary>A request to lend a book to a member until <paramref name="DueDate"/>.</summary>
internal sealed record LoanInput(int BookId, int MemberId, DateTime LoanDate, DateTime DueDate);

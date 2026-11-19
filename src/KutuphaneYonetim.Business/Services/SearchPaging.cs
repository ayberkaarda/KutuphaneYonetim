using KutuphaneYonetim.Business.Validation;
using KutuphaneYonetim.Domain;

namespace KutuphaneYonetim.Business.Services;

/// <summary>Normalises search input and keeps the requested page within the result.</summary>
internal static class SearchPaging
{
    public const int MaxPageSize = 200;
    public const int MaxTextLength = 100;

    public static BookQuery Normalize(BookQuery query)
    {
        ArgumentNullException.ThrowIfNull(query);

        var text = NormalizeText(query.Text);

        // "978-975-..." also finds the stored ISBN, which has no separators; titles and authors are still
        // searched with the text as typed ("1914-1918").
        string? isbnText = null;
        if (text is not null && text.Any(char.IsAsciiDigit) && text.All(c => char.IsAsciiDigit(c) || c is '-' or ' ' or 'x' or 'X'))
        {
            var compact = IsbnFormat.Normalize(text);
            isbnText = string.Equals(compact, text, StringComparison.Ordinal) ? null : compact;
        }

        return query with
        {
            Text = text,
            IsbnText = isbnText,
            Page = Math.Max(1, query.Page),
            PageSize = Math.Clamp(query.PageSize, 1, MaxPageSize),
        };
    }

    public static MemberQuery Normalize(MemberQuery query)
    {
        ArgumentNullException.ThrowIfNull(query);

        return query with
        {
            Text = NormalizeText(query.Text),
            Page = Math.Max(1, query.Page),
            PageSize = Math.Clamp(query.PageSize, 1, MaxPageSize),
        };
    }

    /// <summary>
    /// Runs the search and, when the requested page lies beyond the last page (for example after rows
    /// were deleted), returns the last page instead of an empty one.
    /// </summary>
    public static async Task<PagedResult<T>> SearchAsync<TQuery, T>(
        TQuery query,
        int page,
        Func<TQuery, Task<PagedResult<T>>> search,
        Func<TQuery, int, TQuery> withPage)
    {
        ArgumentNullException.ThrowIfNull(search);
        ArgumentNullException.ThrowIfNull(withPage);

        var result = await search(query).ConfigureAwait(false);
        if (result.Items.Count == 0 && result.TotalCount > 0 && page > result.PageCount)
        {
            result = await search(withPage(query, result.PageCount)).ConfigureAwait(false);
        }

        return result;
    }

    private static string? NormalizeText(string? text)
    {
        var trimmed = text?.Trim();
        if (string.IsNullOrEmpty(trimmed))
        {
            return null;
        }

        return trimmed.Length > MaxTextLength ? trimmed[..MaxTextLength] : trimmed;
    }
}

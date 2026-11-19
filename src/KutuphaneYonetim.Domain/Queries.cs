namespace KutuphaneYonetim.Domain;

/// <summary>One page of a larger result.</summary>
/// <param name="Items">The rows on this page.</param>
/// <param name="TotalCount">Number of rows matching the query on all pages.</param>
/// <param name="Page">1-based page number.</param>
/// <param name="PageSize">Maximum number of rows per page.</param>
public sealed record PagedResult<T>(IReadOnlyList<T> Items, int TotalCount, int Page, int PageSize)
{
    /// <summary>Number of pages; at least 1 so an empty result still has a page to show.</summary>
    public int PageCount => Math.Max(1, (TotalCount + PageSize - 1) / PageSize);
}

/// <summary>Which books a book search returns.</summary>
public enum BookAvailability
{
    All,

    /// <summary>On the shelf and free to lend (not held for a reservation).</summary>
    Available,

    /// <summary>Lent to a member.</summary>
    OnLoan,

    /// <summary>On the shelf but held for the member at the front of its reservation queue.</summary>
    Reserved,
}

/// <summary>Text search (title, author, ISBN) and availability filter for books.</summary>
public sealed record BookQuery(
    string? Text = null,
    BookAvailability Availability = BookAvailability.All,
    int Page = 1,
    int PageSize = BookQuery.DefaultPageSize)
{
    public const int DefaultPageSize = 50;
}

/// <summary>Which members a member search returns.</summary>
public enum MemberFilter
{
    All,

    /// <summary>Members with fines that are not paid yet.</summary>
    WithUnpaidFine,

    /// <summary>Members who have not returned a book yet.</summary>
    WithActiveLoan,
}

/// <summary>Text search (first name, last name, full name) and filter for members.</summary>
public sealed record MemberQuery(
    string? Text = null,
    MemberFilter Filter = MemberFilter.All,
    int Page = 1,
    int PageSize = BookQuery.DefaultPageSize);

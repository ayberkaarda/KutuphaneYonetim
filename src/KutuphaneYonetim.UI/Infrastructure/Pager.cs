using System.Globalization;
using KutuphaneYonetim.Domain;

namespace KutuphaneYonetim.UI.Infrastructure;

/// <summary>Keeps the current page of a list screen and the state of its previous/next buttons.</summary>
internal sealed class Pager(Button previous, Button next, Label status)
{
    public int Page { get; private set; } = 1;

    /// <summary>Back to the first page, for example when the search text changes.</summary>
    public void Reset() => Page = 1;

    public void MovePrevious() => Page = Math.Max(1, Page - 1);

    public void MoveNext() => Page++;

    /// <summary>Takes over the page the service actually returned and updates the controls.</summary>
    public void Show<T>(PagedResult<T> result)
    {
        ArgumentNullException.ThrowIfNull(result);

        Page = result.Page;
        previous.Enabled = result.Page > 1;
        next.Enabled = result.Page < result.PageCount;
        status.Text = string.Format(
            CultureInfo.CurrentCulture, "Sayfa {0} / {1} ({2} kayıt)", result.Page, result.PageCount, result.TotalCount);
    }
}

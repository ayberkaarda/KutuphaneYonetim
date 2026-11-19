namespace KutuphaneYonetim.UI.Infrastructure;

/// <summary>
/// Numbers overlapping requests (for example list loads triggered by searching or paging) so that only the
/// result of the most recent one is applied; responses that arrive out of order are dropped.
/// Intended for the UI thread only.
/// </summary>
internal sealed class RequestSequence
{
    private int _latest;

    /// <summary>Starts a new request; every earlier request becomes stale.</summary>
    public int Next() => ++_latest;

    /// <summary>Whether <paramref name="request"/> is still the most recent request.</summary>
    public bool IsLatest(int request) => request == _latest;
}

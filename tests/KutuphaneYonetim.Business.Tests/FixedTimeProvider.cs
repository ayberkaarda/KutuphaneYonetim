namespace KutuphaneYonetim.Business.Tests;

/// <summary>A clock frozen at a given instant, in UTC so the local time is predictable.</summary>
internal sealed class FixedTimeProvider(DateTimeOffset now) : TimeProvider
{
    public DateTimeOffset Now { get; set; } = now;

    public override DateTimeOffset GetUtcNow() => Now;

    public override TimeZoneInfo LocalTimeZone => TimeZoneInfo.Utc;
}

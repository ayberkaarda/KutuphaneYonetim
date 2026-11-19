namespace KutuphaneYonetim.Data.Tests;

/// <summary>A clock the test moves by hand, in UTC so the local time is predictable.</summary>
internal sealed class ManualClock(DateTime start) : TimeProvider
{
    public DateTime Now { get; set; } = start;

    public override DateTimeOffset GetUtcNow() => new(DateTime.SpecifyKind(Now, DateTimeKind.Utc));

    public override TimeZoneInfo LocalTimeZone => TimeZoneInfo.Utc;

    public void AdvanceDays(int days) => Now = Now.AddDays(days);
}

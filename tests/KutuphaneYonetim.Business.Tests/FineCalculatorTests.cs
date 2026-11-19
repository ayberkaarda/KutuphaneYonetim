using KutuphaneYonetim.Business.Services;

namespace KutuphaneYonetim.Business.Tests;

public sealed class FineCalculatorTests
{
    private static readonly DateTime Due = new(2026, 5, 10, 0, 0, 0, DateTimeKind.Unspecified);

    [Theory]
    [InlineData(-1, 0)] // Early.
    [InlineData(0, 0)] // On the due date (0 days).
    [InlineData(1, 1)]
    [InlineData(30, 30)]
    public void OverdueDays_CountsCalendarDaysAfterDueDate(int offsetDays, int expected)
    {
        Assert.Equal(expected, FineCalculator.OverdueDays(Due, Due.AddDays(offsetDays)));
    }

    [Fact]
    public void OverdueDays_IgnoresTimeOfDay()
    {
        Assert.Equal(0, FineCalculator.OverdueDays(Due, Due.AddHours(23).AddMinutes(59)));
        Assert.Equal(1, FineCalculator.OverdueDays(Due.AddHours(15), Due.AddDays(1).AddMinutes(1)));
    }

    [Fact]
    public void OverdueDays_WithoutDueDate_IsZero()
    {
        Assert.Equal(0, FineCalculator.OverdueDays(null, Due.AddYears(1)));
    }

    [Theory]
    [InlineData(0, 2.50, 0)]
    [InlineData(1, 2.50, 2.50)]
    [InlineData(3, 1.333, 4.00)] // 3.999 rounds to kuruş.
    [InlineData(-2, 2.50, 0)]
    [InlineData(5, 0, 0)]
    public void Fine_IsDaysTimesDailyRateRoundedToKurus(int days, double daily, double expected)
    {
        Assert.Equal((decimal)expected, FineCalculator.Fine(days, (decimal)daily));
    }
}

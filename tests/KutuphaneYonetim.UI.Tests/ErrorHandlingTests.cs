using KutuphaneYonetim.Data.Persistence;
using KutuphaneYonetim.UI.Infrastructure;

namespace KutuphaneYonetim.UI.Tests;

public sealed class ErrorHandlingTests
{
    private const string PersonalData = "Ayşe Yılmaz";

    [Fact]
    public void DescribeForLog_LeavesOutExceptionMessages()
    {
        var exception = Throw(new InvalidOperationException(
            "Outer " + PersonalData,
            new ArgumentException("Inner " + PersonalData)));

        var logText = AppLogging.DescribeForLog(exception);

        Assert.DoesNotContain(PersonalData, logText, StringComparison.Ordinal);
        Assert.Contains(typeof(InvalidOperationException).FullName!, logText, StringComparison.Ordinal);
        Assert.Contains(typeof(ArgumentException).FullName!, logText, StringComparison.Ordinal);
        Assert.Contains(nameof(Throw), logText, StringComparison.Ordinal); // Stack trace is kept.
    }

    [Fact]
    public void DescribeForLog_IncludesAllAggregateInnerExceptions()
    {
        var exception = new AggregateException(new TimeoutException(PersonalData), new FormatException(PersonalData));

        var logText = AppLogging.DescribeForLog(exception);

        Assert.Contains(typeof(TimeoutException).FullName!, logText, StringComparison.Ordinal);
        Assert.Contains(typeof(FormatException).FullName!, logText, StringComparison.Ordinal);
        Assert.DoesNotContain(PersonalData, logText, StringComparison.Ordinal);
    }

    [Fact]
    public void Describe_UnexpectedError_ShowsOnlyTurkishTextWithoutRawMessage()
    {
        var message = ErrorReporter.Describe(new InvalidOperationException("Sequence contains no elements " + PersonalData));

        Assert.StartsWith("Beklenmeyen bir hata oluştu.", message, StringComparison.Ordinal);
        Assert.DoesNotContain("Sequence", message, StringComparison.Ordinal);
        Assert.DoesNotContain(PersonalData, message, StringComparison.Ordinal);
    }

    [Fact]
    public void Describe_DatabaseInitializationFailure_ExplainsSetup()
    {
        var message = ErrorReporter.Describe(new DatabaseInitializationException(new TimeoutException()));

        Assert.StartsWith("Veritabanı hazırlanamadı.", message, StringComparison.Ordinal);
    }

    [Fact]
    public void Describe_WrappedTimeout_IsRecognised()
    {
        var message = ErrorReporter.Describe(new AggregateException(new TimeoutException()));

        Assert.Equal("İşlem zaman aşımına uğradı. Lütfen tekrar deneyin.", message);
    }

    [Fact]
    public void CreateLogger_WritesDailyFileInGivenDirectory()
    {
        var directory = Path.Combine(Path.GetTempPath(), "kutuphane-log-test-" + Guid.NewGuid().ToString("N"));
        try
        {
            using (var logger = AppLogging.CreateLogger(directory))
            {
                logger.Information("probe");
            }

            var file = Assert.Single(Directory.GetFiles(directory));
            Assert.Matches(@"kutuphane-\d{8}\.log$", file);
            Assert.Contains("probe", File.ReadAllText(file), StringComparison.Ordinal);
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    [Fact]
    public void DefaultDirectory_IsUnderLocalApplicationData()
    {
        var root = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);

        Assert.StartsWith(root, AppLogging.DefaultDirectory, StringComparison.OrdinalIgnoreCase);
    }

    private static Exception Throw(Exception exception)
    {
        try
        {
            throw exception;
        }
#pragma warning disable CA1031 // Test helper captures the stack trace.
        catch (Exception caught)
#pragma warning restore CA1031
        {
            return caught;
        }
    }
}

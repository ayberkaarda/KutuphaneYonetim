using System.Data.Common;
using System.Globalization;
using System.Text;
using Serilog;
using Serilog.Core;

namespace KutuphaneYonetim.UI.Infrastructure;

/// <summary>File logging for diagnostics. Logs stay on the user's machine, outside the repository.</summary>
internal static class AppLogging
{
    private const int RetainedDays = 30;

    /// <summary>%LOCALAPPDATA%\KutuphaneYonetim\logs</summary>
    public static string DefaultDirectory { get; } = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "KutuphaneYonetim",
        "logs");

    /// <summary>Creates a logger that writes one file per day and keeps the last 30 files.</summary>
    public static Logger CreateLogger(string directory)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(directory);

        return new LoggerConfiguration()
            .MinimumLevel.Information()
            .WriteTo.File(
                Path.Combine(directory, "kutuphane-.log"),
                rollingInterval: RollingInterval.Day,
                retainedFileCountLimit: RetainedDays,
                formatProvider: CultureInfo.InvariantCulture,
                outputTemplate: "{Timestamp:yyyy-MM-dd HH:mm:ss.fff zzz} [{Level:u3}] {Message:lj}{NewLine}")
            .CreateLogger();
    }

    /// <summary>
    /// Describes an exception for the log without its messages: exception messages can carry user
    /// entered values (for example member names inside a database error), so only types, error
    /// codes and stack traces are written.
    /// </summary>
    public static string DescribeForLog(Exception exception)
    {
        ArgumentNullException.ThrowIfNull(exception);

        var builder = new StringBuilder();
        Append(builder, exception, depth: 0);
        return builder.ToString();
    }

    private static void Append(StringBuilder builder, Exception exception, int depth)
    {
        var indent = new string(' ', depth * 2);
        builder.Append(indent).Append(exception.GetType().FullName)
            .Append(CultureInfo.InvariantCulture, $" (HResult 0x{exception.HResult:X8})");

        if (exception is DbException dbException)
        {
            builder.Append(CultureInfo.InvariantCulture, $" ErrorCode={dbException.ErrorCode}");
            if (!string.IsNullOrEmpty(dbException.SqlState))
            {
                builder.Append(" SqlState=").Append(dbException.SqlState);
            }
        }

        builder.AppendLine();
        if (exception.StackTrace is { } stackTrace)
        {
            builder.Append(indent).AppendLine(stackTrace.TrimEnd());
        }

        if (exception is AggregateException aggregate)
        {
            foreach (var inner in aggregate.InnerExceptions)
            {
                Append(builder, inner, depth + 1);
            }
        }
        else if (exception.InnerException is { } inner)
        {
            Append(builder, inner, depth + 1);
        }
    }
}

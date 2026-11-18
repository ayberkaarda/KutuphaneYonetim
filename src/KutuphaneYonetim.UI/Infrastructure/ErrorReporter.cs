using System.Data.Common;
using KutuphaneYonetim.Business;
using KutuphaneYonetim.Data.Persistence;
using Serilog;

namespace KutuphaneYonetim.UI.Infrastructure;

/// <summary>
/// Logs exceptions and turns them into short, user friendly Turkish messages. Technical details
/// go to the log file only; the user never sees raw exception text.
/// </summary>
internal static class ErrorReporter
{
    private const string Caption = "Kütüphane Yönetim";

    public static void Show(Exception? exception, IWin32Window? owner = null)
    {
        if (exception is null)
        {
            return;
        }

        if (exception is BusinessRuleException)
        {
            // Business rule messages are fixed texts without user data.
            Log.Warning("Business rule rejected an operation: {Rule}", exception.Message);
            MessageBox.Show(owner, exception.Message, "Uyarı", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

        Log.Error("Operation failed: {Details}", AppLogging.DescribeForLog(exception));
        MessageBox.Show(owner, Describe(exception), Caption, MessageBoxButtons.OK, MessageBoxIcon.Error);
    }

    public static string Describe(Exception exception)
    {
        ArgumentNullException.ThrowIfNull(exception);

        if (Find<DatabaseInitializationException>(exception) is not null)
        {
            return "Veritabanı hazırlanamadı. SQL Server'ın çalıştığından, bağlantı ayarlarının doğru olduğundan "
                + "ve veritabanı oluşturma yetkiniz bulunduğundan emin olun.";
        }

        if (Find<DbException>(exception) is not null)
        {
            return "Veritabanı işlemi tamamlanamadı. SQL Server'ın çalıştığından ve bağlantı ayarlarının doğru olduğundan emin olun.";
        }

        if (Find<TimeoutException>(exception) is not null)
        {
            return "İşlem zaman aşımına uğradı. Lütfen tekrar deneyin.";
        }

        // Raw exception text is technical (often English) and may contain user data: log only.
        return "Beklenmeyen bir hata oluştu. İşleminiz tamamlanamamış olabilir.\n\n"
            + "Ayrıntılar günlük dosyasına kaydedildi.";
    }

    private static T? Find<T>(Exception exception)
        where T : Exception
    {
        for (Exception? current = exception; current is not null; current = current.InnerException)
        {
            if (current is T match)
            {
                return match;
            }

            if (current is AggregateException { InnerExceptions.Count: > 0 } aggregate)
            {
                current = aggregate.InnerExceptions[0];
                if (current is T inner)
                {
                    return inner;
                }
            }
        }

        return null;
    }
}

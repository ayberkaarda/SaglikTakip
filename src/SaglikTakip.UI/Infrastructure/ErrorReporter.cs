using System.Data.Common;
using System.Diagnostics;
using SaglikTakip.Business;
using SaglikTakip.Data.Persistence;

namespace SaglikTakip.UI.Infrastructure;

/// <summary>Turns exceptions into short, user friendly Turkish messages.</summary>
internal static class ErrorReporter
{
    private const string Caption = "Sağlık Takip";

    public static void Show(Exception? exception, IWin32Window? owner = null)
    {
        if (exception is null)
        {
            return;
        }

        if (exception is BusinessRuleException)
        {
            MessageBox.Show(owner, exception.Message, "Uyarı", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

        Trace.TraceError(exception.ToString());
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

        return "Beklenmeyen bir hata oluştu. İşleminiz tamamlanamamış olabilir.\n\nAyrıntı: " + exception.Message;
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

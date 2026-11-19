using System.Data.Common;
using System.Diagnostics;
using System.Globalization;
using System.Text;
using SaglikTakip.Business;
using SaglikTakip.Data.Persistence;

namespace SaglikTakip.UI.Infrastructure;

/// <summary>Turns exceptions into short, user friendly Turkish messages.</summary>
internal static class ErrorReporter
{
    private const string Caption = "Sağlık Takip";
    private const int MaxLoggedExceptions = 8;

    internal const string UnexpectedErrorMessage = "Beklenmeyen bir hata oluştu. İşleminiz tamamlanamamış olabilir.";

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

        Trace.TraceError(FormatForLog(exception));
        MessageBox.Show(owner, Describe(exception), Caption, MessageBoxButtons.OK, MessageBoxIcon.Error);
    }

    /// <summary>
    /// Builds a log entry that carries no exception messages and no file paths: exception messages
    /// can contain server names, connection details or personal data. Only the exception types, their
    /// error codes and the methods of each stack frame are written, for the whole inner exception chain.
    /// </summary>
    public static string FormatForLog(Exception exception)
    {
        ArgumentNullException.ThrowIfNull(exception);

        var builder = new StringBuilder();
        var depth = 0;
        for (Exception? current = exception; current is not null && depth < MaxLoggedExceptions; depth++)
        {
            if (depth > 0)
            {
                builder.AppendLine("--- inner exception ---");
            }

            builder.Append(current.GetType().FullName)
                .Append(" (HResult 0x")
                .Append(current.HResult.ToString("X8", CultureInfo.InvariantCulture))
                .AppendLine(")");

            foreach (var frame in new StackTrace(current, fNeedFileInfo: false).GetFrames())
            {
                var method = frame.GetMethod();
                if (method is not null)
                {
                    builder.Append("   at ")
                        .Append(method.DeclaringType?.FullName ?? "<unknown>")
                        .Append('.')
                        .AppendLine(method.Name);
                }
            }

            current = current is AggregateException { InnerExceptions.Count: > 0 } aggregate
                ? aggregate.InnerExceptions[0]
                : current.InnerException;
        }

        return builder.ToString();
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

        return UnexpectedErrorMessage;
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

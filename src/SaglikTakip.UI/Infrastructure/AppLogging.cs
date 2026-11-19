using System.Globalization;
using Serilog;
using Serilog.Core;

namespace SaglikTakip.UI.Infrastructure;

/// <summary>
/// File logging for diagnostics. Logs stay on the user's machine, outside the repository. Health data
/// (weights, pulses, notes), user names and exception messages are never written; see
/// <see cref="ErrorReporter.FormatForLog"/> for what an error entry contains.
/// </summary>
internal static class AppLogging
{
    internal const int RetainedDays = 30;

    /// <summary>%LOCALAPPDATA%\SaglikTakip\logs</summary>
    public static string DefaultDirectory { get; } = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "SaglikTakip",
        "logs");

    /// <summary>Creates a logger that writes one file per day and keeps the last 30 files.</summary>
    public static Logger CreateLogger(string directory)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(directory);

        return new LoggerConfiguration()
            .MinimumLevel.Information()
            .WriteTo.File(
                Path.Combine(directory, "sagliktakip-.log"),
                rollingInterval: RollingInterval.Day,
                retainedFileCountLimit: RetainedDays,
                formatProvider: CultureInfo.InvariantCulture,
                outputTemplate: "{Timestamp:yyyy-MM-dd HH:mm:ss.fff zzz} [{Level:u3}] {Message:lj}{NewLine}")
            .CreateLogger();
    }
}

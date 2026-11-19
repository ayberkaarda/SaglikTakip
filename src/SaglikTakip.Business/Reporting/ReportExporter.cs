using System.Text;

namespace SaglikTakip.Business.Reporting;

/// <summary>Export formats of the period report.</summary>
public enum ReportFormat
{
    Csv,
    Html,
}

/// <summary>
/// Saves a <see cref="PeriodReport"/> to a file. The content is written to a temporary file in the
/// target folder first and moved over the target only when complete, so a failure (disk full,
/// cancelled export) never leaves a half-written report or destroys an earlier one.
/// </summary>
public static class ReportExporter
{
    public static string Extension(ReportFormat format) => format == ReportFormat.Csv ? ".csv" : ".html";

    public static Task ExportAsync(PeriodReport report, ReportFormat format, string path, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(report);

        return format switch
        {
            ReportFormat.Csv => WriteAtomicallyAsync(path, CsvReportWriter.Encoding, w => CsvReportWriter.Write(report, w), cancellationToken),
            ReportFormat.Html => WriteAtomicallyAsync(path, HtmlReportWriter.Encoding, w => HtmlReportWriter.Write(report, w), cancellationToken),
            _ => throw new ArgumentOutOfRangeException(nameof(format), format, "Unknown report format."),
        };
    }

    /// <summary>Writes through a temporary file in the same folder and moves it over <paramref name="path"/>.</summary>
    internal static async Task WriteAtomicallyAsync(
        string path,
        Encoding encoding,
        Action<TextWriter> write,
        CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        ArgumentNullException.ThrowIfNull(encoding);
        ArgumentNullException.ThrowIfNull(write);

        var target = Path.GetFullPath(path);
        var directory = Path.GetDirectoryName(target) ?? throw new ArgumentException("The path has no folder.", nameof(path));

        // Same folder, so the final move is a rename on the same volume.
        var temporary = Path.Combine(directory, $".{Path.GetFileName(target)}.{Guid.NewGuid():N}.tmp");
        try
        {
            var stream = new FileStream(temporary, FileMode.CreateNew, FileAccess.Write, FileShare.None, 4096, FileOptions.Asynchronous);
            await using (stream.ConfigureAwait(false))
            {
                var writer = new StreamWriter(stream, encoding);
                await using (writer.ConfigureAwait(false))
                {
                    write(writer);
                    cancellationToken.ThrowIfCancellationRequested();
                    await writer.FlushAsync(cancellationToken).ConfigureAwait(false);
                    stream.Flush(flushToDisk: true);
                }
            }

            File.Move(temporary, target, overwrite: true);
        }
        catch
        {
            TryDelete(temporary);
            throw;
        }
    }

    private static void TryDelete(string file)
    {
        try
        {
            File.Delete(file);
        }
        catch (IOException)
        {
            // Best effort; the original error is more useful to the caller.
        }
        catch (UnauthorizedAccessException)
        {
        }
    }
}

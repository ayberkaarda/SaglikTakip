using System.Globalization;
using System.Text;
using SaglikTakip.Business.Insights;
using SaglikTakip.Domain.Entities;

namespace SaglikTakip.Business.Reporting;

/// <summary>
/// Writes a <see cref="PeriodReport"/> as CSV for spreadsheet programs: UTF-8 with a byte order mark,
/// ";" as separator and Turkish number format (decimal comma), which Excel in Turkish opens directly.
/// Dates use yyyy-MM-dd. Every cell goes through <see cref="Cell"/>, which neutralises formulas.
/// </summary>
public static class CsvReportWriter
{
    public const char Separator = ';';

    /// <summary>UTF-8 with a byte order mark, so spreadsheet programs detect the encoding.</summary>
    public static Encoding Encoding { get; } = new UTF8Encoding(encoderShouldEmitUTF8Identifier: true);

    private static readonly CultureInfo Turkish = CultureInfo.GetCultureInfo("tr-TR");

    /// <summary>
    /// Makes one CSV cell safe. A value starting with "=", "+", "-", "@", a tab or a carriage return
    /// would be run as a formula by spreadsheet programs (CSV/formula injection), so it gets a leading
    /// apostrophe. Values containing the separator, quotes or line breaks are quoted, quotes doubled.
    /// </summary>
    public static string Cell(string? value)
    {
        var text = value ?? string.Empty;
        if (text.Length > 0 && text[0] is '=' or '+' or '-' or '@' or '\t' or '\r')
        {
            text = "'" + text;
        }

        if (text.IndexOfAny([Separator, '"', '\r', '\n']) >= 0)
        {
            text = "\"" + text.Replace("\"", "\"\"", StringComparison.Ordinal) + "\"";
        }

        return text;
    }

    public static void Write(PeriodReport report, TextWriter writer)
    {
        ArgumentNullException.ThrowIfNull(report);
        ArgumentNullException.ThrowIfNull(writer);

        Line(writer, "Sağlık Takip dönem raporu");
        Line(writer, "Kullanıcı", report.User.Name);
        Line(writer, "Başlangıç", Day(report.From));
        Line(writer, "Bitiş", Day(report.To));
        Line(writer, "Gün sayısı", report.Days.ToString(Turkish));
        Line(writer, "Oluşturulma", report.GeneratedAt.ToString("yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture));
        writer.WriteLine();

        Line(writer, "Özet", "Ölçüm sayısı", "En düşük", "En yüksek", "Ortalama", "İlk", "Son");
        SummaryLine(writer, "Kilo (kg)", report.Weight);
        SummaryLine(writer, "Nabız (atım/dk)", report.Pulse);
        writer.WriteLine();

        if (report.Goal is { } goal)
        {
            Line(writer, "Hedef kilo (kg)", Number(goal.Goal.TargetWeight));
            Line(writer, "Başlangıç kilosu (kg)", Number(goal.Goal.StartWeight));
            Line(writer, "Kilo sınırları (kg)", Number(goal.Goal.MinWeight), Number(goal.Goal.MaxWeight));
            Line(writer, "Nabız sınırları", Number(goal.Goal.MinPulse), Number(goal.Goal.MaxPulse));
            Line(writer, "Hedefe ilerleme (%)", goal.ProgressPercent is { } p ? p.ToString("0", Turkish) : string.Empty);
            Line(writer, "Sınır dışı ölçüm", goal.Alerts.Count.ToString(Turkish));
            writer.WriteLine();
        }

        var flagged = report.Goal?.Alerts.ToLookup(a => a.RecordId);
        Line(writer, "Tarih", "Kilo (kg)", "Nabız (atım/dk)", "Hedef dışı", "Notlar");
        foreach (var record in report.Records)
        {
            var alerts = flagged?[record.Id] ?? [];
            Line(
                writer,
                Day(record.Date),
                Number(record.Weight),
                record.Pulse.ToString(Turkish),
                string.Join(", ", alerts.Select(Flag)),
                record.Notes);
        }
    }

    private static void SummaryLine(TextWriter writer, string name, MetricSummary? summary)
    {
        if (summary is null)
        {
            Line(writer, name, "0");
            return;
        }

        Line(
            writer,
            name,
            summary.Count.ToString(Turkish),
            Number(summary.Minimum),
            Number(summary.Maximum),
            Number(summary.Average),
            Number(summary.First),
            Number(summary.Last));
    }

    private static string Flag(ThresholdAlert alert) =>
        (alert.Metric == HealthMetric.Weight ? "Kilo" : "Nabız")
        + (alert.Side == LimitSide.BelowMinimum ? " düşük" : " yüksek");

    private static string Day(DateTime value) => value.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);

    private static string Number(double? value) => value?.ToString("0.##", Turkish) ?? string.Empty;

    private static void Line(TextWriter writer, params string?[] cells) =>
        writer.WriteLine(string.Join(Separator, cells.Select(Cell)));
}

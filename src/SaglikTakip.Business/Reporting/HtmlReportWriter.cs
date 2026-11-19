using System.Globalization;
using System.Net;
using System.Text;
using SaglikTakip.Business.Insights;
using SaglikTakip.Domain.Entities;

namespace SaglikTakip.Business.Reporting;

/// <summary>
/// Writes a <see cref="PeriodReport"/> as a single, self-contained HTML page sized for A4 printing.
/// It needs no library: any browser shows it and "Yazdır &gt; PDF olarak kaydet" turns it into a PDF.
/// The page has no scripts and a restrictive content security policy, and every value is HTML encoded.
/// </summary>
public static class HtmlReportWriter
{
    private static readonly CultureInfo Turkish = CultureInfo.GetCultureInfo("tr-TR");

    public static Encoding Encoding { get; } = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false);

    public static void Write(PeriodReport report, TextWriter writer)
    {
        ArgumentNullException.ThrowIfNull(report);
        ArgumentNullException.ThrowIfNull(writer);

        var title = $"Sağlık Takip dönem raporu: {Day(report.From)} - {Day(report.To)}";
        writer.WriteLine("<!DOCTYPE html>");
        writer.WriteLine("<html lang=\"tr\">");
        writer.WriteLine("<head>");
        writer.WriteLine("<meta charset=\"utf-8\">");
        writer.WriteLine("<meta http-equiv=\"Content-Security-Policy\" content=\"default-src 'none'; style-src 'unsafe-inline'\">");
        writer.WriteLine("<meta name=\"viewport\" content=\"width=device-width, initial-scale=1\">");
        writer.WriteLine($"<title>{E(title)}</title>");
        writer.WriteLine("<style>");
        writer.WriteLine(Styles);
        writer.WriteLine("</style>");
        writer.WriteLine("</head>");
        writer.WriteLine("<body>");
        writer.WriteLine("<h1>Sağlık Takip dönem raporu</h1>");
        writer.WriteLine("<dl class=\"meta\">");
        Term(writer, "Kullanıcı", report.User.Name);
        Term(writer, "Dönem", $"{Day(report.From)} – {Day(report.To)} ({report.Days} gün)");
        Term(writer, "Oluşturulma", report.GeneratedAt.ToString("dd.MM.yyyy HH:mm", Turkish));
        writer.WriteLine("</dl>");

        writer.WriteLine("<h2>Özet</h2>");
        writer.WriteLine("<table><thead><tr><th>Ölçüm</th><th>Sayı</th><th>En düşük</th><th>En yüksek</th><th>Ortalama</th><th>İlk</th><th>Son</th></tr></thead><tbody>");
        SummaryRow(writer, "Kilo (kg)", report.Weight);
        SummaryRow(writer, "Nabız (atım/dk)", report.Pulse);
        writer.WriteLine("</tbody></table>");

        if (report.Records.Count > 1)
        {
            writer.WriteLine("<h2>Kilo eğilimi</h2>");
            writer.WriteLine(Sparkline(report.Records.Select(r => (r.Date, r.Weight)).ToList(), report.Goal?.Goal));
        }

        if (report.Goal is { } goal)
        {
            writer.WriteLine("<h2>Hedef</h2>");
            writer.WriteLine("<dl class=\"meta\">");
            Term(writer, "Hedef kilo", Unit(goal.Goal.TargetWeight, "kg"));
            Term(writer, "Başlangıç kilosu", Unit(goal.Goal.StartWeight, "kg"));
            Term(writer, "Kilo sınırları", Limits(goal.Goal.MinWeight, goal.Goal.MaxWeight, "kg"));
            Term(writer, "Nabız sınırları", Limits(goal.Goal.MinPulse, goal.Goal.MaxPulse, "atım/dk"));
            Term(writer, "Hedefe ilerleme", goal.ProgressPercent is { } p ? "%" + p.ToString("0", Turkish) : "hesaplanamadı");
            Term(writer, "Sınır dışı ölçüm", goal.Alerts.Count.ToString(Turkish));
            writer.WriteLine("</dl>");
        }

        writer.WriteLine("<h2>Ölçümler</h2>");
        if (report.Records.Count == 0)
        {
            writer.WriteLine("<p>Bu dönemde ölçüm yok.</p>");
        }
        else
        {
            var flagged = report.Goal?.Alerts.ToLookup(a => a.RecordId);
            writer.WriteLine("<table><thead><tr><th>Tarih</th><th>Kilo (kg)</th><th>Nabız</th><th>Notlar</th></tr></thead><tbody>");
            foreach (var record in report.Records)
            {
                var alerts = flagged?[record.Id].ToList() ?? [];
                writer.Write(alerts.Count > 0 ? "<tr class=\"alert\">" : "<tr>");
                writer.Write($"<td>{E(Day(record.Date))}</td>");
                writer.Write($"<td class=\"num\">{E(record.Weight.ToString("0.##", Turkish))}{Marker(alerts, HealthMetric.Weight)}</td>");
                writer.Write($"<td class=\"num\">{E(record.Pulse.ToString(Turkish))}{Marker(alerts, HealthMetric.Pulse)}</td>");
                writer.WriteLine($"<td>{E(record.Notes ?? string.Empty)}</td></tr>");
            }

            writer.WriteLine("</tbody></table>");
            if (flagged is not null && report.Goal!.Alerts.Count > 0)
            {
                writer.WriteLine("<p class=\"note\">▲ üst sınırın üstünde, ▼ alt sınırın altında. Sınıra eşit değerler aralık içindedir.</p>");
            }
        }

        writer.WriteLine("<footer>PDF için tarayıcıda Yazdır &gt; PDF olarak kaydet seçeneğini kullanın. Bu rapor kişisel sağlık verisi içerir.</footer>");
        writer.WriteLine("</body>");
        writer.WriteLine("</html>");
    }

    /// <summary>Inline SVG line of the values with optional limit lines; numbers use the invariant culture.</summary>
    internal static string Sparkline(IReadOnlyList<(DateTime Date, double Value)> points, HealthGoal? goal)
    {
        const double Width = 640;
        const double Height = 160;
        const double Pad = 12;

        var values = points.Select(p => p.Value).ToList();
        if (goal?.MinWeight is { } low)
        {
            values.Add(low);
        }

        if (goal?.MaxWeight is { } high)
        {
            values.Add(high);
        }

        var min = values.Min();
        var max = values.Max();
        if (max - min < 1e-9)
        {
            min -= 1;
            max += 1;
        }

        var first = points[0].Date;
        var span = Math.Max(1, (points[^1].Date - first).TotalDays);
        string X(DateTime date) => (Pad + ((date - first).TotalDays / span * (Width - (2 * Pad)))).ToString("0.#", CultureInfo.InvariantCulture);
        string Y(double value) => (Height - Pad - ((value - min) / (max - min) * (Height - (2 * Pad)))).ToString("0.#", CultureInfo.InvariantCulture);

        var builder = new StringBuilder();
        builder.Append(CultureInfo.InvariantCulture, $"<svg class=\"chart\" viewBox=\"0 0 {Width} {Height}\" role=\"img\" aria-label=\"Kilo eğilimi\">");
        foreach (var limit in new[] { goal?.MinWeight, goal?.MaxWeight })
        {
            if (limit is { } y)
            {
                builder.Append(CultureInfo.InvariantCulture, $"<line x1=\"{Pad}\" x2=\"{Width - Pad}\" y1=\"{Y(y)}\" y2=\"{Y(y)}\" class=\"limit\"/>");
            }
        }

        builder.Append("<polyline class=\"line\" points=\"");
        builder.AppendJoin(' ', points.Select(p => X(p.Date) + "," + Y(p.Value)));
        builder.Append("\"/></svg>");
        return builder.ToString();
    }

    private const string Styles = """
        @page { size: A4; margin: 16mm; }
        body { font-family: "Segoe UI", Arial, sans-serif; color: #1d2330; margin: 24px; font-size: 13px; }
        h1 { font-size: 20px; margin: 0 0 8px; }
        h2 { font-size: 15px; margin: 20px 0 6px; border-bottom: 1px solid #c8ccd4; padding-bottom: 2px; }
        dl.meta { display: grid; grid-template-columns: max-content 1fr; gap: 2px 12px; margin: 0; }
        dl.meta dt { color: #5a6270; }
        dl.meta dd { margin: 0; }
        table { border-collapse: collapse; width: 100%; }
        th, td { border: 1px solid #c8ccd4; padding: 3px 6px; text-align: left; }
        th { background: #eef0f4; }
        td.num { text-align: right; font-variant-numeric: tabular-nums; }
        tr.alert td { background: #fdecea; }
        .chart { width: 100%; max-width: 640px; height: auto; border: 1px solid #c8ccd4; }
        .chart .line { fill: none; stroke: #2f6db3; stroke-width: 2; }
        .chart .limit { stroke: #b3261e; stroke-width: 1; stroke-dasharray: 4 3; }
        .note, footer { color: #5a6270; font-size: 11px; }
        footer { margin-top: 24px; }
        @media print { body { margin: 0; } tr { break-inside: avoid; } }
        """;

    private static void SummaryRow(TextWriter writer, string name, MetricSummary? summary)
    {
        if (summary is null)
        {
            writer.WriteLine($"<tr><td>{E(name)}</td><td class=\"num\">0</td><td colspan=\"5\">Ölçüm yok</td></tr>");
            return;
        }

        writer.WriteLine(
            $"<tr><td>{E(name)}</td><td class=\"num\">{summary.Count.ToString(Turkish)}</td>"
            + $"<td class=\"num\">{N(summary.Minimum)}</td><td class=\"num\">{N(summary.Maximum)}</td>"
            + $"<td class=\"num\">{N(summary.Average)}</td><td class=\"num\">{N(summary.First)}</td><td class=\"num\">{N(summary.Last)}</td></tr>");
    }

    private static string Marker(List<ThresholdAlert> alerts, HealthMetric metric) =>
        alerts.FirstOrDefault(a => a.Metric == metric) is { } alert
            ? alert.Side == LimitSide.AboveMaximum ? " ▲" : " ▼"
            : string.Empty;

    private static void Term(TextWriter writer, string term, string value) =>
        writer.WriteLine($"<dt>{E(term)}</dt><dd>{E(value)}</dd>");

    private static string Unit(double? value, string unit) =>
        value is { } v ? v.ToString("0.##", Turkish) + " " + unit : "—";

    private static string Limits(double? min, double? max, string unit) =>
        min is null && max is null
            ? "—"
            : $"{(min is { } a ? a.ToString("0.##", Turkish) : "…")} – {(max is { } b ? b.ToString("0.##", Turkish) : "…")} {unit}";

    private static string N(double value) => E(value.ToString("0.##", Turkish));

    private static string Day(DateTime value) => value.ToString("dd.MM.yyyy", Turkish);

    private static string E(string value) => WebUtility.HtmlEncode(value);
}

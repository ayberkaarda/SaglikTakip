using System.ComponentModel;
using System.Diagnostics;
using System.Globalization;
using System.Text;
using SaglikTakip.Business;
using SaglikTakip.Business.Insights;
using SaglikTakip.Business.Reporting;
using SaglikTakip.Business.Services;
using SaglikTakip.Domain.Entities;
using SaglikTakip.UI.Infrastructure;
using Serilog;

namespace SaglikTakip.UI
{
    /// <summary>Weekly or monthly summary of the selected user with CSV and printable HTML export.</summary>
    public sealed class DonemRaporuForm : Form
    {
        internal const string NoUser = "Rapor için bir kullanıcı seçin.";
        internal const string Loading = "Yükleniyor…";

        private readonly IUserService _userService;
        private readonly IPeriodReportService _reportService;
        private readonly RequestSequence _previewRequests = new();
        private readonly ExclusiveOperation _export = new();

        private readonly ComboBox _userPicker = FormLayout.UserPicker();
        private readonly DateTimePicker _from;
        private readonly DateTimePicker _to;
        private readonly TextBox _preview = new()
        {
            Name = "previewText",
            Dock = DockStyle.Fill,
            Multiline = true,
            ReadOnly = true,
            ScrollBars = ScrollBars.Vertical,
            Font = new Font("Consolas", 9.75F),
            BackColor = SystemColors.Window,
        };

        private readonly Button _csv = FormLayout.Button("btnCsv", "CSV olarak kaydet");
        private readonly Button _html = FormLayout.Button("btnHtml", "Yazdırılabilir rapor (HTML)");
        private readonly Button[] _presets;
        private bool _applyingPreset;

        public DonemRaporuForm(IUserService userService, IPeriodReportService reportService)
        {
            _userService = userService;
            _reportService = reportService;

            var week = DayRanges.LastDays(DateTime.Today, 7);
            _from = FormLayout.DatePicker("fromPicker", week.First);
            _to = FormLayout.DatePicker("toPicker", week.Last);
            _presets =
            [
                Preset("btnHafta", "Son 7 gün", () => DayRanges.LastDays(DateTime.Today, 7)),
                Preset("btnAy", "Son 30 gün", () => DayRanges.LastDays(DateTime.Today, 30)),
                Preset("btnBuAy", "Bu ay", () => DayRanges.MonthToDate(DateTime.Today)),
                Preset("btnGecenAy", "Geçen ay", () => DayRanges.PreviousMonth(DateTime.Today)),
            ];
            ChoosePath = AskForPath;
            OfferToOpen = _ => MessageBox.Show(
                this,
                "Rapor kaydedildi. Yazdırmak veya PDF olarak kaydetmek için tarayıcıda açılsın mı?",
                "Bilgi",
                MessageBoxButtons.YesNo,
                MessageBoxIcon.Question) == DialogResult.Yes;
            BuildLayout();

            Load += async (_, _) => await this.RunAsync(() => _userPicker.LoadUsersAsync(_userService));
            _userPicker.SelectedIndexChanged += async (_, _) => await ShowPreviewAsync();
            _from.ValueChanged += async (_, _) => await OnRangeChangedAsync();
            _to.ValueChanged += async (_, _) => await OnRangeChangedAsync();
            _csv.Click += async (_, _) => await ExportAsync(ReportFormat.Csv);
            _html.Click += async (_, _) => await ExportAsync(ReportFormat.Html);
        }

        /// <summary>Asks for the target file; returns <see langword="null"/> when the user cancels. Replaceable in tests.</summary>
        [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        internal Func<ReportFormat, string, string?> ChoosePath { get; set; }

        /// <summary>Asks whether to open the saved HTML report in the browser. Replaceable in tests.</summary>
        [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        internal Func<string, bool> OfferToOpen { get; set; }

        /// <summary>Loads the preview for the current selection; only the latest answer is shown.</summary>
        internal async Task ShowPreviewAsync()
        {
            var request = _previewRequests.Next();
            if (_userPicker.SelectedItem is not User user)
            {
                _preview.Text = NoUser;
                UseWaitCursor = false;
                return;
            }

            _preview.Text = Loading;
            UseWaitCursor = true;
            try
            {
                var report = await _reportService.BuildAsync(user.Id, _from.Value, _to.Value);
                if (_previewRequests.IsLatest(request))
                {
                    _preview.Text = DescribePreview(report);
                }
            }
            catch (BusinessRuleException ex) when (_previewRequests.IsLatest(request))
            {
                _preview.Text = ex.Message;
            }
#pragma warning disable CA1031 // UI boundary: every failure is reported to the user.
            catch (Exception ex)
#pragma warning restore CA1031
            {
                if (_previewRequests.IsLatest(request))
                {
                    _preview.Text = "—";
                    ErrorReporter.Show(ex, this);
                }
                else
                {
                    Log.Warning("A superseded period report request failed: {Details}", ErrorReporter.FormatForLog(ex));
                }
            }
            finally
            {
                if (_previewRequests.IsLatest(request))
                {
                    UseWaitCursor = false;
                }
            }
        }

        /// <summary>
        /// Builds the report for the current selection at the time of the click (never the preview that may
        /// belong to an earlier selection) and saves it; ignored while a previous export is pending.
        /// </summary>
        internal Task<bool> ExportAsync(ReportFormat format) =>
            this.RunExclusiveAsync(_export, async () =>
            {
                if (_userPicker.SelectedItem is not User user)
                {
                    MessageBox.Show(this, "Lütfen bir kullanıcı seçin.", "Uyarı", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    return;
                }

                var report = await _reportService.BuildAsync(user.Id, _from.Value, _to.Value);
                var path = ChoosePath(format, SuggestedFileName(report, format));
                if (string.IsNullOrEmpty(path))
                {
                    return;
                }

                await ReportExporter.ExportAsync(report, format, path);

                // The path is not logged: folder and file names can contain the user's name.
                Log.Information("Period report exported as {Format}", format);
                if (format == ReportFormat.Html && OfferToOpen(path))
                {
                    Process.Start(new ProcessStartInfo(path) { UseShellExecute = true })?.Dispose();
                }
                else if (format == ReportFormat.Csv)
                {
                    MessageBox.Show(this, "Rapor kaydedildi.", "Bilgi", MessageBoxButtons.OK, MessageBoxIcon.Information);
                }
            },
            [_csv, _html, _userPicker, _from, _to, .. _presets]);

        /// <summary>For example "saglik-raporu-2025-06-01_2025-06-07.csv"; no personal data in the name.</summary>
        internal static string SuggestedFileName(PeriodReport report, ReportFormat format)
        {
            ArgumentNullException.ThrowIfNull(report);

            return string.Format(
                CultureInfo.InvariantCulture,
                "saglik-raporu-{0:yyyy-MM-dd}_{1:yyyy-MM-dd}{2}",
                report.From,
                report.To,
                ReportExporter.Extension(format));
        }

        internal static string DescribePreview(PeriodReport report)
        {
            ArgumentNullException.ThrowIfNull(report);

            var culture = CultureInfo.CurrentCulture;
            var text = new StringBuilder();
            text.AppendLine(culture, $"Kullanıcı : {report.User.Name}");
            text.AppendLine(culture, $"Dönem     : {report.From:d} - {report.To:d} ({report.Days} gün)");
            text.AppendLine(culture, $"Ölçüm     : {report.Records.Count}");
            text.AppendLine();
            text.AppendLine(Summary("Kilo (kg)", report.Weight, culture));
            text.AppendLine(Summary("Nabız    ", report.Pulse, culture));
            text.AppendLine();
            text.AppendLine(report.Goal is null ? "Hedef tanımlı değil." : HedefForm.DescribeStatus(report.Goal));
            foreach (var alert in report.Goal?.Alerts ?? [])
            {
                text.AppendLine("  • " + GoalEvaluator.Describe(alert, culture));
            }

            return text.ToString();
        }

        private static string Summary(string name, MetricSummary? summary, CultureInfo culture) =>
            summary is null
                ? $"{name}: ölçüm yok"
                : string.Format(
                    culture,
                    "{0}: en düşük {1:0.#}, en yüksek {2:0.#}, ortalama {3:0.#}, ilk {4:0.#}, son {5:0.#}",
                    name,
                    summary.Minimum,
                    summary.Maximum,
                    summary.Average,
                    summary.First,
                    summary.Last);

        private async Task OnRangeChangedAsync()
        {
            // A preset sets both ends; one preview is enough.
            if (!_applyingPreset)
            {
                await ShowPreviewAsync();
            }
        }

        private Button Preset(string name, string text, Func<(DateTime First, DateTime Last)> range)
        {
            var button = FormLayout.Button(name, text);
            button.Click += async (_, _) =>
            {
                var (first, last) = range();
                _applyingPreset = true;
                try
                {
                    // Widen first so the pickers never hold a reversed range in between.
                    _from.Value = first < _from.Value ? first : _from.Value;
                    _to.Value = last;
                    _from.Value = first;
                }
                finally
                {
                    _applyingPreset = false;
                }

                await ShowPreviewAsync();
            };
            return button;
        }

        private string? AskForPath(ReportFormat format, string suggestedName)
        {
            using var dialog = new SaveFileDialog
            {
                FileName = suggestedName,
                Filter = format == ReportFormat.Csv ? "CSV dosyası (*.csv)|*.csv" : "HTML raporu (*.html)|*.html",
                OverwritePrompt = true,
                AddExtension = true,
            };
            return dialog.ShowDialog(this) == DialogResult.OK ? dialog.FileName : null;
        }

        private void BuildLayout()
        {
            FormLayout.ApplyDefaults(this, "Sağlık Takip | Dönem Raporu", new Size(760, 520));
            Name = nameof(DonemRaporuForm);

            var filters = FormLayout.Row(
                FormLayout.Caption("Kullanıcı"), _userPicker,
                FormLayout.Caption("Başlangıç"), _from,
                FormLayout.Caption("Bitiş"), _to);
            var presets = FormLayout.Row(_presets);

            var close = FormLayout.Button("btnKapat", "Kapat");
            close.Click += (_, _) => Close();
            var actions = FormLayout.Row(_csv, _html, close);
            actions.Dock = DockStyle.Bottom;

            Controls.Add(_preview);
            Controls.Add(actions);
            Controls.Add(presets);
            Controls.Add(filters);
            _preview.Text = NoUser;
        }
    }
}

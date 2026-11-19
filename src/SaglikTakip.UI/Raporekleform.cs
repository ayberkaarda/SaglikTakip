using System.Globalization;
using SaglikTakip.Business.Services;
using SaglikTakip.Domain.Entities;
using SaglikTakip.UI.Infrastructure;
using SaglikTakip.UI.Theming;
using Serilog;

namespace SaglikTakip.UI
{
    /// <summary>Shows the health records and exercises of the selected user.</summary>
    public sealed class Raporekleform : AppDialog
    {
        private const string Empty = "—";
        private const string Loading = "Yükleniyor…";
        private const string NoUserRows = "Kullanıcı seçtiğinizde kayıtlar burada listelenir.";

        private static readonly Dictionary<string, string> HealthRecordColumns = new()
        {
            [nameof(HealthRecord.Date)] = "Tarih",
            [nameof(HealthRecord.Weight)] = "Kilo",
            [nameof(HealthRecord.Pulse)] = "Nabız",
            [nameof(HealthRecord.Notes)] = "Notlar",
        };

        private static readonly Dictionary<string, string> ExerciseColumns = new()
        {
            [nameof(Exercise.Name)] = "Egzersiz Adı",
            [nameof(Exercise.DurationMinutes)] = "Süre (dk)",
            [nameof(Exercise.Repetitions)] = "Tekrar Sayısı",
        };

        private readonly IUserService _userService;
        private readonly IReportService _reportService;
        private readonly RequestSequence _reportRequests = new();

        private readonly ThemedComboBox comboBox1 = FormLayout.UserPicker("comboBox1");
        private readonly ThemedGrid dataSaglikKaydi = new() { Name = "dataSaglikKaydi", EmptyText = NoUserRows };
        private readonly ThemedGrid dataSporKaydi = new() { Name = "dataSporKaydi", EmptyText = NoUserRows };
        private readonly Label lblTarih = FormLayout.Value("lblTarih");
        private readonly Label lblKilo = FormLayout.Value("lblKilo");
        private readonly Label lblNabiz = FormLayout.Value("lblNabiz");
        private readonly Label lblNot = FormLayout.Text("lblNot");
        private readonly Label lblEgzersiz = FormLayout.Value("lblEgzersiz");
        private readonly Label lblSure = FormLayout.Value("lblSure");
        private readonly Label lblTekrar = FormLayout.Value("lblTekrar");

        public Raporekleform(IUserService userService, IReportService reportService)
            : base("Rapor Görüntüle", "Seçilen kişinin son ölçümü, son egzersizi ve tüm kayıtları.", Glyphs.Document, new Size(940, 640))
        {
            _userService = userService;
            _reportService = reportService;
            Load += async (_, _) =>
            {
                await this.RunAsync(() => comboBox1.LoadUsersAsync(_userService));
                comboBox1.SelectedIndexChanged += async (_, _) => await ShowSelectedUserAsync();
            };

            BuildLayout();
            ClearReport(Empty);
            SetFooterButtons(CreateCloseButton("btnkapat"));
            FinishLayout();
        }

        /// <summary>
        /// Loads the report of the selected user. The previous report is cleared at once, and a
        /// response is shown only while its request is still the latest one, so a slow response for an
        /// earlier selection can never overwrite the current one.
        /// </summary>
        internal async Task ShowSelectedUserAsync()
        {
            var request = _reportRequests.Next();
            if (comboBox1.SelectedItem is not User user)
            {
                ClearReport(Empty);
                UseWaitCursor = false;
                return;
            }

            ClearReport(Loading);
            UseWaitCursor = true;
            try
            {
                var report = await _reportService.GetUserReportAsync(user.Id);
                if (_reportRequests.IsLatest(request))
                {
                    ShowReport(report);
                }
            }
#pragma warning disable CA1031 // UI boundary: every failure is reported to the user.
            catch (Exception ex)
#pragma warning restore CA1031
            {
                if (_reportRequests.IsLatest(request))
                {
                    // Never leave a previous user's report on screen next to this user's name.
                    ClearReport(Empty);
                    ErrorReporter.Show(ex, this);
                }
                else
                {
                    // The user has already moved on; keep the failure for diagnostics only.
                    Log.Warning("A superseded report request failed: {Details}", ErrorReporter.FormatForLog(ex));
                }
            }
            finally
            {
                if (_reportRequests.IsLatest(request))
                {
                    UseWaitCursor = false;
                }
            }
        }

        private void ClearReport(string placeholder)
        {
            dataSaglikKaydi.DataSource = null;
            dataSporKaydi.DataSource = null;
            var rows = placeholder == Loading ? Loading : NoUserRows;
            dataSaglikKaydi.EmptyText = rows;
            dataSporKaydi.EmptyText = rows;

            foreach (var label in new[] { lblKilo, lblNabiz, lblNot, lblTarih, lblEgzersiz, lblSure, lblTekrar })
            {
                label.Text = placeholder;
            }
        }

        private void ShowReport(UserReport report)
        {
            dataSaglikKaydi.EmptyText = "Bu kişi için sağlık kaydı yok.";
            dataSporKaydi.EmptyText = "Bu kişi için egzersiz kaydı yok.";
            dataSaglikKaydi.DataSource = report.HealthRecords.ToList();
            dataSaglikKaydi.ConfigureColumns(HealthRecordColumns);
            if (dataSaglikKaydi.Columns[nameof(HealthRecord.Date)] is { } dateColumn)
            {
                dateColumn.DefaultCellStyle.Format = "d";
            }

            dataSporKaydi.DataSource = report.Exercises.ToList();
            dataSporKaydi.ConfigureColumns(ExerciseColumns);

            var record = report.LatestHealthRecord;
            lblKilo.Text = record?.Weight.ToString(CultureInfo.CurrentCulture) ?? Empty;
            lblNabiz.Text = record?.Pulse.ToString(CultureInfo.CurrentCulture) ?? Empty;
            lblNot.Text = record is null ? Empty : record.Notes ?? Empty;
            lblTarih.Text = record?.Date.ToString("dd MMMM yyyy", CultureInfo.CurrentCulture) ?? Empty;

            var exercise = report.LatestExercise;
            lblEgzersiz.Text = exercise?.Name ?? Empty;
            lblSure.Text = exercise?.DurationMinutes.ToString(CultureInfo.CurrentCulture) ?? Empty;
            lblTekrar.Text = exercise?.Repetitions.ToString(CultureInfo.CurrentCulture) ?? Empty;
        }

        private void BuildLayout()
        {
            var layout = new TableLayoutPanel { Name = "layout", Dock = DockStyle.Fill, ColumnCount = 2, RowCount = 3 };
            layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
            layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
            layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));

            var picker = FormLayout.Field("&Kullanıcı", comboBox1);
            picker.Dock = DockStyle.None;
            picker.Anchor = AnchorStyles.Left | AnchorStyles.Top;
            comboBox1.Width = 320;
            comboBox1.Anchor = AnchorStyles.Left;
            layout.Controls.Add(picker, 0, 0);
            layout.SetColumnSpan(picker, 2);

            var latestRecord = FormLayout.Card(
                "latestRecordCard",
                "Son sağlık kaydı",
                Stats(
                    [("Tarih", lblTarih), ("Kilo (kg)", lblKilo), ("Nabız (atım/dk)", lblNabiz)],
                    ("Notlar", lblNot)),
                fill: false);
            var latestExercise = FormLayout.Card(
                "latestExerciseCard",
                "Son egzersiz",
                Stats([("Egzersiz", lblEgzersiz), ("Süre (dk)", lblSure), ("Tekrar", lblTekrar)], null),
                fill: false);
            AddCell(layout, latestRecord, 0, 1);
            AddCell(layout, latestExercise, 1, 1);
            AddCell(layout, FormLayout.Card("healthRecordsCard", "Sağlık kayıtları", dataSaglikKaydi), 0, 2);
            AddCell(layout, FormLayout.Card("exercisesCard", "Egzersizler", dataSporKaydi), 1, 2);
            Content.Controls.Add(layout);
        }

        private static void AddCell(TableLayoutPanel layout, Control card, int column, int row)
        {
            card.Dock = DockStyle.Fill;
            card.Margin = new Padding(column == 0 ? 0 : Spacing.S, 0, column == 0 ? Spacing.S : 0, Spacing.L);
            layout.Controls.Add(card, column, row);
        }

        /// <summary>Caption-over-value pairs in one row, and an optional full-width text below.</summary>
        private static TableLayoutPanel Stats((string Caption, Label Value)[] values, (string Caption, Label Value)? note)
        {
            var grid = new TableLayoutPanel
            {
                AutoSize = true,
                AutoSizeMode = AutoSizeMode.GrowAndShrink,
                ColumnCount = values.Length,
                Margin = Padding.Empty,
            };
            // The first value (a date or a name) is the widest.
            for (var i = 0; i < values.Length; i++)
            {
                grid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, i == 0 ? 44 : 56F / (values.Length - 1)));
            }

            for (var i = 0; i < values.Length; i++)
            {
                grid.Controls.Add(Stat(values[i].Caption, values[i].Value), i, 0);
            }

            if (note is { } text)
            {
                var stat = Stat(text.Caption, text.Value);
                stat.Margin = new Padding(0, Spacing.M, 0, 0);
                grid.Controls.Add(stat, 0, 1);
                grid.SetColumnSpan(stat, values.Length);
            }

            return grid;
        }

        private static TableLayoutPanel Stat(string caption, Label value)
        {
            var stat = new TableLayoutPanel
            {
                AutoSize = true,
                AutoSizeMode = AutoSizeMode.GrowAndShrink,
                ColumnCount = 1,
                Dock = DockStyle.Fill,
                Margin = Padding.Empty,
            };
            var label = FormLayout.Hint(caption);
            label.Margin = new Padding(0, 0, 0, Spacing.XS);
            value.AutoEllipsis = true;
            value.AccessibleName = caption;
            stat.Controls.Add(label);
            stat.Controls.Add(value);
            return stat;
        }
    }
}

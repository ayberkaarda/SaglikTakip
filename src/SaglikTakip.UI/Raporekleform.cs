using System.Diagnostics;
using System.Globalization;
using SaglikTakip.Business.Services;
using SaglikTakip.Domain.Entities;
using SaglikTakip.UI.Infrastructure;

namespace SaglikTakip.UI
{
    /// <summary>Shows the health records and exercises of the selected user.</summary>
    public partial class Raporekleform : Form
    {
        private const string Empty = "—";
        private const string Loading = "Yükleniyor…";

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

        public Raporekleform(IUserService userService, IReportService reportService)
        {
            _userService = userService;
            _reportService = reportService;
            InitializeComponent();
        }

        private async void Raporekleform_Load(object sender, EventArgs e)
        {
            lblTarih.Text = " ";
            lblKilo.Text = " ";
            lblNabiz.Text = " ";
            lblNot.Text = " ";
            lblEgzersiz.Text = " ";
            lblSure.Text = " ";
            lblTekrar.Text = " ";

            await this.RunAsync(() => comboBox1.LoadUsersAsync(_userService));
            comboBox1.SelectedIndexChanged += comboBox1_SelectedIndexChanged;
        }

        private async void comboBox1_SelectedIndexChanged(object? sender, EventArgs e)
        {
            await ShowSelectedUserAsync();
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
                ClearReport(string.Empty);
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
                    Trace.TraceWarning(ErrorReporter.FormatForLog(ex));
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

            foreach (var label in new[] { lblKilo, lblNabiz, lblNot, lblTarih, lblEgzersiz, lblSure, lblTekrar })
            {
                label.Text = placeholder;
            }
        }

        private void ShowReport(UserReport report)
        {
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

        private void btnkapat_Click(object sender, EventArgs e)
        {
            Close();
        }

        private void btnAsagial_Click(object sender, EventArgs e)
        {
            WindowState = FormWindowState.Minimized;
        }
    }
}

using System.Globalization;
using SaglikTakip.Business;
using SaglikTakip.Business.Insights;
using SaglikTakip.Business.Services;
using SaglikTakip.Domain.Entities;
using SaglikTakip.UI.Infrastructure;
using Serilog;

namespace SaglikTakip.UI
{
    /// <summary>Weight or pulse of the selected user over a date range, with summary and moving average.</summary>
    public sealed class TrendForm : Form
    {
        internal const string Loading = "Yükleniyor…";
        internal const string NoUser = "Grafik için bir kullanıcı seçin.";

        private readonly IUserService _userService;
        private readonly ITrendService _trendService;
        private readonly IGoalService _goalService;
        private readonly RequestSequence _trendRequests = new();

        private readonly ComboBox _userPicker = FormLayout.UserPicker();
        private readonly ComboBox _metricPicker = new() { Name = "metricPicker", DropDownStyle = ComboBoxStyle.DropDownList, Width = 90 };
        private readonly DateTimePicker _from;
        private readonly DateTimePicker _to;
        private readonly NumericUpDown _window = new()
        {
            Name = "windowInput",
            Minimum = TrendCalculator.MinWindow,
            Maximum = TrendCalculator.MaxWindow,
            Value = 7,
            Width = 60,
        };

        private readonly TrendChart _chart = new() { Name = "trendChart", Dock = DockStyle.Fill };
        private readonly Label _summary = FormLayout.Value("summaryLabel");
        private readonly Label _goal = FormLayout.Value("goalLabel");
        private readonly ListBox _alerts = new() { Name = "alertList", Dock = DockStyle.Fill, IntegralHeight = false };

        public TrendForm(IUserService userService, ITrendService trendService, IGoalService goalService)
        {
            _userService = userService;
            _trendService = trendService;
            _goalService = goalService;

            var today = DateTime.Today;
            _from = FormLayout.DatePicker("fromPicker", today.AddDays(-29));
            _to = FormLayout.DatePicker("toPicker", today);
            _metricPicker.Items.AddRange([new MetricOption(HealthMetric.Weight, "Kilo", "kg"), new MetricOption(HealthMetric.Pulse, "Nabız", "atım/dk")]);
            _metricPicker.SelectedIndex = 0;

            BuildLayout();

            Load += async (_, _) => await this.RunAsync(() => _userPicker.LoadUsersAsync(_userService));
            _userPicker.SelectedIndexChanged += async (_, _) => await ShowTrendAsync();
            _metricPicker.SelectedIndexChanged += async (_, _) => await ShowTrendAsync();
            _from.ValueChanged += async (_, _) => await ShowTrendAsync();
            _to.ValueChanged += async (_, _) => await ShowTrendAsync();
            _window.ValueChanged += async (_, _) => await ShowTrendAsync();
        }

        /// <summary>
        /// Loads the trend for the current selection. The previous chart is cleared at once and a response
        /// is applied only while it belongs to the latest request, so a slow answer for an earlier user or
        /// range never replaces the current one.
        /// </summary>
        internal async Task ShowTrendAsync()
        {
            var request = _trendRequests.Next();
            if (_userPicker.SelectedItem is not User user)
            {
                ShowMessage(NoUser);
                UseWaitCursor = false;
                return;
            }

            var metric = (MetricOption)_metricPicker.SelectedItem!;
            ShowMessage(Loading);
            UseWaitCursor = true;
            try
            {
                var trendTask = _trendService.GetTrendAsync(user.Id, metric.Metric, _from.Value, _to.Value, (int)_window.Value);
                var goalTask = _goalService.GetStatusAsync(user.Id, _from.Value, _to.Value);
                await Task.WhenAll(trendTask, goalTask);
                if (_trendRequests.IsLatest(request))
                {
                    ShowTrend(await trendTask, await goalTask, metric);
                }
            }
            catch (BusinessRuleException ex) when (_trendRequests.IsLatest(request))
            {
                // For example a start date after the end date while the user is still picking: no dialog.
                ShowMessage(ex.Message);
            }
#pragma warning disable CA1031 // UI boundary: every failure is reported to the user.
            catch (Exception ex)
#pragma warning restore CA1031
            {
                if (_trendRequests.IsLatest(request))
                {
                    ShowMessage("—");
                    ErrorReporter.Show(ex, this);
                }
                else
                {
                    Log.Warning("A superseded trend request failed: {Details}", ErrorReporter.FormatForLog(ex));
                }
            }
            finally
            {
                if (_trendRequests.IsLatest(request))
                {
                    UseWaitCursor = false;
                }
            }
        }

        private void ShowMessage(string text)
        {
            _chart.Clear(text);
            _summary.Text = string.Empty;
            _goal.Text = string.Empty;
            _alerts.Items.Clear();
        }

        private void ShowTrend(MetricTrend trend, GoalStatus? status, MetricOption metric)
        {
            var goal = status?.Goal;
            var (lower, upper) = metric.Metric == HealthMetric.Weight
                ? (goal?.MinWeight, goal?.MaxWeight)
                : ((double?)goal?.MinPulse, (double?)goal?.MaxPulse);

            _chart.EmptyText = "Seçilen aralıkta ölçüm yok.";
            _chart.SetData(trend.Points, lower, upper);
            _summary.Text = DescribeSummary(trend, metric.Unit);
            _goal.Text = status is null
                ? "Hedef tanımlı değil."
                : HedefForm.DescribeStatus(status);

            _alerts.BeginUpdate();
            try
            {
                _alerts.Items.Clear();
                foreach (var alert in status?.Alerts.Where(a => a.Metric == metric.Metric) ?? [])
                {
                    _alerts.Items.Add(GoalEvaluator.Describe(alert, CultureInfo.CurrentCulture));
                }
            }
            finally
            {
                _alerts.EndUpdate();
            }
        }

        internal static string DescribeSummary(MetricTrend trend, string unit)
        {
            ArgumentNullException.ThrowIfNull(trend);

            if (trend.Summary is not { } s)
            {
                return "Seçilen aralıkta ölçüm yok.";
            }

            var culture = CultureInfo.CurrentCulture;
            var lastAverage = trend.Points.Count > 0 ? trend.Points[^1].MovingAverage : null;
            return string.Format(
                culture,
                "Ölçüm: {0}   En düşük: {1:0.#} {5}   En yüksek: {2:0.#} {5}   Ortalama: {3:0.#} {5}   Son {6} ölçüm gününün ortalaması: {4}",
                s.Count,
                s.Minimum,
                s.Maximum,
                s.Average,
                lastAverage.HasValue ? lastAverage.Value.ToString("0.#", culture) + " " + unit : "yetersiz veri",
                unit,
                trend.MovingAverageWindow);
        }

        private void BuildLayout()
        {
            FormLayout.ApplyDefaults(this, "Sağlık Takip | Trend Grafikleri", new Size(860, 520));
            Name = nameof(TrendForm);

            var filters = FormLayout.Row(
                FormLayout.Caption("Kullanıcı"), _userPicker,
                FormLayout.Caption("Ölçüm"), _metricPicker,
                FormLayout.Caption("Başlangıç"), _from,
                FormLayout.Caption("Bitiş"), _to,
                FormLayout.Caption("Hareketli ortalama (ölçüm günü)"), _window);

            var summaryRow = FormLayout.Row(_summary, _goal);
            summaryRow.FlowDirection = FlowDirection.TopDown;
            summaryRow.Dock = DockStyle.Bottom;

            var alertPanel = new Panel { Dock = DockStyle.Right, Width = 260, Padding = new Padding(4) };
            var alertCaption = FormLayout.Caption("Hedef sınırı dışındaki ölçümler");
            alertCaption.Dock = DockStyle.Top;
            alertPanel.Controls.Add(_alerts);
            alertPanel.Controls.Add(alertCaption);

            Controls.Add(_chart);
            Controls.Add(alertPanel);
            Controls.Add(summaryRow);
            Controls.Add(filters);
            _chart.Clear(NoUser);
        }

        private sealed record MetricOption(HealthMetric Metric, string Text, string Unit)
        {
            public override string ToString() => Text;
        }
    }
}

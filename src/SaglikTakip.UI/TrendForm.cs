using System.Globalization;
using SaglikTakip.Business;
using SaglikTakip.Business.Insights;
using SaglikTakip.Business.Services;
using SaglikTakip.Domain.Entities;
using SaglikTakip.UI.Infrastructure;
using SaglikTakip.UI.Theming;
using Serilog;

namespace SaglikTakip.UI
{
    /// <summary>Weight or pulse of the selected user over a date range, with summary and moving average.</summary>
    public sealed class TrendForm : AppDialog
    {
        internal const string Loading = "Yükleniyor…";
        internal const string NoUser = "Grafik için bir kullanıcı seçin.";
        internal const string NoData = "Seçilen aralıkta ölçüm yok.";
        private const string Dash = "—";

        private readonly IUserService _userService;
        private readonly ITrendService _trendService;
        private readonly IGoalService _goalService;
        private readonly RequestSequence _trendRequests = new();

        private readonly ComboBox _userPicker = FormLayout.UserPicker();
        private readonly ComboBox _metricPicker = FormLayout.Picker("metricPicker", 120);
        private readonly DateTimePicker _from;
        private readonly DateTimePicker _to;
        private readonly NumericUpDown _window = new()
        {
            Name = "windowInput",
            Minimum = TrendCalculator.MinWindow,
            Maximum = TrendCalculator.MaxWindow,
            Value = 7,
            Width = 80,
            TextAlign = HorizontalAlignment.Right,
        };

        private readonly TrendChart _chart = new() { Name = "trendChart", Dock = DockStyle.Fill };
        private readonly Label _count = FormLayout.Value("countValue");
        private readonly Label _minimum = FormLayout.Value("minimumValue");
        private readonly Label _maximum = FormLayout.Value("maximumValue");
        private readonly Label _average = FormLayout.Value("averageValue");
        private readonly Label _moving = FormLayout.Value("movingAverageValue");
        private readonly Label _movingCaption = FormLayout.Hint("Son ölçüm günlerinin ortalaması");
        private readonly Label _goal = FormLayout.Text("goalLabel");
        private readonly Label _noAlerts = FormLayout.Hint("Sınır dışı ölçüm yok.");
        private readonly ListBox _alerts = new() { Name = "alertList", Dock = DockStyle.Fill, IntegralHeight = false };

        public TrendForm(IUserService userService, ITrendService trendService, IGoalService goalService)
            : base("Trend Grafikleri", "Kilo ve nabzın seyri, hareketli ortalama ve hedef aralığıyla.", Glyphs.Chart, new Size(1040, 700))
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
            ShowMessage(Loading, loading: true);
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
                    ShowMessage(Dash);
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

        private void ShowMessage(string text, bool loading = false)
        {
            if (loading)
            {
                _chart.ShowLoading(text);
            }
            else
            {
                _chart.Clear(text);
            }

            _chart.AccessibleDescription = text;
            foreach (var value in new[] { _count, _minimum, _maximum, _average, _moving })
            {
                value.Text = Dash;
            }

            _goal.Text = string.Empty;
            SetAlerts([]);
        }

        private void ShowTrend(MetricTrend trend, GoalStatus? status, MetricOption metric)
        {
            var goal = status?.Goal;
            var (lower, upper) = metric.Metric == HealthMetric.Weight
                ? (goal?.MinWeight, goal?.MaxWeight)
                : ((double?)goal?.MinPulse, (double?)goal?.MaxPulse);

            _chart.EmptyText = NoData;
            _chart.Unit = metric.Unit;
            _chart.SetData(trend.Points, lower, upper);
            _chart.AccessibleDescription = DescribeSummary(trend, metric.Unit);
            ShowStats(trend, metric.Unit);
            _goal.Text = status is null
                ? "Hedef tanımlı değil. Hedefler ekranından hedef ve uyarı sınırları ekleyebilirsiniz."
                : HedefForm.DescribeStatus(status).Replace("   ", Environment.NewLine, StringComparison.Ordinal);
            SetAlerts(status?.Alerts.Where(a => a.Metric == metric.Metric).Select(a => GoalEvaluator.Describe(a, CultureInfo.CurrentCulture)) ?? []);
        }

        private void ShowStats(MetricTrend trend, string unit)
        {
            var culture = CultureInfo.CurrentCulture;
            _movingCaption.Text = string.Format(culture, "Son {0} ölçüm gününün ortalaması", trend.MovingAverageWindow);
            if (trend.Summary is not { } s)
            {
                foreach (var value in new[] { _minimum, _maximum, _average, _moving })
                {
                    value.Text = Dash;
                }

                _count.Text = "0";
                return;
            }

            string Format(double value) => value.ToString("0.#", culture) + " " + unit;
            var lastAverage = trend.Points.Count > 0 ? trend.Points[^1].MovingAverage : null;
            _count.Text = s.Count.ToString(culture);
            _minimum.Text = Format(s.Minimum);
            _maximum.Text = Format(s.Maximum);
            _average.Text = Format(s.Average);
            _moving.Text = lastAverage.HasValue ? Format(lastAverage.Value) : "yetersiz veri";
        }

        private void SetAlerts(IEnumerable<string> alerts)
        {
            _alerts.BeginUpdate();
            try
            {
                _alerts.Items.Clear();
                foreach (var alert in alerts)
                {
                    _alerts.Items.Add(alert);
                }
            }
            finally
            {
                _alerts.EndUpdate();
            }

            _noAlerts.Visible = _alerts.Items.Count == 0;
        }

        internal static string DescribeSummary(MetricTrend trend, string unit)
        {
            ArgumentNullException.ThrowIfNull(trend);

            if (trend.Summary is not { } s)
            {
                return NoData;
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
            Name = nameof(TrendForm);

            var filters = new FlowLayoutPanel
            {
                Name = "filters",
                AutoSize = true,
                AutoSizeMode = AutoSizeMode.GrowAndShrink,
                WrapContents = true,
                Dock = DockStyle.Top,
                Margin = Padding.Empty,
            };
            _userPicker.Width = 280;
            foreach (var field in new[]
            {
                FormLayout.Field("&Kullanıcı", _userPicker),
                FormLayout.Field("Ö&lçüm", _metricPicker),
                FormLayout.Field("&Başlangıç", _from),
                FormLayout.Field("Bi&tiş", _to),
                FormLayout.Field("&Ortalama (ölçüm günü)", _window),
            })
            {
                field.Dock = DockStyle.None;
                field.Margin = new Padding(0, 0, Spacing.L, 0);
                filters.Controls.Add(field);
            }

            var stats = new TableLayoutPanel
            {
                Name = "stats",
                AutoSize = true,
                AutoSizeMode = AutoSizeMode.GrowAndShrink,
                ColumnCount = 5,
                Dock = DockStyle.Top,
                Margin = Padding.Empty,
                Padding = new Padding(0, Spacing.L, 0, 0),
            };
            (Label Caption, Label Value)[] items =
            [
                (FormLayout.Hint("Ölçüm sayısı"), _count),
                (FormLayout.Hint("En düşük"), _minimum),
                (FormLayout.Hint("En yüksek"), _maximum),
                (FormLayout.Hint("Ortalama"), _average),
                (_movingCaption, _moving),
            ];
            for (var i = 0; i < items.Length; i++)
            {
                stats.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 20));
                items[i].Caption.Margin = new Padding(0, 0, Spacing.S, Spacing.XS);
                items[i].Value.AccessibleName = items[i].Caption.Text;
                stats.Controls.Add(items[i].Caption, i, 0);
                stats.Controls.Add(items[i].Value, i, 1);
            }

            var top = FormLayout.Stack("filterStack", filters, stats);
            var filterCard = FormLayout.Card("filterCard", null, top, fill: false);

            _goal.AutoSize = false;
            _goal.Dock = DockStyle.Fill;
            var goalCard = FormLayout.Card("goalCard", "Hedef durumu", _goal);
            goalCard.Dock = DockStyle.Top;
            goalCard.Height = 168;

            _noAlerts.Dock = DockStyle.Top;
            var alertBody = new Panel { Dock = DockStyle.Fill };
            alertBody.Controls.Add(_alerts);
            alertBody.Controls.Add(_noAlerts);
            var alertCard = FormLayout.Card("alertCard", "Sınır dışı ölçümler", alertBody);
            alertCard.Margin = Padding.Empty;

            var side = new Panel { Name = "side", Dock = DockStyle.Fill, Margin = new Padding(Spacing.S, 0, 0, Spacing.L) };
            side.Controls.Add(alertCard);
            side.Controls.Add(goalCard);

            var chartCard = FormLayout.Card("chartCard", null, _chart);
            chartCard.Margin = new Padding(0, 0, Spacing.S, Spacing.L);
            chartCard.Padding = new Padding(Spacing.S, Spacing.M, Spacing.M, Spacing.S);

            var main = new TableLayoutPanel { Name = "main", Dock = DockStyle.Fill, ColumnCount = 2, RowCount = 1, Margin = Padding.Empty };
            main.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            main.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 290));
            main.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            main.Controls.Add(chartCard, 0, 0);
            main.Controls.Add(side, 1, 0);

            Content.Controls.Add(FormLayout.Page((filterCard, false), (main, true)));

            SetFooterButtons(CreateCloseButton());
            ShowMessage(NoUser);
            FinishLayout();
        }

        private sealed record MetricOption(HealthMetric Metric, string Text, string Unit)
        {
            public override string ToString() => Text;
        }
    }
}

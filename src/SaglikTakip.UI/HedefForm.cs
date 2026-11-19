using System.Globalization;
using SaglikTakip.Business.Insights;
using SaglikTakip.Business.Services;
using SaglikTakip.Business.Validation;
using SaglikTakip.Domain.Entities;
using SaglikTakip.UI.Infrastructure;
using Serilog;

namespace SaglikTakip.UI
{
    /// <summary>Goal weight, alert limits and goal progress of the selected user.</summary>
    public sealed class HedefForm : Form
    {
        internal const string NoUser = "Bir kullanıcı seçin.";
        internal const string Loading = "Yükleniyor…";

        private readonly IUserService _userService;
        private readonly IGoalService _goalService;
        private readonly RequestSequence _goalRequests = new();
        private readonly ExclusiveOperation _write = new();

        private readonly ComboBox _userPicker = FormLayout.UserPicker();
        private readonly OptionalNumberInput _targetWeight = new("targetWeight", 1, 200, 1, 70);
        private readonly OptionalNumberInput _startWeight = new("startWeight", 1, 200, 1, 70);
        private readonly OptionalNumberInput _minWeight = new("minWeight", 1, 200, 1, 50);
        private readonly OptionalNumberInput _maxWeight = new("maxWeight", 1, 200, 1, 90);
        private readonly OptionalNumberInput _minPulse = new("minPulse", 1, 250, 0, 50);
        private readonly OptionalNumberInput _maxPulse = new("maxPulse", 1, 250, 0, 100);
        private readonly Label _status = FormLayout.Value("statusLabel");
        private readonly Button _save = FormLayout.Button("btnKaydet", "Kaydet");
        private readonly Button _delete = FormLayout.Button("btnSil", "Hedefi Sil");
        private readonly TableLayoutPanel _editor;

        /// <summary>User whose goal is on screen; <see langword="null"/> while loading or after a failure.</summary>
        private int? _loadedUserId;

        public HedefForm(IUserService userService, IGoalService goalService)
        {
            _userService = userService;
            _goalService = goalService;
            _editor = FormLayout.Grid(
                ("Hedef kilo (kg)", _targetWeight),
                ("Başlangıç kilosu (kg)", _startWeight),
                ("En düşük kilo (kg)", _minWeight),
                ("En yüksek kilo (kg)", _maxWeight),
                ("En düşük nabız", _minPulse),
                ("En yüksek nabız", _maxPulse));
            BuildLayout();
            SetEditorEnabled(false);

            Load += async (_, _) => await this.RunAsync(() => _userPicker.LoadUsersAsync(_userService));
            _userPicker.SelectedIndexChanged += async (_, _) => await LoadSelectedGoalAsync();
            _save.Click += async (_, _) => await SaveAsync();
            _delete.Click += async (_, _) => await DeleteAsync();
        }

        /// <summary>
        /// Loads the goal of the selected user. The editor is cleared and locked at once and unlocked only
        /// when the latest request answers, so a late answer for a previous user can neither be shown nor
        /// be saved under the current user.
        /// </summary>
        internal async Task LoadSelectedGoalAsync()
        {
            var request = _goalRequests.Next();
            _loadedUserId = null;
            SetEditorEnabled(false);
            ShowGoal(null);

            if (_userPicker.SelectedItem is not User user)
            {
                _status.Text = NoUser;
                UseWaitCursor = false;
                return;
            }

            _status.Text = Loading;
            UseWaitCursor = true;
            try
            {
                var status = await _goalService.GetStatusAsync(user.Id, DayRanges.Earliest, DateTime.Today);
                if (_goalRequests.IsLatest(request))
                {
                    ShowGoal(status?.Goal);
                    _status.Text = DescribeStatus(status);
                    _loadedUserId = user.Id;
                    SetEditorEnabled(true);
                }
            }
#pragma warning disable CA1031 // UI boundary: every failure is reported to the user.
            catch (Exception ex)
#pragma warning restore CA1031
            {
                if (_goalRequests.IsLatest(request))
                {
                    _status.Text = "—";
                    ErrorReporter.Show(ex, this);
                }
                else
                {
                    Log.Warning("A superseded goal request failed: {Details}", ErrorReporter.FormatForLog(ex));
                }
            }
            finally
            {
                if (_goalRequests.IsLatest(request))
                {
                    UseWaitCursor = false;
                }
            }
        }

        /// <summary>Saves the entered goal for the loaded user; ignored while a previous write is pending.</summary>
        internal async Task SaveAsync()
        {
            var saved = false;
            await this.RunExclusiveAsync(_write, async () =>
            {
                if (_loadedUserId is not { } userId)
                {
                    return;
                }

                await _goalService.SaveAsync(userId, ReadInput());
                saved = true;
                MessageBox.Show(this, "Hedef kaydedildi.", "Bilgi", MessageBoxButtons.OK, MessageBoxIcon.Information);
            },
            _save, _delete, _userPicker, _editor);

            if (saved)
            {
                await LoadSelectedGoalAsync();
            }
        }

        /// <summary>Deletes the loaded user's goal after a confirmation; ignored while a write is pending.</summary>
        internal async Task DeleteAsync()
        {
            var deleted = false;
            await this.RunExclusiveAsync(_write, async () =>
            {
                if (_loadedUserId is not { } userId)
                {
                    return;
                }

                var answer = MessageBox.Show(
                    this, "Bu kullanıcının hedefi silinsin mi?", "Onay", MessageBoxButtons.YesNo, MessageBoxIcon.Question);
                if (answer != DialogResult.Yes)
                {
                    return;
                }

                deleted = await _goalService.DeleteAsync(userId);
            },
            _save, _delete, _userPicker, _editor);

            if (deleted)
            {
                await LoadSelectedGoalAsync();
            }
        }

        internal GoalInput ReadInput() =>
            new(_targetWeight.Value, _startWeight.Value, _minWeight.Value, _maxWeight.Value, _minPulse.IntValue, _maxPulse.IntValue);

        internal static string DescribeStatus(GoalStatus? status)
        {
            if (status is null)
            {
                return "Bu kullanıcı için hedef tanımlı değil.";
            }

            var culture = CultureInfo.CurrentCulture;
            var current = status.CurrentWeight is { } weight
                ? string.Format(culture, "Güncel kilo: {0:0.#} kg ({1:d})", weight, status.CurrentWeightDate)
                : "Güncel kilo: ölçüm yok";
            var progress = status.ProgressPercent is { } percent
                ? string.Format(culture, "Hedefe ilerleme: %{0:0}", percent)
                : "Hedefe ilerleme: hesaplanamadı (başlangıç ve hedef kilo gerekir)";
            return string.Format(culture, "{0}   {1}   Sınır dışı ölçüm: {2}", current, progress, status.Alerts.Count);
        }

        private void ShowGoal(HealthGoal? goal)
        {
            _targetWeight.Value = (decimal?)goal?.TargetWeight;
            _startWeight.Value = (decimal?)goal?.StartWeight;
            _minWeight.Value = (decimal?)goal?.MinWeight;
            _maxWeight.Value = (decimal?)goal?.MaxWeight;
            _minPulse.Value = goal?.MinPulse;
            _maxPulse.Value = goal?.MaxPulse;
        }

        private void SetEditorEnabled(bool enabled)
        {
            _editor.Enabled = enabled;
            _save.Enabled = enabled;
            _delete.Enabled = enabled;
        }

        private void BuildLayout()
        {
            FormLayout.ApplyDefaults(this, "Sağlık Takip | Hedefler", new Size(620, 360));
            Name = nameof(HedefForm);

            var close = FormLayout.Button("btnKapat", "Kapat");
            close.Click += (_, _) => Close();
            var buttons = FormLayout.Row(_save, _delete, close);
            buttons.Dock = DockStyle.Bottom;
            var hint = FormLayout.Row(FormLayout.Caption(
                "İşaretlenmeyen değerler boş kalır. Sınırlar dahildir: sınıra eşit ölçüm uyarı vermez."));

            var statusRow = FormLayout.Row(_status);
            Controls.Add(statusRow);
            Controls.Add(hint);
            Controls.Add(_editor);
            Controls.Add(FormLayout.Row(FormLayout.Caption("Kullanıcı"), _userPicker));
            Controls.Add(buttons);
            _status.Text = NoUser;
        }
    }
}

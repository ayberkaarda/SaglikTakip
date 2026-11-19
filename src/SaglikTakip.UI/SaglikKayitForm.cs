using System.Globalization;
using SaglikTakip.Business.Insights;
using SaglikTakip.Business.Services;
using SaglikTakip.Domain.Entities;
using SaglikTakip.UI.Infrastructure;
using Serilog;

namespace SaglikTakip.UI
{
    /// <summary>Adds a weight and pulse measurement for a user.</summary>
    public partial class SaglikKayitForm : Form
    {
        private readonly IUserService _userService;
        private readonly IHealthRecordService _healthRecordService;
        private readonly IGoalService _goalService;
        private readonly ExclusiveOperation _save = new();

        public SaglikKayitForm(IUserService userService, IHealthRecordService healthRecordService, IGoalService goalService)
        {
            _userService = userService;
            _healthRecordService = healthRecordService;
            _goalService = goalService;
            InitializeComponent();
        }

        private async void SaglikKayitForm_Load(object sender, EventArgs e)
        {
            await this.RunAsync(() => comboBox1.LoadUsersAsync(_userService));
        }

        private async void btnKaydet_Click(object sender, EventArgs e)
        {
            await SaveAsync();
        }

        /// <summary>Saves the entered measurement; ignored while a previous save is still pending.</summary>
        internal Task SaveAsync()
        {
            return this.RunExclusiveAsync(_save, async () =>
            {
                if (comboBox1.SelectedItem is not User user)
                {
                    MessageBox.Show(this, "Lütfen bir kullanıcı seçin.", "Uyarı", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    return;
                }

                var record = await _healthRecordService.AddAsync(
                    user.Id,
                    dateTimePicker1.Value,
                    numericUpDown1.Value,
                    (int)numericUpDown2.Value,
                    textBox1.Text);

                var alerts = await CheckGoalAsync(record);
                if (alerts.Count == 0)
                {
                    MessageBox.Show(this, "Sağlık kaydı eklendi.", "Bilgi", MessageBoxButtons.OK, MessageBoxIcon.Information);
                }
                else
                {
                    MessageBox.Show(this, DescribeAlerts(alerts), "Uyarı", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                }
            },
            btnKaydet, btnkapat, comboBox1, dateTimePicker1, numericUpDown1, numericUpDown2, textBox1);
        }

        internal static string DescribeAlerts(IReadOnlyList<ThresholdAlert> alerts) =>
            "Sağlık kaydı eklendi, ancak ölçüm hedef sınırlarının dışında:" + Environment.NewLine
            + string.Join(Environment.NewLine, alerts.Select(a => "• " + GoalEvaluator.Describe(a, CultureInfo.CurrentCulture)));

        /// <summary>
        /// The record is already saved here, so a failing goal check must not look like a failed save:
        /// it is logged and the normal confirmation is shown.
        /// </summary>
        private async Task<IReadOnlyList<ThresholdAlert>> CheckGoalAsync(HealthRecord record)
        {
            try
            {
                return await _goalService.CheckAsync(record);
            }
#pragma warning disable CA1031 // The save succeeded; the goal check is optional.
            catch (Exception ex)
#pragma warning restore CA1031
            {
                Log.Warning("Goal check after saving a health record failed: {Details}", ErrorReporter.FormatForLog(ex));
                return [];
            }
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

using System.Globalization;
using SaglikTakip.Business.Insights;
using SaglikTakip.Business.Services;
using SaglikTakip.Domain.Entities;
using SaglikTakip.UI.Infrastructure;
using SaglikTakip.UI.Theming;
using Serilog;

namespace SaglikTakip.UI
{
    /// <summary>Adds a weight and pulse measurement for a user.</summary>
    public sealed class SaglikKayitForm : AppDialog
    {
        private readonly IUserService _userService;
        private readonly IHealthRecordService _healthRecordService;
        private readonly IGoalService _goalService;
        private readonly ExclusiveOperation _save = new();

        private readonly ThemedComboBox comboBox1 = FormLayout.UserPicker("comboBox1");
        private readonly DateTimePicker dateTimePicker1 = new() { Name = "dateTimePicker1", Format = DateTimePickerFormat.Short, Width = 140 };
        private readonly NumericUpDown numericUpDown1 = new() { Name = "numericUpDown1", Maximum = 200, Width = 120, TextAlign = HorizontalAlignment.Right };
        private readonly NumericUpDown numericUpDown2 = new() { Name = "numericUpDown2", Maximum = 250, Width = 120, TextAlign = HorizontalAlignment.Right };
        private readonly TextBox textBox1 = new()
        {
            Name = "textBox1",
            MaxLength = 500,
            Multiline = true,
            AcceptsReturn = true,
            Height = 76,

            // The preferred height of a multiline box does not follow Height; the layout uses these limits.
            MinimumSize = new Size(0, 76),
            MaximumSize = new Size(0, 76),
            PlaceholderText = "İsteğe bağlı, en fazla 500 karakter",
        };

        private readonly Button btnKaydet = FormLayout.Button("btnKaydet", "Kaydet", ButtonKind.Primary);
        private readonly Button btnkapat;

        public SaglikKayitForm(IUserService userService, IHealthRecordService healthRecordService, IGoalService goalService)
            : base("Sağlık Kaydı Ekle", "Ölçümü kaydedin; hedef sınırlarının dışındaysa uyarı alırsınız.", Glyphs.Heart, new Size(620, 600))
        {
            _userService = userService;
            _healthRecordService = healthRecordService;
            _goalService = goalService;
            btnkapat = CreateCloseButton("btnkapat");
            btnKaydet.Click += async (_, _) => await SaveAsync();
            Load += async (_, _) => await this.RunAsync(() => comboBox1.LoadUsersAsync(_userService));

            var fields = FormLayout.Fields(
                "fields",
                [FormLayout.Field("&Kullanıcı", comboBox1)],
                [FormLayout.Field("&Tarih", dateTimePicker1), FormLayout.Field("K&ilo (kg)", numericUpDown1), FormLayout.Field("&Nabız (atım/dk)", numericUpDown2)],
                [FormLayout.Field("N&otlar", textBox1)]);
            Content.Controls.Add(FormLayout.Page((FormLayout.Card("recordCard", "Ölçüm", fields, fill: false), false)));
            SetFooterButtons(btnKaydet, btnkapat);
            AcceptButton = btnKaydet;
            FinishLayout();
        }

        /// <summary>Saves the entered measurement; ignored while a previous save is still pending.</summary>
        internal Task SaveAsync()
        {
            return this.RunExclusiveAsync(_save, async () =>
            {
                if (comboBox1.SelectedItem is not User user)
                {
                    ShowStatus(StatusKind.Warning, "Lütfen bir kullanıcı seçin.");
                    _save.FocusAfterUnlock(comboBox1);
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
                    ShowStatus(StatusKind.Success, "Sağlık kaydı eklendi.");
                }
                else
                {
                    ShowStatus(StatusKind.Warning, DescribeAlerts(alerts));
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
    }
}

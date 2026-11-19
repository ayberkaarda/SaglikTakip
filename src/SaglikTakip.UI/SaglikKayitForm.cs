using SaglikTakip.Business.Services;
using SaglikTakip.Domain.Entities;
using SaglikTakip.UI.Infrastructure;

namespace SaglikTakip.UI
{
    /// <summary>Adds a weight and pulse measurement for a user.</summary>
    public partial class SaglikKayitForm : Form
    {
        private readonly IUserService _userService;
        private readonly IHealthRecordService _healthRecordService;
        private readonly ExclusiveOperation _save = new();

        public SaglikKayitForm(IUserService userService, IHealthRecordService healthRecordService)
        {
            _userService = userService;
            _healthRecordService = healthRecordService;
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

                await _healthRecordService.AddAsync(
                    user.Id,
                    dateTimePicker1.Value,
                    numericUpDown1.Value,
                    (int)numericUpDown2.Value,
                    textBox1.Text);

                MessageBox.Show(this, "Sağlık kaydı eklendi.", "Bilgi", MessageBoxButtons.OK, MessageBoxIcon.Information);
            },
            btnKaydet, btnkapat, comboBox1, dateTimePicker1, numericUpDown1, numericUpDown2, textBox1);
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

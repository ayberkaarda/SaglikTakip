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
            if (comboBox1.SelectedItem is not User user)
            {
                MessageBox.Show("Lütfen bir kullanıcı seçin.", "Uyarı", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            await this.RunAsync(async () =>
            {
                await _healthRecordService.AddAsync(
                    user.Id,
                    dateTimePicker1.Value,
                    numericUpDown1.Value,
                    (int)numericUpDown2.Value,
                    textBox1.Text);

                MessageBox.Show("Sağlık kaydı eklendi.", "Bilgi", MessageBoxButtons.OK, MessageBoxIcon.Information);
            });
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

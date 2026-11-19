using SaglikTakip.Business.Services;
using SaglikTakip.Domain.Entities;
using SaglikTakip.UI.Infrastructure;

namespace SaglikTakip.UI
{
    /// <summary>Adds an exercise for a user.</summary>
    public partial class EgzersizKayitForm : Form
    {
        private readonly IUserService _userService;
        private readonly IExerciseService _exerciseService;
        private readonly ExclusiveOperation _save = new();

        public EgzersizKayitForm(IUserService userService, IExerciseService exerciseService)
        {
            _userService = userService;
            _exerciseService = exerciseService;
            InitializeComponent();
        }

        private async void EgzersizKayitForm_Load_1(object sender, EventArgs e)
        {
            await this.RunAsync(() => comboBox1.LoadUsersAsync(_userService));
        }

        private async void btnKaydet_Click(object sender, EventArgs e)
        {
            await SaveAsync();
        }

        /// <summary>Saves the entered exercise; ignored while a previous save is still pending.</summary>
        internal Task SaveAsync()
        {
            return this.RunExclusiveAsync(_save, async () =>
            {
                if (comboBox1.SelectedItem is not User user)
                {
                    MessageBox.Show(this, "Lütfen bir kullanıcı seçiniz.", "Uyarı", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    return;
                }

                if (string.IsNullOrWhiteSpace(textBox1.Text))
                {
                    MessageBox.Show(this, "Egzersiz adını giriniz.", "Uyarı", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    return;
                }

                await _exerciseService.AddAsync(
                    user.Id,
                    textBox1.Text,
                    (int)numericUpDown1.Value,
                    (int)numericUpDown2.Value);

                MessageBox.Show("Egzersiz kaydedildi.", "Bilgi", MessageBoxButtons.OK, MessageBoxIcon.Information);

                textBox1.Clear();
                numericUpDown1.Value = 0;
                numericUpDown2.Value = 0;
                comboBox1.SelectedIndex = -1;
            },
            btnKaydet, btnkapat, comboBox1, textBox1, numericUpDown1, numericUpDown2);
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

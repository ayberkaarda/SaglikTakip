using System.Globalization;
using SaglikTakip.Business.Services;
using SaglikTakip.UI.Infrastructure;

namespace SaglikTakip.UI
{
    /// <summary>Adds a new user.</summary>
    public partial class KullaniciEkleForm : Form
    {
        private readonly IUserService _userService;
        private readonly ExclusiveOperation _save = new();

        public KullaniciEkleForm(IUserService userService)
        {
            _userService = userService;
            InitializeComponent();
        }

        private void textBox2_KeyPress(object sender, KeyPressEventArgs e)
        {
            e.Handled = !char.IsDigit(e.KeyChar) && !char.IsControl(e.KeyChar);
        }

        private async void btnKaydet_Click(object sender, EventArgs e)
        {
            await SaveAsync();
        }

        /// <summary>Saves the entered user; ignored while a previous save is still pending.</summary>
        internal Task SaveAsync()
        {
            return this.RunExclusiveAsync(_save, async () =>
            {
                if (!int.TryParse(textBoxyas.Text, NumberStyles.None, CultureInfo.CurrentCulture, out var age))
                {
                    MessageBox.Show(this, "Lütfen geçerli bir yaş girin.", "Uyarı", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    return;
                }

                await _userService.AddAsync(textBoxad.Text, age, comboBoxcins.SelectedItem?.ToString());

                MessageBox.Show("Kullanıcı kaydedildi.", "Bilgi", MessageBoxButtons.OK, MessageBoxIcon.Information);

                textBoxad.Text = "Ad";
                textBoxyas.Text = "Yaş";
                comboBoxcins.SelectedIndex = -1;
                comboBoxcins.Text = "Cinsiyet";
            },
            btnKaydet, btnkapat, textBoxad, textBoxyas, comboBoxcins);
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

using System.Globalization;
using SaglikTakip.Business.Services;
using SaglikTakip.Domain.Entities;
using SaglikTakip.UI.Infrastructure;
using SaglikTakip.UI.Theming;

namespace SaglikTakip.UI
{
    /// <summary>Adds a new user.</summary>
    public sealed class KullaniciEkleForm : AppDialog
    {
        private readonly IUserService _userService;
        private readonly ExclusiveOperation _save = new();

        private readonly TextBox textBoxad = new() { Name = "textBoxad", MaxLength = 16, PlaceholderText = "Örn. Ayşe Yılmaz" };
        private readonly TextBox textBoxyas = new() { Name = "textBoxyas", MaxLength = 3, Width = 120, PlaceholderText = "Örn. 34" };
        private readonly ThemedComboBox comboBoxcins = new() { Name = "comboBoxcins", Placeholder = "Seçin" };
        private readonly Button btnKaydet = FormLayout.Button("btnKaydet", "Kaydet", ButtonKind.Primary);
        private readonly Button btnkapat;

        public KullaniciEkleForm(IUserService userService)
            : base("Kullanıcı Ekle", "Takip edilecek kişinin temel bilgilerini girin.", Glyphs.AddFriend, new Size(520, 420))
        {
            _userService = userService;
            btnkapat = CreateCloseButton("btnkapat");
            comboBoxcins.Items.AddRange([.. Genders.All]);
            textBoxyas.KeyPress += (_, e) => e.Handled = !char.IsDigit(e.KeyChar) && !char.IsControl(e.KeyChar);
            btnKaydet.Click += async (_, _) => await SaveAsync();

            var fields = FormLayout.Fields(
                "fields",
                [FormLayout.Field("&Ad soyad", textBoxad, "En fazla 16 karakter.")],
                [FormLayout.Field("&Yaş", textBoxyas), FormLayout.Field("&Cinsiyet", comboBoxcins)]);
            Content.Controls.Add(FormLayout.Page((FormLayout.Card("userCard", "Kişi bilgileri", fields, fill: false), false)));
            SetFooterButtons(btnKaydet, btnkapat);
            AcceptButton = btnKaydet;
            FinishLayout();
        }

        /// <summary>Saves the entered user; ignored while a previous save is still pending.</summary>
        internal Task SaveAsync()
        {
            return this.RunExclusiveAsync(_save, async () =>
            {
                if (!int.TryParse(textBoxyas.Text, NumberStyles.None, CultureInfo.CurrentCulture, out var age))
                {
                    ShowStatus(StatusKind.Warning, "Lütfen geçerli bir yaş girin.");
                    _save.FocusAfterUnlock(textBoxyas);
                    return;
                }

                await _userService.AddAsync(textBoxad.Text, age, comboBoxcins.SelectedItem?.ToString());

                ShowStatus(StatusKind.Success, "Kullanıcı kaydedildi. Yeni bir kişi ekleyebilirsiniz.");
                textBoxad.Clear();
                textBoxyas.Clear();
                comboBoxcins.SelectedIndex = -1;
                _save.FocusAfterUnlock(textBoxad);
            },
            btnKaydet, btnkapat, textBoxad, textBoxyas, comboBoxcins);
        }
    }
}

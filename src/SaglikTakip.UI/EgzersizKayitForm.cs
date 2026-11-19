using SaglikTakip.Business.Services;
using SaglikTakip.Domain.Entities;
using SaglikTakip.UI.Infrastructure;
using SaglikTakip.UI.Theming;

namespace SaglikTakip.UI
{
    /// <summary>Adds an exercise for a user.</summary>
    public sealed class EgzersizKayitForm : AppDialog
    {
        private readonly IUserService _userService;
        private readonly IExerciseService _exerciseService;
        private readonly ExclusiveOperation _save = new();

        private readonly ThemedComboBox comboBox1 = FormLayout.UserPicker("comboBox1");
        private readonly TextBox textBox1 = new() { Name = "textBox1", MaxLength = 100, PlaceholderText = "Örn. Tempolu yürüyüş" };
        private readonly NumericUpDown numericUpDown1 = new() { Name = "numericUpDown1", Width = 120, TextAlign = HorizontalAlignment.Right };
        private readonly NumericUpDown numericUpDown2 = new() { Name = "numericUpDown2", Width = 120, TextAlign = HorizontalAlignment.Right };
        private readonly Button btnKaydet = FormLayout.Button("btnKaydet", "Kaydet", ButtonKind.Primary);
        private readonly Button btnkapat;

        public EgzersizKayitForm(IUserService userService, IExerciseService exerciseService)
            : base("Egzersiz Planla", "Egzersizi kişiye ekleyin; süre dakika olarak girilir.", Glyphs.Run, new Size(560, 460))
        {
            _userService = userService;
            _exerciseService = exerciseService;
            btnkapat = CreateCloseButton("btnkapat");
            btnKaydet.Click += async (_, _) => await SaveAsync();
            Load += async (_, _) => await this.RunAsync(() => comboBox1.LoadUsersAsync(_userService));

            var fields = FormLayout.Fields(
                "fields",
                [FormLayout.Field("&Kullanıcı", comboBox1)],
                [FormLayout.Field("&Egzersiz adı", textBox1)],
                [FormLayout.Field("&Süre (dk)", numericUpDown1), FormLayout.Field("&Tekrar sayısı", numericUpDown2)]);
            Content.Controls.Add(FormLayout.Page((FormLayout.Card("exerciseCard", "Egzersiz", fields, fill: false), false)));
            SetFooterButtons(btnKaydet, btnkapat);
            AcceptButton = btnKaydet;
            FinishLayout();
        }

        /// <summary>Saves the entered exercise; ignored while a previous save is still pending.</summary>
        internal Task SaveAsync()
        {
            return this.RunExclusiveAsync(_save, async () =>
            {
                if (comboBox1.SelectedItem is not User user)
                {
                    ShowStatus(StatusKind.Warning, "Lütfen bir kullanıcı seçiniz.");
                    _save.FocusAfterUnlock(comboBox1);
                    return;
                }

                if (string.IsNullOrWhiteSpace(textBox1.Text))
                {
                    ShowStatus(StatusKind.Warning, "Egzersiz adını giriniz.");
                    _save.FocusAfterUnlock(textBox1);
                    return;
                }

                await _exerciseService.AddAsync(
                    user.Id,
                    textBox1.Text,
                    (int)numericUpDown1.Value,
                    (int)numericUpDown2.Value);

                ShowStatus(StatusKind.Success, "Egzersiz kaydedildi.");

                textBox1.Clear();
                numericUpDown1.Value = 0;
                numericUpDown2.Value = 0;
                comboBox1.SelectedIndex = -1;
                _save.FocusAfterUnlock(comboBox1);
            },
            btnKaydet, btnkapat, comboBox1, textBox1, numericUpDown1, numericUpDown2);
        }
    }
}

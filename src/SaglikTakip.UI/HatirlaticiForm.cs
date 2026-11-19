using System.Globalization;
using SaglikTakip.Business.Insights;
using SaglikTakip.Business.Services;
using SaglikTakip.Domain.Entities;
using SaglikTakip.UI.Infrastructure;
using Serilog;

namespace SaglikTakip.UI
{
    /// <summary>Defines periodic reminders and lists them; due and missed ones come first.</summary>
    public sealed class HatirlaticiForm : Form
    {
        private static readonly Dictionary<string, string> Columns = new()
        {
            [nameof(Row.Status)] = "Durum",
            [nameof(Row.User)] = "Kullanıcı",
            [nameof(Row.Kind)] = "Tür",
            [nameof(Row.Title)] = "Başlık",
            [nameof(Row.Interval)] = "Tekrar",
            [nameof(Row.NextDue)] = "Sonraki zaman",
        };

        private readonly IUserService _userService;
        private readonly IReminderService _reminderService;
        private readonly RequestSequence _listRequests = new();
        private readonly ExclusiveOperation _write = new();

        private readonly ComboBox _userPicker = FormLayout.UserPicker();
        private readonly ComboBox _kindPicker = new() { Name = "kindPicker", DropDownStyle = ComboBoxStyle.DropDownList, Width = 120 };
        private readonly TextBox _title = new() { Name = "titleInput", MaxLength = 100, Width = 240 };
        private readonly NumericUpDown _every = new() { Name = "everyInput", Minimum = 1, Maximum = 365, Value = 1, Width = 60 };
        private readonly ComboBox _unitPicker = new() { Name = "unitPicker", DropDownStyle = ComboBoxStyle.DropDownList, Width = 80 };
        private readonly DateTimePicker _firstDue = new()
        {
            Name = "firstDuePicker",
            Format = DateTimePickerFormat.Custom,
            CustomFormat = "dd.MM.yyyy HH:mm",
            Width = 140,
        };

        private readonly DataGridView _grid = new()
        {
            Name = "reminderGrid",
            Dock = DockStyle.Fill,
            ReadOnly = true,
            AllowUserToAddRows = false,
            AllowUserToDeleteRows = false,
            SelectionMode = DataGridViewSelectionMode.FullRowSelect,
            MultiSelect = false,
            AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill,
            BackgroundColor = SystemColors.Window,
            RowHeadersVisible = false,
        };

        private readonly Button _add = FormLayout.Button("btnEkle", "Ekle");
        private readonly Button _complete = FormLayout.Button("btnTamamlandi", "Tamamlandı");
        private readonly Button _delete = FormLayout.Button("btnSil", "Sil");
        private readonly Button _refresh = FormLayout.Button("btnYenile", "Yenile");
        private readonly Label _status = FormLayout.Value("statusLabel");

        public HatirlaticiForm(IUserService userService, IReminderService reminderService)
        {
            _userService = userService;
            _reminderService = reminderService;
            _kindPicker.Items.AddRange([new KindOption(ReminderKind.Measurement), new KindOption(ReminderKind.Exercise)]);
            _kindPicker.SelectedIndex = 0;
            _unitPicker.Items.AddRange([new UnitOption("Saat", 1), new UnitOption("Gün", 24), new UnitOption("Hafta", 24 * 7)]);
            _unitPicker.SelectedIndex = 1;
            _firstDue.Value = ReminderSchedule.ToMinute(DateTime.Now.AddHours(1));
            BuildLayout();

            Load += async (_, _) =>
            {
                await this.RunAsync(() => _userPicker.LoadUsersAsync(_userService));
                await ReloadAsync();
            };
            _add.Click += async (_, _) => await AddAsync();
            _complete.Click += async (_, _) => await CompleteSelectedAsync();
            _delete.Click += async (_, _) => await DeleteSelectedAsync();
            _refresh.Click += async (_, _) => await ReloadAsync();
        }

        /// <summary>Repeat interval in hours from the "every N unit" inputs.</summary>
        internal int IntervalHours => (int)_every.Value * ((UnitOption)_unitPicker.SelectedItem!).Hours;

        /// <summary>Reloads the list; only the latest load is applied, so an older, slower answer is dropped.</summary>
        internal async Task ReloadAsync()
        {
            var request = _listRequests.Next();
            UseWaitCursor = true;
            try
            {
                var items = await _reminderService.GetAllAsync();
                if (_listRequests.IsLatest(request))
                {
                    ShowItems(items);
                }
            }
#pragma warning disable CA1031 // UI boundary: every failure is reported to the user.
            catch (Exception ex)
#pragma warning restore CA1031
            {
                if (_listRequests.IsLatest(request))
                {
                    _grid.DataSource = null;
                    _status.Text = "—";
                    ErrorReporter.Show(ex, this);
                }
                else
                {
                    Log.Warning("A superseded reminder list request failed: {Details}", ErrorReporter.FormatForLog(ex));
                }
            }
            finally
            {
                if (_listRequests.IsLatest(request))
                {
                    UseWaitCursor = false;
                }
            }
        }

        /// <summary>Adds the entered reminder; ignored while a previous write is still pending.</summary>
        internal async Task AddAsync()
        {
            var added = false;
            await this.RunExclusiveAsync(_write, async () =>
            {
                if (_userPicker.SelectedItem is not User user)
                {
                    MessageBox.Show(this, "Lütfen bir kullanıcı seçin.", "Uyarı", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    return;
                }

                await _reminderService.AddAsync(
                    user.Id, ((KindOption)_kindPicker.SelectedItem!).Kind, _title.Text, IntervalHours, _firstDue.Value);
                added = true;
                _title.Clear();
            },
            WriteControls());

            if (added)
            {
                await ReloadAsync();
            }
        }

        /// <summary>Marks the selected due reminder as done; ignored while a previous write is still pending.</summary>
        internal async Task CompleteSelectedAsync()
        {
            var changed = false;
            await this.RunExclusiveAsync(_write, async () =>
            {
                if (SelectedItem() is not { } item)
                {
                    return;
                }

                if (!item.IsDue)
                {
                    MessageBox.Show(this, "Bu hatırlatıcının zamanı henüz gelmedi.", "Bilgi", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    return;
                }

                changed = await _reminderService.CompleteAsync(item.Reminder.Id, item.Reminder.NextDueAt);
            },
            WriteControls());

            if (changed)
            {
                await ReloadAsync();
            }
        }

        /// <summary>Deletes the selected reminder after a confirmation.</summary>
        internal async Task DeleteSelectedAsync()
        {
            var changed = false;
            await this.RunExclusiveAsync(_write, async () =>
            {
                if (SelectedItem() is not { } item)
                {
                    return;
                }

                var answer = MessageBox.Show(this, "Seçilen hatırlatıcı silinsin mi?", "Onay", MessageBoxButtons.YesNo, MessageBoxIcon.Question);
                if (answer == DialogResult.Yes)
                {
                    changed = await _reminderService.DeleteAsync(item.Reminder.Id);
                }
            },
            WriteControls());

            if (changed)
            {
                await ReloadAsync();
            }
        }

        /// <summary>"Her 2 gün", "Her 1 hafta", "Her 5 saat".</summary>
        internal static string DescribeInterval(int hours) =>
            hours % (24 * 7) == 0 ? string.Format(CultureInfo.CurrentCulture, "Her {0} hafta", hours / (24 * 7))
            : hours % 24 == 0 ? string.Format(CultureInfo.CurrentCulture, "Her {0} gün", hours / 24)
            : string.Format(CultureInfo.CurrentCulture, "Her {0} saat", hours);

        internal static string DescribeStatus(ReminderItem item) =>
            !item.IsDue ? "Bekliyor"
            : item.DueOccurrences > 1 ? string.Format(CultureInfo.CurrentCulture, "Zamanı geçti ({0} kez)", item.DueOccurrences)
            : "Zamanı geldi";

        private Control[] WriteControls() => [_add, _complete, _delete, _refresh, _userPicker, _grid];

        private ReminderItem? SelectedItem() => (_grid.CurrentRow?.DataBoundItem as Row)?.Item;

        private void ShowItems(IReadOnlyList<ReminderItem> items)
        {
            // Due ones first, then by time.
            var rows = items
                .OrderByDescending(i => i.IsDue)
                .ThenBy(i => i.Reminder.NextDueAt)
                .Select(i => new Row(i))
                .ToList();
            _grid.DataSource = rows;
            _grid.ConfigureColumns(Columns);
            if (_grid.Columns[nameof(Row.NextDue)] is { } column)
            {
                column.DefaultCellStyle.Format = "dd.MM.yyyy HH:mm";
            }

            var due = items.Count(i => i.IsDue);
            _status.Text = string.Format(CultureInfo.CurrentCulture, "{0} hatırlatıcı, {1} tanesinin zamanı geldi.", items.Count, due);
        }

        private void BuildLayout()
        {
            FormLayout.ApplyDefaults(this, "Sağlık Takip | Hatırlatıcılar", new Size(820, 520));
            Name = nameof(HatirlaticiForm);

            var every = FormLayout.Row(_every, _unitPicker);
            every.Dock = DockStyle.None;
            every.Padding = new Padding(0);
            var editor = FormLayout.Grid(
                ("Kullanıcı", _userPicker),
                ("Tür", _kindPicker),
                ("Başlık", _title),
                ("Tekrar", every),
                ("İlk hatırlatma", _firstDue));
            var addRow = FormLayout.Row(_add);

            var close = FormLayout.Button("btnKapat", "Kapat");
            close.Click += (_, _) => Close();
            var actions = FormLayout.Row(_complete, _delete, _refresh, close, _status);
            actions.Dock = DockStyle.Bottom;

            Controls.Add(_grid);
            Controls.Add(actions);
            Controls.Add(addRow);
            Controls.Add(editor);
        }

        private sealed record KindOption(ReminderKind Kind)
        {
            public override string ToString() => ReminderMonitor.KindText(Kind);
        }

        private sealed record UnitOption(string Text, int Hours)
        {
            public override string ToString() => Text;
        }

        /// <summary>Grid row; property names are mapped to Turkish headers.</summary>
        private sealed class Row(ReminderItem item)
        {
            public ReminderItem Item { get; } = item;

            public string Status => DescribeStatus(Item);

            public string User => Item.UserName;

            public string Kind => ReminderMonitor.KindText(Item.Reminder.Kind);

            public string Title => Item.Reminder.Title;

            public string Interval => DescribeInterval(Item.Reminder.IntervalHours);

            public DateTime NextDue => Item.Reminder.NextDueAt;
        }
    }
}

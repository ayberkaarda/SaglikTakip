using System.Globalization;
using SaglikTakip.Business.Insights;
using SaglikTakip.Business.Services;
using SaglikTakip.Domain.Entities;
using SaglikTakip.UI.Infrastructure;
using SaglikTakip.UI.Theming;
using Serilog;

namespace SaglikTakip.UI
{
    /// <summary>Defines periodic reminders and lists them; due and missed ones come first.</summary>
    public sealed class HatirlaticiForm : AppDialog
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
        private readonly ComboBox _kindPicker = FormLayout.Picker("kindPicker", 130);
        private readonly TextBox _title = new() { Name = "titleInput", MaxLength = 100, PlaceholderText = "Örn. Tansiyon ölç" };
        private readonly NumericUpDown _every = new() { Name = "everyInput", Minimum = 1, Maximum = 365, Value = 1, Width = 72, TextAlign = HorizontalAlignment.Right };
        private readonly ComboBox _unitPicker = FormLayout.Picker("unitPicker", 110);
        private readonly DateTimePicker _firstDue = new()
        {
            Name = "firstDuePicker",
            Format = DateTimePickerFormat.Custom,
            CustomFormat = "dd.MM.yyyy HH:mm",
            Width = 170,
        };

        private readonly ThemedGrid _grid = new()
        {
            Name = "reminderGrid",
            Dock = DockStyle.Fill,
            EmptyText = "Henüz hatırlatıcı yok. Soldaki formdan ilk hatırlatıcıyı ekleyin.",
        };

        private readonly Button _add = FormLayout.Button("btnEkle", "Ekle", ButtonKind.Primary);
        private readonly Button _complete = FormLayout.Button("btnTamamlandi", "Tamamlandı");
        private readonly Button _delete = FormLayout.Button("btnSil", "Sil", ButtonKind.Danger);
        private readonly Button _refresh = FormLayout.Button("btnYenile", "Yenile");
        private readonly Label _status = FormLayout.Hint(string.Empty);

        public HatirlaticiForm(IUserService userService, IReminderService reminderService)
            : base("Hatırlatıcılar", "Ölçüm ve egzersiz için tekrarlayan hatırlatmalar; zamanı gelenler üstte.", Glyphs.Bell, new Size(1060, 640))
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
                    ShowStatus(StatusKind.Warning, "Lütfen bir kullanıcı seçin.");
                    _userPicker.Focus();
                    return;
                }

                await _reminderService.AddAsync(
                    user.Id, ((KindOption)_kindPicker.SelectedItem!).Kind, _title.Text, IntervalHours, _firstDue.Value);
                added = true;
                ShowStatus(StatusKind.Success, "Hatırlatıcı eklendi.");
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
            var attempted = false;
            await this.RunExclusiveAsync(_write, async () =>
            {
                if (SelectedItem() is not { } item)
                {
                    return;
                }

                // The displayed IsDue can be stale; the service decides against the current time and
                // returns false when the occurrence is not due (or was already completed).
                attempted = true;
                await _reminderService.CompleteAsync(item.Reminder.Id, item.Reminder.NextDueAt);
            },
            WriteControls());

            if (attempted)
            {
                // Refresh either way so a stale row is replaced by the current state.
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

                if (Dialogs.Confirm(this, "Seçilen hatırlatıcı silinsin mi?"))
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

        /// <summary>Lets tests choose the row, because a grid that is never shown cannot select one.</summary>
        [System.ComponentModel.Browsable(false)]
        [System.ComponentModel.DesignerSerializationVisibility(System.ComponentModel.DesignerSerializationVisibility.Hidden)]
        internal ReminderItem? SelectionOverride { get; set; }

        private ReminderItem? SelectedItem() => SelectionOverride ?? (_grid.CurrentRow?.DataBoundItem as Row)?.Item;

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

            // Short values keep their full width; the user and the title share the rest.
            foreach (var name in new[] { nameof(Row.Status), nameof(Row.Kind), nameof(Row.Interval), nameof(Row.NextDue) })
            {
                if (_grid.Columns[name] is { } fitted)
                {
                    fitted.AutoSizeMode = DataGridViewAutoSizeColumnMode.AllCells;
                }
            }

            if (_grid.Columns[nameof(Row.NextDue)] is { } column)
            {
                column.DefaultCellStyle.Format = "dd.MM.yyyy HH:mm";
            }

            var due = items.Count(i => i.IsDue);
            _status.Text = string.Format(CultureInfo.CurrentCulture, "{0} hatırlatıcı, {1} tanesinin zamanı geldi.", items.Count, due);
        }

        /// <summary>Due and missed rows get the warning or danger text color and a stronger weight.</summary>
        private void HighlightStatus(object? sender, DataGridViewCellFormattingEventArgs e)
        {
            if (e.RowIndex < 0 || _grid.Columns[e.ColumnIndex].DataPropertyName != nameof(Row.Status)
                || _grid.Rows[e.RowIndex].DataBoundItem is not Row { Item.IsDue: true } row || e.CellStyle is null)
            {
                return;
            }

            e.CellStyle.ForeColor = row.Item.DueOccurrences > 1 ? Theme.Current.Danger : Theme.Current.Warning;
            e.CellStyle.SelectionForeColor = e.CellStyle.ForeColor;
            e.CellStyle.Font = Theme.Fonts.BodyStrong;
        }

        private void BuildLayout()
        {
            Name = nameof(HatirlaticiForm);
            _grid.CellFormatting += HighlightStatus;

            var every = new FlowLayoutPanel
            {
                AutoSize = true,
                AutoSizeMode = AutoSizeMode.GrowAndShrink,
                WrapContents = false,
                Margin = Padding.Empty,
                AccessibleName = "Tekrar",
            };
            _every.Margin = new Padding(0, 0, Spacing.S, 0);
            _every.AccessibleName = "Tekrar sayısı";
            _unitPicker.Margin = Padding.Empty;
            _unitPicker.AccessibleName = "Tekrar birimi";
            every.Controls.Add(_every);
            every.Controls.Add(_unitPicker);

            var editor = FormLayout.Fields(
                "reminderEditor",
                [FormLayout.Field("&Kullanıcı", _userPicker)],
                [FormLayout.Field("&Tür", _kindPicker)],
                [FormLayout.Field("&Başlık", _title)],
                [FormLayout.Field("&Her", every)],
                [FormLayout.Field("İlk &hatırlatma", _firstDue)]);
            _add.Margin = new Padding(0, Spacing.S, 0, 0);
            var addRow = FormLayout.Row(_add);
            var editorBody = FormLayout.Stack("editorStack", editor, addRow);

            var actions = FormLayout.Row(_complete, _delete, _refresh, _status);
            actions.Dock = DockStyle.Bottom;
            actions.Padding = new Padding(0, Spacing.M, 0, 0);
            _status.Margin = new Padding(Spacing.M, Spacing.M, 0, 0);
            var list = new Panel { Dock = DockStyle.Fill };
            list.Controls.Add(_grid);
            list.Controls.Add(actions);

            var layout = new TableLayoutPanel { Name = "layout", Dock = DockStyle.Fill, ColumnCount = 2, RowCount = 1 };
            layout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 320));
            layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            var editorCard = FormLayout.Card("editorCard", "Yeni hatırlatıcı", editorBody);
            editorCard.Margin = new Padding(0, 0, Spacing.S, Spacing.L);
            var listCard = FormLayout.Card("listCard", "Hatırlatıcılar", list);
            listCard.Margin = new Padding(Spacing.S, 0, 0, Spacing.L);
            layout.Controls.Add(editorCard, 0, 0);
            layout.Controls.Add(listCard, 1, 0);
            Content.Controls.Add(layout);

            SetFooterNote("Uygulama açıkken zamanı gelen hatırlatıcılar bildirim alanından duyurulur.");
            SetFooterButtons(CreateCloseButton());
            FinishLayout();
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

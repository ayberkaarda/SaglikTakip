using SaglikTakip.Business.Services;
using SaglikTakip.UI.Infrastructure;
using SaglikTakip.UI.Theming;
using Serilog;

namespace SaglikTakip.UI
{
    /// <summary>
    /// Main menu that opens the user, health record, exercise, report, trend, goal and reminder screens.
    /// While it is open, due reminders are announced from the notification area once a minute; reminders
    /// that came due while the application was closed are listed at startup.
    /// </summary>
    public sealed class Form1 : Form
    {
        internal const int ReminderCheckIntervalMilliseconds = 60_000;

        private readonly IFormFactory _formFactory;
        private readonly IReminderService _reminderService;
        private readonly System.ComponentModel.Container _components = new();
        private readonly System.Windows.Forms.Timer _reminderTimer;
        private readonly CancellationTokenSource _lifetime = new();
        private IReminderNotifier? _notifier;
        private ReminderMonitor? _monitor;
        private bool _dialogOpen;

        public Form1(IFormFactory formFactory, IReminderService reminderService)
        {
            _formFactory = formFactory;
            _reminderService = reminderService;
            BuildLayout();

            _reminderTimer = new System.Windows.Forms.Timer(_components) { Interval = ReminderCheckIntervalMilliseconds };
            _reminderTimer.Tick += async (_, _) => await CheckRemindersAsync();
            Shown += async (_, _) => await StartRemindersAsync();
            FormClosed += (_, _) => Shutdown();
            Disposed += (_, _) => Shutdown();
        }

        /// <summary>Creates the tray notifier; replaced in tests so no tray icon appears.</summary>
        internal Func<Icon?, IReminderNotifier> NotifierFactory = icon => new NotifyIconReminderNotifier(icon);

        /// <summary>Shows a dialog of the given type instead of creating and showing it; used by tests.</summary>
        internal Action<Type>? DialogHook;

        private bool IsClosing => _lifetime.IsCancellationRequested || IsDisposed;

        protected override void Dispose(bool disposing)
        {
            // The base raises Disposed first, which stops the reminder work (see Shutdown).
            base.Dispose(disposing);
            if (disposing)
            {
                _components.Dispose();
            }
        }

        /// <summary>Stops the reminder work; pending continuations see the cancelled token and do nothing.</summary>
        private void Shutdown()
        {
            if (_lifetime.IsCancellationRequested)
            {
                return;
            }

            _lifetime.Cancel();
            _reminderTimer.Stop();
            (_notifier as IDisposable)?.Dispose();
        }

        /// <summary>
        /// Lists the reminders that came due while the application was closed, then starts the periodic
        /// check. Those occurrences are not announced again by the balloon.
        /// </summary>
        internal async Task StartRemindersAsync()
        {
            _notifier = NotifierFactory(Icon);
            if (_notifier is NotifyIconReminderNotifier trayNotifier)
            {
                trayNotifier.Clicked += (_, _) => OpenDialog<HatirlaticiForm>();
            }

            _monitor = new ReminderMonitor(_reminderService, _notifier);

            try
            {
                var missed = await _reminderService.GetDueAsync(_lifetime.Token);
                if (IsClosing)
                {
                    return;
                }

                if (missed.Count > 0)
                {
                    Log.Information("{Count} reminder(s) came due while the application was closed", missed.Count);

                    // Only occurrences that were actually listed are marked as seen. If another screen is
                    // open the list is not shown, and the periodic check announces them instead.
                    if (OpenDialog<HatirlaticiForm>())
                    {
                        _monitor.MarkSeen(missed);
                    }
                }
            }
#pragma warning disable CA1031 // Reminders are optional; a failure must not block the main menu.
            catch (Exception ex)
#pragma warning restore CA1031
            {
                if (!IsClosing)
                {
                    ErrorReporter.Record(Log.Logger, ex);
                }
            }
            finally
            {
                if (!IsClosing)
                {
                    _reminderTimer.Start();
                }
            }
        }

        internal async Task CheckRemindersAsync()
        {
            if (_monitor is null || IsClosing)
            {
                return;
            }

            try
            {
                await _monitor.CheckAsync(_lifetime.Token);
            }
#pragma warning disable CA1031 // Runs every minute; failures are logged instead of opening a dialog each time.
            catch (Exception ex)
#pragma warning restore CA1031
            {
                if (!IsClosing)
                {
                    ErrorReporter.Record(Log.Logger, ex);
                }
            }
        }

        /// <summary>Opens the screen modally; returns <see langword="false"/> when another screen is already open.</summary>
        internal bool OpenDialog<T>()
            where T : Form
        {
            // A balloon click while another screen is open must not stack a second modal window.
            if (_dialogOpen || IsClosing)
            {
                return false;
            }

            _dialogOpen = true;
            try
            {
                if (DialogHook is { } hook)
                {
                    hook(typeof(T));
                }
                else
                {
                    using var form = _formFactory.Create<T>();
                    form.ShowDialog(this);
                }

                return true;
            }
            finally
            {
                _dialogOpen = false;
            }
        }

        private void BuildLayout()
        {
            SuspendLayout();
            AutoScaleDimensions = new SizeF(96F, 96F);
            AutoScaleMode = AutoScaleMode.Dpi;
            Name = nameof(Form1);
            Text = AppDialog.AppName;
            Font = Theme.Fonts.Body;
            Icon = Theme.AppIcon;
            StartPosition = FormStartPosition.CenterScreen;
            ClientSize = new Size(880, 640);
            MinimumSize = SizeFromClientSize(new Size(700, 520));

            var tiles = new TableLayoutPanel
            {
                Name = "menu",
                Dock = DockStyle.Top,
                AutoSize = true,
                AutoSizeMode = AutoSizeMode.GrowAndShrink,
                ColumnCount = 3,
                Margin = Padding.Empty,
            };
            for (var i = 0; i < 3; i++)
            {
                tiles.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F / 3));
            }

            AddSection(tiles, "Kayıtlar",
                Tile("btnKullaniciEkle", "Kullanıcı Ekle", "Takip edilecek kişiyi adı, yaşı ve cinsiyetiyle ekleyin.", Glyphs.AddFriend, OpenDialog<KullaniciEkleForm>),
                Tile("btnSaglikKaydi", "Sağlık Kaydı Ekle", "Kilo, nabız ve notları tarihle birlikte kaydedin.", Glyphs.Heart, OpenDialog<SaglikKayitForm>),
                Tile("btnEgzersizEkle", "Egzersiz Planla", "Egzersizin adını, süresini ve tekrarını girin.", Glyphs.Run, OpenDialog<EgzersizKayitForm>));
            AddSection(tiles, "Takip ve analiz",
                Tile("Raporgor", "Rapor Görüntüle", "Son ölçümü ve tüm kayıtları tek ekranda görün.", Glyphs.Document, OpenDialog<Raporekleform>),
                Tile("btnTrend", "Trend Grafikleri", "Kilo ve nabzın seyrini hedef aralığıyla izleyin.", Glyphs.Chart, OpenDialog<TrendForm>),
                Tile("btnDonemRaporu", "Dönem Raporu", "Haftalık ya da aylık özeti CSV veya HTML alın.", Glyphs.Calendar, OpenDialog<DonemRaporuForm>));
            AddSection(tiles, "Planlama",
                Tile("btnHedef", "Hedefler", "Hedef kiloyu ve uyarı sınırlarını belirleyin.", Glyphs.Target, OpenDialog<HedefForm>),
                Tile("btnHatirlatici", "Hatırlatıcılar", "Ölçüm ve egzersiz için tekrarlayan hatırlatma kurun.", Glyphs.Bell, OpenDialog<HatirlaticiForm>));

            var body = new Panel
            {
                Name = "body",
                Dock = DockStyle.Fill,
                AutoScroll = true,
                Padding = new Padding(Spacing.XL, Spacing.S, Spacing.XL - Spacing.M, Spacing.S),
            };
            body.Controls.Add(tiles);

            var exit = FormLayout.Button("cikis", "Çıkış", ButtonKind.Secondary);
            exit.Click += (_, _) => Application.Exit();
            var note = FormLayout.Hint("Hatırlatıcılar, uygulama açıkken bildirim alanından duyurulur.");
            note.Anchor = AnchorStyles.Left;
            note.Margin = new Padding(Spacing.XS, 0, Spacing.M, 0);
            var footer = new TableLayoutPanel
            {
                Name = "footer",
                Dock = DockStyle.Bottom,
                AutoSize = true,
                AutoSizeMode = AutoSizeMode.GrowAndShrink,
                ColumnCount = 2,
                Padding = new Padding(Spacing.XL - Spacing.XS, Spacing.M, Spacing.XL - Spacing.XS, Spacing.M),
                BackColor = Theme.Current.Surface,
            };
            footer.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            footer.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            footer.Paint += (_, e) => FormLayout.DrawDivider(footer, e.Graphics, top: true);
            exit.Anchor = AnchorStyles.Right;
            footer.Controls.Add(note, 0, 0);
            footer.Controls.Add(exit, 1, 0);

            Controls.Add(body);
            Controls.Add(footer);
            var header = FormLayout.Header(
                AppDialog.AppName,
                "Ölçümlerinizi, egzersizlerinizi ve hedeflerinizi tek yerden izleyin.",
                string.Empty,
                Theme.Logo);
            header.Controls["headerTitle"]!.Font = Theme.Fonts.Display;
            Controls.Add(header);

            ThemeStyler.Apply(this);
            ResumeLayout(false);
            PerformLayout();
        }

        private static TileButton Tile(string name, string title, string description, string glyph, Func<bool> open)
        {
            var tile = new TileButton(name, title, description, glyph);
            tile.Click += (_, _) => open();
            return tile;
        }

        private static void AddSection(TableLayoutPanel grid, string title, params TileButton[] tiles)
        {
            var heading = FormLayout.SectionTitle(title);
            heading.Font = Theme.Fonts.BodyStrong;
            heading.ForeColor = Theme.Current.TextSecondary;
            heading.Margin = new Padding(0, Spacing.L, 0, Spacing.S);
            grid.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            grid.Controls.Add(heading, 0, grid.RowStyles.Count - 1);
            grid.SetColumnSpan(heading, grid.ColumnCount);

            grid.RowStyles.Add(new RowStyle(SizeType.Absolute, Sizes.TileHeight + Spacing.M));
            for (var i = 0; i < tiles.Length; i++)
            {
                grid.Controls.Add(tiles[i], i, grid.RowStyles.Count - 1);
            }
        }
    }
}

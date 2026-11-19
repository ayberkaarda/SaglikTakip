using SaglikTakip.Business.Services;
using SaglikTakip.UI.Infrastructure;
using Serilog;

namespace SaglikTakip.UI
{
    /// <summary>
    /// Main menu that opens the user, health record, exercise, report, trend, goal and reminder screens.
    /// While it is open, due reminders are announced from the notification area once a minute; reminders
    /// that came due while the application was closed are listed at startup.
    /// </summary>
    public partial class Form1 : Form
    {
        internal const int ReminderCheckIntervalMilliseconds = 60_000;

        private readonly IFormFactory _formFactory;
        private readonly IReminderService _reminderService;
        private readonly System.Windows.Forms.Timer _reminderTimer;
        private readonly CancellationTokenSource _lifetime = new();
        private IReminderNotifier? _notifier;
        private ReminderMonitor? _monitor;
        private bool _dialogOpen;

        public Form1(IFormFactory formFactory, IReminderService reminderService)
        {
            _formFactory = formFactory;
            _reminderService = reminderService;
            InitializeComponent();

            components ??= new System.ComponentModel.Container();
            _reminderTimer = new System.Windows.Forms.Timer(components) { Interval = ReminderCheckIntervalMilliseconds };
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

        private void btnKullaniciEkle_Click(object sender, EventArgs e)
        {
            OpenDialog<KullaniciEkleForm>();
        }

        private void btnSaglikKaydi_Click(object sender, EventArgs e)
        {
            OpenDialog<SaglikKayitForm>();
        }

        private void btnEgzersizEkle_Click(object sender, EventArgs e)
        {
            OpenDialog<EgzersizKayitForm>();
        }

        private void Raporgor_Click(object sender, EventArgs e)
        {
            OpenDialog<Raporekleform>();
        }

        private void btnTrend_Click(object sender, EventArgs e)
        {
            OpenDialog<TrendForm>();
        }

        private void btnHedef_Click(object sender, EventArgs e)
        {
            OpenDialog<HedefForm>();
        }

        private void btnHatirlatici_Click(object sender, EventArgs e)
        {
            OpenDialog<HatirlaticiForm>();
        }

        private void btnDonemRaporu_Click(object sender, EventArgs e)
        {
            OpenDialog<DonemRaporuForm>();
        }

        private void cikis_Click(object sender, EventArgs e)
        {
            Application.Exit();
        }

        private void btnkapat_Click(object sender, EventArgs e)
        {
            Close();
        }

        private void btnAsagial_Click(object sender, EventArgs e)
        {
            WindowState = FormWindowState.Minimized;
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
    }
}

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
        private NotifyIconReminderNotifier? _notifier;
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
            FormClosed += (_, _) => _notifier?.Dispose();
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
        private async Task StartRemindersAsync()
        {
            _notifier = new NotifyIconReminderNotifier(Icon);
            _notifier.Clicked += (_, _) => OpenDialog<HatirlaticiForm>();
            _monitor = new ReminderMonitor(_reminderService, _notifier);

            try
            {
                var missed = await _reminderService.GetDueAsync();
                _monitor.MarkSeen(missed);
                if (missed.Count > 0)
                {
                    Log.Information("{Count} reminder(s) came due while the application was closed", missed.Count);
                    OpenDialog<HatirlaticiForm>();
                }
            }
#pragma warning disable CA1031 // Reminders are optional; a failure must not block the main menu.
            catch (Exception ex)
#pragma warning restore CA1031
            {
                ErrorReporter.Record(Log.Logger, ex);
            }
            finally
            {
                _reminderTimer.Start();
            }
        }

        private async Task CheckRemindersAsync()
        {
            if (_monitor is null)
            {
                return;
            }

            try
            {
                await _monitor.CheckAsync();
            }
#pragma warning disable CA1031 // Runs every minute; failures are logged instead of opening a dialog each time.
            catch (Exception ex)
#pragma warning restore CA1031
            {
                ErrorReporter.Record(Log.Logger, ex);
            }
        }

        private void OpenDialog<T>()
            where T : Form
        {
            // A balloon click while another screen is open must not stack a second modal window.
            if (_dialogOpen)
            {
                return;
            }

            _dialogOpen = true;
            try
            {
                using var form = _formFactory.Create<T>();
                form.ShowDialog(this);
            }
            finally
            {
                _dialogOpen = false;
            }
        }
    }
}

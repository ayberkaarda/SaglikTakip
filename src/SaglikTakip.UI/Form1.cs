using SaglikTakip.UI.Infrastructure;

namespace SaglikTakip.UI
{
    /// <summary>Main menu that opens the user, health record, exercise and report screens.</summary>
    public partial class Form1 : Form
    {
        private readonly IFormFactory _formFactory;

        public Form1(IFormFactory formFactory)
        {
            _formFactory = formFactory;
            InitializeComponent();
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

        private void OpenDialog<T>()
            where T : Form
        {
            using var form = _formFactory.Create<T>();
            form.ShowDialog(this);
        }
    }
}

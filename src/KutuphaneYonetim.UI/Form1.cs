using KutuphaneYonetim.UI.Infrastructure;

namespace KutuphaneYonetim.UI
{
    /// <summary>Main menu that opens the book, member and loan screens.</summary>
    public partial class Form1 : Form
    {
        private readonly IFormFactory _formFactory;

        public Form1(IFormFactory formFactory)
        {
            _formFactory = formFactory;
            InitializeComponent();
        }

        private void btnKitap_Click(object sender, EventArgs e)
        {
            _formFactory.Create<KitapForm>().Show();
        }

        private void btnUye_Click(object sender, EventArgs e)
        {
            _formFactory.Create<UyeForm>().Show();
        }

        private void btnOdunc_Click(object sender, EventArgs e)
        {
            _formFactory.Create<OduncForm>().Show();
        }
    }
}

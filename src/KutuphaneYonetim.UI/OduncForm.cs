using KutuphaneYonetim.Business.Services;
using KutuphaneYonetim.Domain.Entities;
using KutuphaneYonetim.UI.Infrastructure;

namespace KutuphaneYonetim.UI
{
    /// <summary>Lends books to members, takes returns and shows the loan history with dates.</summary>
    public partial class OduncForm : Form
    {
        private static readonly Dictionary<string, string> ColumnHeaders = new()
        {
            [nameof(LoanDetail.Id)] = "İşlem No",
            [nameof(LoanDetail.BookId)] = "Kitap No",
            [nameof(LoanDetail.BookTitle)] = "Kitap",
            [nameof(LoanDetail.MemberId)] = "Üye No",
            [nameof(LoanDetail.MemberName)] = "Üye",
            [nameof(LoanDetail.LoanDate)] = "Ödünç Tarihi",
            [nameof(LoanDetail.ReturnDate)] = "İade Tarihi",
        };

        private readonly IBookService _bookService;
        private readonly IMemberService _memberService;
        private readonly ILoanService _loanService;
        private int? _selectedBookId;

        public OduncForm(IBookService bookService, IMemberService memberService, ILoanService loanService)
        {
            _bookService = bookService;
            _memberService = memberService;
            _loanService = loanService;
            InitializeComponent();
        }

        private async void OduncForm_Load(object sender, EventArgs e)
        {
            await this.RunAsync(async () =>
            {
                await LoadComboBoxesAsync();
                await ListLoansAsync();
            });
        }

        private async Task LoadComboBoxesAsync()
        {
            cmbKitaplar.DisplayMember = nameof(Book.Title);
            cmbKitaplar.ValueMember = nameof(Book.Id);
            cmbKitaplar.DataSource = await _bookService.GetAvailableAsync();

            cmbUyeler.DisplayMember = nameof(Member.FullName);
            cmbUyeler.ValueMember = nameof(Member.Id);
            cmbUyeler.DataSource = await _memberService.GetAllAsync();
        }

        private async Task ListLoansAsync()
        {
            dataGridView1.DataSource = await _loanService.GetAllAsync();
            dataGridView1.ConfigureColumns(ColumnHeaders);
        }

        private async void btnOduncAl_Click(object sender, EventArgs e)
        {
            if (cmbKitaplar.SelectedValue is not int bookId || cmbUyeler.SelectedValue is not int memberId)
            {
                MessageBox.Show("Lütfen ödünç verilecek kitabı ve üyeyi seçin.", "Uyarı", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            await this.RunAsync(async () =>
            {
                await _loanService.LendAsync(bookId, memberId);
                await LoadComboBoxesAsync();
                await ListLoansAsync();
            });
        }

        private async void btnIadeEt_Click(object sender, EventArgs e)
        {
            if (_selectedBookId is not int bookId)
            {
                MessageBox.Show("Lütfen iade edilecek kitabı seçin.", "Uyarı", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            await this.RunAsync(async () =>
            {
                await _loanService.ReturnAsync(bookId);
                MessageBox.Show("Kitap başarıyla iade edildi.", "Bilgi", MessageBoxButtons.OK, MessageBoxIcon.Information);
                await LoadComboBoxesAsync();
                await ListLoansAsync();
                _selectedBookId = null;
            });
        }

        private void dataGridView1_CellClick(object sender, DataGridViewCellEventArgs e)
        {
            if (e.RowIndex >= 0 && dataGridView1.Rows[e.RowIndex].DataBoundItem is LoanDetail loan)
            {
                _selectedBookId = loan.BookId;
            }
        }
    }
}

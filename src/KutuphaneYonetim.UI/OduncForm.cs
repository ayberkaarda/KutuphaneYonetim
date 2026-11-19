using System.ComponentModel;
using System.Globalization;
using KutuphaneYonetim.Business.Services;
using KutuphaneYonetim.Domain.Entities;
using KutuphaneYonetim.UI.Infrastructure;

namespace KutuphaneYonetim.UI
{
    /// <summary>
    /// Lends books to members with a due date, takes returns (recording late fines) and shows the
    /// loan history with dates, overdue days and fines.
    /// </summary>
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
            [nameof(LoanDetail.DueDate)] = "Teslim Tarihi",
            [nameof(LoanDetail.ReturnDate)] = "İade Tarihi",
            [nameof(LoanDetail.OverdueDays)] = "Gecikme (Gün)",
            [nameof(LoanDetail.EstimatedFine)] = "Biriken Ceza (TL)",
            [nameof(LoanDetail.FineAmount)] = "Ceza (TL)",
            [nameof(LoanDetail.FinePaidDate)] = "Ceza Ödeme Tarihi",
        };

        private readonly IBookService _bookService;
        private readonly IMemberService _memberService;
        private readonly ILoanService _loanService;
        private readonly ExclusiveOperation _write = new();
        private readonly RequestSequence _pickerRequests = new();
        private readonly RequestSequence _listRequests = new();
        private int? _selectedBookId;

        public OduncForm(IBookService bookService, IMemberService memberService, ILoanService loanService)
        {
            _bookService = bookService;
            _memberService = memberService;
            _loanService = loanService;
            InitializeComponent();
        }

        [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        internal UserPrompts Prompts { get; set; } = UserPrompts.Default;

        private Control[] WriteButtons => [btnOduncAl, btnIadeEt];

        private async void OduncForm_Load(object sender, EventArgs e)
        {
            dtpTeslim.MinDate = DateTime.Today;
            dtpTeslim.Value = _loanService.GetDefaultDueDate();

            await this.RunAsync(LoadAsync);
        }

        /// <summary>Initial load of the pickers and the loan list.</summary>
        internal async Task LoadAsync()
        {
            await LoadComboBoxesAsync();
            await ListLoansAsync();
        }

        /// <summary>Loads the pickers; only the most recent of overlapping loads is shown.</summary>
        private async Task LoadComboBoxesAsync()
        {
            var request = _pickerRequests.Next();
            var books = await _bookService.GetAvailableAsync();
            var members = await _memberService.GetAllAsync();
            if (!_pickerRequests.IsLatest(request))
            {
                return;
            }

            cmbKitaplar.DisplayMember = nameof(Book.Title);
            cmbKitaplar.ValueMember = nameof(Book.Id);
            cmbKitaplar.DataSource = books;

            cmbUyeler.DisplayMember = nameof(Member.FullName);
            cmbUyeler.ValueMember = nameof(Member.Id);
            cmbUyeler.DataSource = members;
        }

        /// <summary>Loads the loan list; only the most recent of overlapping loads is shown.</summary>
        private async Task ListLoansAsync()
        {
            var request = _listRequests.Next();
            var loans = await _loanService.GetAllAsync();
            if (!_listRequests.IsLatest(request))
            {
                return;
            }

            dataGridView1.DataSource = loans;
            dataGridView1.ConfigureColumns(ColumnHeaders);
            dataGridView1.FormatMoneyColumns(nameof(LoanDetail.EstimatedFine), nameof(LoanDetail.FineAmount));
            dataGridView1.FormatDateColumns(nameof(LoanDetail.DueDate));
        }

        private async void btnOduncAl_Click(object sender, EventArgs e) => await LendAsync();

        /// <summary>Lends the chosen book; a second click while the first loan is pending is ignored.</summary>
        internal Task LendAsync() => this.RunExclusiveAsync(
            _write,
            async () =>
            {
                if (cmbKitaplar.SelectedValue is not int bookId || cmbUyeler.SelectedValue is not int memberId)
                {
                    Prompts.Warn("Lütfen ödünç verilecek kitabı ve üyeyi seçin.");
                    return;
                }

                await _loanService.LendAsync(bookId, memberId, dtpTeslim.Value.Date);
                await LoadComboBoxesAsync();
                await ListLoansAsync();
                dtpTeslim.Value = _loanService.GetDefaultDueDate();
            },
            WriteButtons);

        private async void btnIadeEt_Click(object sender, EventArgs e) => await ReturnAsync();

        /// <summary>Takes the selected book back; a second click while the first return is pending is ignored.</summary>
        internal Task ReturnAsync() => this.RunExclusiveAsync(
            _write,
            async () =>
            {
                if (_selectedBookId is not int bookId)
                {
                    Prompts.Warn("Lütfen iade edilecek kitabı seçin.");
                    return;
                }

                var receipt = await _loanService.ReturnAsync(bookId);
                Prompts.Inform(DescribeReturn(receipt));
                await LoadComboBoxesAsync();
                await ListLoansAsync();
                _selectedBookId = null;
            },
            WriteButtons);

        private static string DescribeReturn(ReturnReceipt receipt)
        {
            var text = receipt.Fine > 0
                ? string.Format(
                    CultureInfo.CurrentCulture,
                    "Kitap iade edildi. {0} gün gecikme için {1:N2} TL ceza kaydedildi.",
                    receipt.OverdueDays,
                    receipt.Fine)
                : "Kitap başarıyla iade edildi.";

            if (receipt.HeldForMemberName is { } member && receipt.HeldUntil is { } until)
            {
                text += string.Format(
                    CultureInfo.CurrentCulture,
                    "\n\nKitap rezervasyon sırasındaki {0} için {1:g} tarihine kadar ayrıldı.",
                    member,
                    until);
            }

            return text;
        }

        private void dataGridView1_CellClick(object sender, DataGridViewCellEventArgs e)
        {
            if (e.RowIndex >= 0 && dataGridView1.Rows[e.RowIndex].DataBoundItem is LoanDetail loan)
            {
                SelectLoan(loan);
            }
        }

        /// <summary>Makes the book of <paramref name="loan"/> (a row of the list) the one to return.</summary>
        internal void SelectLoan(LoanDetail loan) => _selectedBookId = loan.BookId;
    }
}

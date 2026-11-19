using System.Globalization;
using KutuphaneYonetim.Business.Services;
using KutuphaneYonetim.Domain.Entities;
using KutuphaneYonetim.UI.Infrastructure;
using KutuphaneYonetim.UI.Theming;

namespace KutuphaneYonetim.UI
{
    /// <summary>
    /// Lends books to members with a due date, takes returns (recording late fines) and shows the
    /// loan history with dates, overdue days and fines.
    /// </summary>
    public sealed class OduncForm : AppDialog
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
            [nameof(LoanDetail.FinePaidDate)] = "Ödeme Tarihi",
        };

        /// <summary>Columns that turn to the danger color while a loan is overdue.</summary>
        private static readonly HashSet<string> OverdueColumns =
            [nameof(LoanDetail.DueDate), nameof(LoanDetail.OverdueDays), nameof(LoanDetail.EstimatedFine)];

        private readonly IBookService _bookService;
        private readonly IMemberService _memberService;
        private readonly ILoanService _loanService;
        private readonly ExclusiveOperation _write = new();
        private readonly RequestSequence _pickerRequests = new();
        private readonly RequestSequence _listRequests = new();
        private int? _selectedBookId;

        private readonly ThemedComboBox cmbKitaplar = FormLayout.Picker(nameof(cmbKitaplar), 260, "Müsait kitap seçin");
        private readonly ThemedComboBox cmbUyeler = FormLayout.Picker(nameof(cmbUyeler), 220, "Üye seçin");
        private readonly DateTimePicker dtpTeslim = FormLayout.DatePicker(nameof(dtpTeslim));
        private readonly ThemedGrid dataGridView1 = new() { Name = nameof(dataGridView1), AccessibleName = "Ödünç kayıtları" };
        private readonly ThemedButton btnOduncAl = FormLayout.Button(nameof(btnOduncAl), "Ödünç Ver", ButtonKind.Primary);
        private readonly ThemedButton btnIadeEt = FormLayout.Button(nameof(btnIadeEt), "Seçili Kitabı İade Al");
        private readonly Label lblListeOzet = FormLayout.Hint(string.Empty);

        public OduncForm(IBookService bookService, IMemberService memberService, ILoanService loanService)
            : base("Ödünç ve İade", "Müsait kitabı teslim tarihiyle ödünç verin; iadede gecikme cezası kendiliğinden kaydedilir.", Glyphs.Lending, new Size(1200, 720), new Size(960, 600))
        {
            _bookService = bookService;
            _memberService = memberService;
            _loanService = loanService;
            BuildLayout();

            Load += async (_, _) =>
            {
                PrepareDueDate();
                await this.RunAsync(LoadAsync);
            };
            btnOduncAl.Click += async (_, _) => await LendAsync();
            btnIadeEt.Click += async (_, _) => await ReturnAsync();
            dataGridView1.CellClick += (_, e) => SelectRow(e.RowIndex);
            dataGridView1.CurrentCellChanged += (_, _) =>
            {
                // Arrow keys choose a row too, but only while the user is in the list.
                if (dataGridView1.Focused && dataGridView1.CurrentRow is { } row)
                {
                    SelectRow(row.Index);
                }
            };
            dataGridView1.CellFormatting += HighlightOverdue;
        }

        private Control[] WriteButtons => [btnOduncAl, btnIadeEt];

        /// <summary>The due date starts at the suggested loan period and cannot lie in the past.</summary>
        internal void PrepareDueDate()
        {
            dtpTeslim.MinDate = DateTime.Today;
            dtpTeslim.Value = _loanService.GetDefaultDueDate();
            var days = (dtpTeslim.Value.Date - DateTime.Today).Days;
            SetFooterNote(string.Format(
                CultureInfo.CurrentCulture,
                "Önerilen ödünç süresi {0} gün; teslim tarihinden sonraki her gün için ceza işler. İade için listeden açık bir kaydı seçin.",
                days));
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
            dataGridView1.IsLoading = true;
            try
            {
                var loans = await _loanService.GetAllAsync();
                if (!_listRequests.IsLatest(request))
                {
                    return;
                }

                dataGridView1.DataSource = loans;
                dataGridView1.ConfigureColumns(ColumnHeaders);
                dataGridView1.FormatMoneyColumns(nameof(LoanDetail.EstimatedFine), nameof(LoanDetail.FineAmount));
                dataGridView1.FormatDateColumns(
                    nameof(LoanDetail.LoanDate), nameof(LoanDetail.DueDate), nameof(LoanDetail.ReturnDate), nameof(LoanDetail.FinePaidDate));
                dataGridView1.FitWideColumns(130, 56,nameof(LoanDetail.BookTitle), nameof(LoanDetail.MemberName));
                ShowSummary(loans);
            }
            finally
            {
                if (_listRequests.IsLatest(request))
                {
                    dataGridView1.IsLoading = false;
                }
            }
        }

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

                var title = cmbKitaplar.GetItemText(cmbKitaplar.SelectedItem);
                var member = cmbUyeler.GetItemText(cmbUyeler.SelectedItem);
                var due = dtpTeslim.Value.Date;
                await _loanService.LendAsync(bookId, memberId, due);
                await LoadComboBoxesAsync();
                await ListLoansAsync();
                dtpTeslim.Value = _loanService.GetDefaultDueDate();
                Prompts.Inform(string.Format(
                    CultureInfo.CurrentCulture, "\"{0}\" {1} adlı üyeye ödünç verildi; teslim tarihi {2:d}.", title, member, due));
            },
            WriteButtons);

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
                    "\nKitap rezervasyon sırasındaki {0} için {1:g} tarihine kadar ayrıldı.",
                    member,
                    until);
            }

            return text;
        }

        /// <summary>Makes the book of <paramref name="loan"/> (a row of the list) the one to return.</summary>
        internal void SelectLoan(LoanDetail loan)
        {
            _selectedBookId = loan.BookId;
            dataGridView1.SelectedItem = loan;
        }

        private void SelectRow(int rowIndex)
        {
            if (rowIndex >= 0 && dataGridView1.Rows[rowIndex].DataBoundItem is LoanDetail loan)
            {
                SelectLoan(loan);
            }
        }

        private void ShowSummary(IReadOnlyList<LoanDetail> loans)
        {
            var open = loans.Count(l => l.IsActive);
            var overdue = loans.Count(l => l.IsActive && l.OverdueDays > 0);
            lblListeOzet.Text = string.Format(CultureInfo.CurrentCulture, "{0} kayıt · {1} açık ödünç · {2} gecikmede", loans.Count, open, overdue);
        }

        /// <summary>An open loan past its due date shows its due date, delay and fine in the danger color.</summary>
        private void HighlightOverdue(object? sender, DataGridViewCellFormattingEventArgs e)
        {
            if (e.RowIndex < 0 || e.CellStyle is null
                || !OverdueColumns.Contains(dataGridView1.Columns[e.ColumnIndex].DataPropertyName)
                || dataGridView1.Rows[e.RowIndex].DataBoundItem is not LoanDetail { IsActive: true, OverdueDays: > 0 })
            {
                return;
            }

            e.CellStyle.ForeColor = Theme.Current.Danger;
            e.CellStyle.SelectionForeColor = Theme.Current.Danger;
            e.CellStyle.Font = Theme.Fonts.BodyStrong;
        }

        private void BuildLayout()
        {
            Name = nameof(OduncForm);

            var fields = FormLayout.FieldRow(
                "lendFields",
                (FormLayout.Field("&Kitap", cmbKitaplar), 55),
                (FormLayout.Field("Ü&ye", cmbUyeler), 45),
                (FormLayout.Field("&Teslim tarihi", dtpTeslim), 0),
                (FormLayout.FieldAction(btnOduncAl), 0));
            var lendCard = FormLayout.Card("lendCard", "Ödünç ver", fields, fill: false);

            btnIadeEt.Margin = Padding.Empty;
            lblListeOzet.Name = nameof(lblListeOzet);
            lblListeOzet.Margin = new Padding(Spacing.M, 0, 0, 0);
            var actions = FormLayout.Bar("actionBar", (btnIadeEt, false), (lblListeOzet, true));
            actions.Margin = new Padding(0, Spacing.M, 0, 0);
            dataGridView1.EmptyText = "Henüz ödünç kaydı yok. Yukarıdan bir kitabı ödünç verdiğinizde burada görünür.";
            var listCard = FormLayout.Card("listCard", "Ödünç kayıtları", FormLayout.Page((dataGridView1, true), (actions, false)));

            Content.Controls.Add(FormLayout.Page((lendCard, false), (listCard, true)));
            SetFooterNote("İade almak için listeden açık bir ödünç kaydı seçin.");
            SetFooterButtons(CreateCloseButton());
            FinishLayout();
        }
    }
}

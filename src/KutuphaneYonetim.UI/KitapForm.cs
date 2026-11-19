using System.ComponentModel;
using System.Globalization;
using KutuphaneYonetim.Business.Services;
using KutuphaneYonetim.Domain;
using KutuphaneYonetim.Domain.Entities;
using KutuphaneYonetim.UI.Infrastructure;

namespace KutuphaneYonetim.UI
{
    /// <summary>Searches, lists, adds and deletes books.</summary>
    public partial class KitapForm : Form
    {
        private static readonly Dictionary<string, string> ColumnHeaders = new()
        {
            [nameof(Book.Id)] = "Kitap No",
            [nameof(Book.Title)] = "Kitap Adı",
            [nameof(Book.Author)] = "Yazar",
            [nameof(Book.Isbn)] = "ISBN",
            [nameof(Book.IsAvailable)] = "Rafta",
        };

        private static readonly ListOption<BookAvailability>[] AvailabilityOptions =
        [
            new("Tüm kitaplar", BookAvailability.All),
            new("Müsait", BookAvailability.Available),
            new("Ödünçte", BookAvailability.OnLoan),
            new("Rezervasyon için ayrılmış", BookAvailability.Reserved),
        ];

        private readonly IBookService _bookService;
        private readonly Pager _pager;
        private readonly RequestSequence _listRequests = new();
        private readonly ExclusiveOperation _write = new();
        private Book? _selectedBook;

        public KitapForm(IBookService bookService)
        {
            _bookService = bookService;
            InitializeComponent();
            _pager = new Pager(btnOnceki, btnSonraki, lblSayfa);
        }

        [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        internal UserPrompts Prompts { get; set; } = UserPrompts.Default;

        private Control[] WriteButtons => [btnEkle, btnSil];

        /// <summary>Loads the current page; only the most recent of overlapping loads is shown.</summary>
        internal async Task ListBooksAsync()
        {
            var request = _listRequests.Next();
            _selectedBook = null;
            var query = new BookQuery(txtAra.Text, cmbDurum.SelectedOption(BookAvailability.All), _pager.Page);
            var result = await _bookService.SearchAsync(query);
            if (!_listRequests.IsLatest(request))
            {
                return;
            }

            dataGridView1.DataSource = result.Items;
            dataGridView1.ConfigureColumns(ColumnHeaders);
            _pager.Show(result);
            _selectedBook = null;
        }

        private async Task SearchFromFirstPageAsync()
        {
            _pager.Reset();
            await this.RunAsync(ListBooksAsync);
        }

        private async void KitapForm_Load(object sender, EventArgs e)
        {
            cmbDurum.BindOptions(AvailabilityOptions);
            await this.RunAsync(ListBooksAsync);
        }

        private async void btnAra_Click(object sender, EventArgs e) => await SearchFromFirstPageAsync();

        private async void cmbDurum_SelectionChangeCommitted(object sender, EventArgs e) => await SearchFromFirstPageAsync();

        private async void txtAra_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.KeyCode == Keys.Enter)
            {
                e.SuppressKeyPress = true;
                await SearchFromFirstPageAsync();
            }
        }

        private async void btnOnceki_Click(object sender, EventArgs e)
        {
            _pager.MovePrevious();
            await this.RunAsync(ListBooksAsync);
        }

        private async void btnSonraki_Click(object sender, EventArgs e)
        {
            _pager.MoveNext();
            await this.RunAsync(ListBooksAsync);
        }

        private async void btnEkle_Click(object sender, EventArgs e) => await AddBookAsync();

        /// <summary>Adds the entered book; a second click while the first add is pending is ignored.</summary>
        internal Task AddBookAsync() => this.RunExclusiveAsync(
            _write,
            async () =>
            {
                if (string.IsNullOrWhiteSpace(txtAd.Text) || string.IsNullOrWhiteSpace(txtYazar.Text))
                {
                    Prompts.Warn("Lütfen kitap adı ve yazar alanlarını doldurunuz.");
                    return;
                }

                await _bookService.AddAsync(txtAd.Text, txtYazar.Text, txtIsbn.Text);
                await ListBooksAsync();

                txtAd.Clear();
                txtYazar.Clear();
                txtIsbn.Clear();
            },
            WriteButtons);

        private void dataGridView1_CellClick(object sender, DataGridViewCellEventArgs e)
        {
            if (e.RowIndex >= 0 && dataGridView1.Rows[e.RowIndex].DataBoundItem is Book book)
            {
                _selectedBook = book;
            }
        }

        private async void btnSil_Click(object sender, EventArgs e) => await DeleteBookAsync();

        /// <summary>Deletes the selected book after confirmation; ignored while another write is pending.</summary>
        internal Task DeleteBookAsync() => this.RunExclusiveAsync(
            _write,
            async () =>
            {
                if (_selectedBook is not { } book)
                {
                    Prompts.Warn("Lütfen silinecek kitabı seçin.");
                    return;
                }

                if (!Prompts.Confirm(string.Format(
                    CultureInfo.CurrentCulture, "\"{0}\" (Kitap No {1}) silinsin mi?", book.Title, book.Id)))
                {
                    return;
                }

                await _bookService.DeleteAsync(book.Id);
                Prompts.Inform("Kitap silindi.");
                await ListBooksAsync();
            },
            WriteButtons);
    }
}

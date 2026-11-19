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
        private int? _selectedBookId;

        public KitapForm(IBookService bookService)
        {
            _bookService = bookService;
            InitializeComponent();
            _pager = new Pager(btnOnceki, btnSonraki, lblSayfa);
        }

        private async Task ListBooksAsync()
        {
            var query = new BookQuery(txtAra.Text, cmbDurum.SelectedOption(BookAvailability.All), _pager.Page);
            var result = await _bookService.SearchAsync(query);

            dataGridView1.DataSource = result.Items;
            dataGridView1.ConfigureColumns(ColumnHeaders);
            _pager.Show(result);
            _selectedBookId = null;
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

        private async void btnEkle_Click(object sender, EventArgs e)
        {
            if (string.IsNullOrWhiteSpace(txtAd.Text) || string.IsNullOrWhiteSpace(txtYazar.Text))
            {
                MessageBox.Show("Lütfen kitap adı ve yazar alanlarını doldurunuz.", "Eksik Bilgi", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            await this.RunAsync(async () =>
            {
                await _bookService.AddAsync(txtAd.Text, txtYazar.Text, txtIsbn.Text);
                await ListBooksAsync();

                txtAd.Clear();
                txtYazar.Clear();
                txtIsbn.Clear();
            });
        }

        private void dataGridView1_CellClick(object sender, DataGridViewCellEventArgs e)
        {
            if (e.RowIndex >= 0 && dataGridView1.Rows[e.RowIndex].DataBoundItem is Book book)
            {
                _selectedBookId = book.Id;
            }
        }

        private async void btnSil_Click(object sender, EventArgs e)
        {
            if (_selectedBookId is not int bookId)
            {
                MessageBox.Show("Lütfen silinecek kitabı seçin.", "Uyarı", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            DialogResult result = MessageBox.Show("Seçili kitabı silmek istediğinize emin misiniz?", "Onay", MessageBoxButtons.YesNo, MessageBoxIcon.Question);
            if (result != DialogResult.Yes)
            {
                return;
            }

            await this.RunAsync(async () =>
            {
                await _bookService.DeleteAsync(bookId);
                MessageBox.Show("Kitap silindi.", "Bilgi", MessageBoxButtons.OK, MessageBoxIcon.Information);
                _selectedBookId = null;
                await ListBooksAsync();
            });
        }
    }
}

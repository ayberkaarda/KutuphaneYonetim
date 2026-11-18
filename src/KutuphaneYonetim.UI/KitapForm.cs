using KutuphaneYonetim.Business.Services;
using KutuphaneYonetim.Domain.Entities;
using KutuphaneYonetim.UI.Infrastructure;

namespace KutuphaneYonetim.UI
{
    /// <summary>Lists, adds and deletes books.</summary>
    public partial class KitapForm : Form
    {
        private static readonly Dictionary<string, string> ColumnHeaders = new()
        {
            [nameof(Book.Id)] = "Kitap No",
            [nameof(Book.Title)] = "Kitap Adı",
            [nameof(Book.Author)] = "Yazar",
            [nameof(Book.IsAvailable)] = "Rafta",
        };

        private readonly IBookService _bookService;
        private int? _selectedBookId;

        public KitapForm(IBookService bookService)
        {
            _bookService = bookService;
            InitializeComponent();
        }

        private async Task ListBooksAsync()
        {
            dataGridView1.DataSource = await _bookService.GetAllAsync();
            dataGridView1.ConfigureColumns(ColumnHeaders);
        }

        private async void KitapForm_Load(object sender, EventArgs e)
        {
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
                await _bookService.AddAsync(txtAd.Text, txtYazar.Text);
                await ListBooksAsync();

                txtAd.Clear();
                txtYazar.Clear();
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

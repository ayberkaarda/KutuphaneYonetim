using KutuphaneYonetim.Business.Services;
using KutuphaneYonetim.Domain.Entities;
using KutuphaneYonetim.UI.Infrastructure;

namespace KutuphaneYonetim.UI
{
    /// <summary>
    /// Reserves books that are on loan, shows the queues (who a returned book is held for and until when)
    /// and cancels reservations.
    /// </summary>
    public partial class RezervasyonForm : Form
    {
        private static readonly Dictionary<string, string> ColumnHeaders = new()
        {
            [nameof(ReservationDetail.Id)] = "Rezervasyon No",
            [nameof(ReservationDetail.BookTitle)] = "Kitap",
            [nameof(ReservationDetail.MemberName)] = "Üye",
            [nameof(ReservationDetail.CreatedAt)] = "Rezervasyon Tarihi",
            [nameof(ReservationDetail.QueuePosition)] = "Sıra",
            [nameof(ReservationDetail.Status)] = "Durum",
            [nameof(ReservationDetail.ExpiresAt)] = "Son Alma Tarihi",
        };

        private readonly IBookService _bookService;
        private readonly IMemberService _memberService;
        private readonly IReservationService _reservationService;
        private int? _selectedReservationId;

        public RezervasyonForm(IBookService bookService, IMemberService memberService, IReservationService reservationService)
        {
            _bookService = bookService;
            _memberService = memberService;
            _reservationService = reservationService;
            InitializeComponent();
        }

        private async void RezervasyonForm_Load(object sender, EventArgs e)
        {
            await this.RunAsync(async () =>
            {
                cmbKitaplar.DisplayMember = nameof(Book.Title);
                cmbKitaplar.ValueMember = nameof(Book.Id);
                cmbKitaplar.DataSource = (await _bookService.GetAllAsync()).OrderBy(b => b.Title, StringComparer.CurrentCulture).ToList();

                cmbUyeler.DisplayMember = nameof(Member.FullName);
                cmbUyeler.ValueMember = nameof(Member.Id);
                cmbUyeler.DataSource = await _memberService.GetAllAsync();

                await ListReservationsAsync();
            });
        }

        private async Task ListReservationsAsync()
        {
            dataGridView1.DataSource = await _reservationService.GetActiveAsync();
            dataGridView1.ConfigureColumns(ColumnHeaders);
            _selectedReservationId = null;
        }

        private async void btnRezervasyon_Click(object sender, EventArgs e)
        {
            if (cmbKitaplar.SelectedValue is not int bookId || cmbUyeler.SelectedValue is not int memberId)
            {
                MessageBox.Show("Lütfen rezerve edilecek kitabı ve üyeyi seçin.", "Uyarı", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            await this.RunAsync(async () =>
            {
                await _reservationService.ReserveAsync(bookId, memberId);
                MessageBox.Show("Rezervasyon sıraya eklendi.", "Bilgi", MessageBoxButtons.OK, MessageBoxIcon.Information);
                await ListReservationsAsync();
            });
        }

        private async void btnIptal_Click(object sender, EventArgs e)
        {
            if (_selectedReservationId is not int reservationId)
            {
                MessageBox.Show("Lütfen iptal edilecek rezervasyonu seçin.", "Uyarı", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            DialogResult result = MessageBox.Show("Seçili rezervasyonu iptal etmek istediğinize emin misiniz?", "Onay", MessageBoxButtons.YesNo, MessageBoxIcon.Question);
            if (result != DialogResult.Yes)
            {
                return;
            }

            await this.RunAsync(async () =>
            {
                await _reservationService.CancelAsync(reservationId);
                MessageBox.Show("Rezervasyon iptal edildi.", "Bilgi", MessageBoxButtons.OK, MessageBoxIcon.Information);
                await ListReservationsAsync();
            });
        }

        private void dataGridView1_CellClick(object sender, DataGridViewCellEventArgs e)
        {
            if (e.RowIndex >= 0 && dataGridView1.Rows[e.RowIndex].DataBoundItem is ReservationDetail reservation)
            {
                _selectedReservationId = reservation.Id;
            }
        }

        private void dataGridView1_CellFormatting(object sender, DataGridViewCellFormattingEventArgs e)
        {
            if (e.Value is ReservationStatus status)
            {
                e.Value = status == ReservationStatus.Ready ? "Kitap ayrıldı" : "Sırada";
                e.FormattingApplied = true;
            }
        }
    }
}

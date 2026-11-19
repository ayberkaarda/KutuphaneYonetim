using System.ComponentModel;
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
        private readonly ExclusiveOperation _write = new();
        private int? _selectedReservationId;

        public RezervasyonForm(IBookService bookService, IMemberService memberService, IReservationService reservationService)
        {
            _bookService = bookService;
            _memberService = memberService;
            _reservationService = reservationService;
            InitializeComponent();
        }

        [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        internal UserPrompts Prompts { get; set; } = UserPrompts.Default;

        private Control[] WriteButtons => [btnRezervasyon, btnIptal];

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

        private async void btnRezervasyon_Click(object sender, EventArgs e) => await ReserveAsync();

        /// <summary>Reserves the chosen book; a second click while the first reservation is pending is ignored.</summary>
        internal Task ReserveAsync() => this.RunExclusiveAsync(
            _write,
            async () =>
            {
                if (cmbKitaplar.SelectedValue is not int bookId || cmbUyeler.SelectedValue is not int memberId)
                {
                    Prompts.Warn("Lütfen rezerve edilecek kitabı ve üyeyi seçin.");
                    return;
                }

                await _reservationService.ReserveAsync(bookId, memberId);
                Prompts.Inform("Rezervasyon sıraya eklendi.");
                await ListReservationsAsync();
            },
            WriteButtons);

        private async void btnIptal_Click(object sender, EventArgs e) => await CancelReservationAsync();

        /// <summary>Cancels the selected reservation after confirmation; ignored while another write is pending.</summary>
        internal Task CancelReservationAsync() => this.RunExclusiveAsync(
            _write,
            async () =>
            {
                if (_selectedReservationId is not int reservationId)
                {
                    Prompts.Warn("Lütfen iptal edilecek rezervasyonu seçin.");
                    return;
                }

                if (!Prompts.Confirm("Seçili rezervasyonu iptal etmek istediğinize emin misiniz?"))
                {
                    return;
                }

                await _reservationService.CancelAsync(reservationId);
                Prompts.Inform("Rezervasyon iptal edildi.");
                await ListReservationsAsync();
            },
            WriteButtons);

        private void dataGridView1_CellClick(object sender, DataGridViewCellEventArgs e)
        {
            if (e.RowIndex >= 0 && dataGridView1.Rows[e.RowIndex].DataBoundItem is ReservationDetail reservation)
            {
                SelectReservation(reservation);
            }
        }

        /// <summary>Makes <paramref name="reservation"/> (a row of the list) the one to cancel.</summary>
        internal void SelectReservation(ReservationDetail reservation) => _selectedReservationId = reservation.Id;

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

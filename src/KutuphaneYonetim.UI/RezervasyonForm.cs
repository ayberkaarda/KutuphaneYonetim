using System.Globalization;
using KutuphaneYonetim.Business.Services;
using KutuphaneYonetim.Domain.Entities;
using KutuphaneYonetim.UI.Infrastructure;
using KutuphaneYonetim.UI.Theming;

namespace KutuphaneYonetim.UI
{
    /// <summary>
    /// Reserves books that are on loan, shows the queues (who a returned book is held for and until when)
    /// and cancels reservations.
    /// </summary>
    public sealed class RezervasyonForm : AppDialog
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
        private readonly RequestSequence _listRequests = new();
        private ReservationDetail? _selectedReservation;

        private readonly ThemedComboBox cmbKitaplar = FormLayout.Picker(nameof(cmbKitaplar), 260, "Kitap seçin");
        private readonly ThemedComboBox cmbUyeler = FormLayout.Picker(nameof(cmbUyeler), 220, "Üye seçin");
        private readonly ThemedGrid dataGridView1 = new() { Name = nameof(dataGridView1), AccessibleName = "Etkin rezervasyonlar" };
        private readonly ThemedButton btnRezervasyon = FormLayout.Button(nameof(btnRezervasyon), "Sıraya Ekle", ButtonKind.Primary);
        private readonly ThemedButton btnIptal = FormLayout.Button(nameof(btnIptal), "Seçili Rezervasyonu İptal Et", ButtonKind.Danger);
        private readonly Label lblListeOzet = FormLayout.Hint(string.Empty);

        public RezervasyonForm(IBookService bookService, IMemberService memberService, IReservationService reservationService)
            : base("Rezervasyonlar", "Ödünçteki kitaplar için sıra açın; iade edilen kitap sıradaki üyeye ayrılır.", Glyphs.Reservation, new Size(1040, 640))
        {
            _bookService = bookService;
            _memberService = memberService;
            _reservationService = reservationService;
            BuildLayout();

            Load += async (_, _) => await this.RunAsync(LoadAsync);
            btnRezervasyon.Click += async (_, _) => await ReserveAsync();
            btnIptal.Click += async (_, _) => await CancelReservationAsync();
            dataGridView1.CellClick += (_, e) => SelectRow(e.RowIndex);
            dataGridView1.CurrentCellChanged += (_, _) =>
            {
                // Arrow keys choose a row too, but only while the user is in the list.
                if (dataGridView1.Focused && dataGridView1.CurrentRow is { } row)
                {
                    SelectRow(row.Index);
                }
            };
            dataGridView1.CellFormatting += FormatStatus;
        }

        private Control[] WriteButtons => [btnRezervasyon, btnIptal];

        /// <summary>Initial load of the pickers and the reservation list.</summary>
        internal async Task LoadAsync()
        {
            cmbKitaplar.DisplayMember = nameof(Book.Title);
            cmbKitaplar.ValueMember = nameof(Book.Id);
            cmbKitaplar.DataSource = (await _bookService.GetAllAsync()).OrderBy(b => b.Title, StringComparer.CurrentCulture).ToList();

            cmbUyeler.DisplayMember = nameof(Member.FullName);
            cmbUyeler.ValueMember = nameof(Member.Id);
            cmbUyeler.DataSource = await _memberService.GetAllAsync();

            await ListReservationsAsync();
        }

        /// <summary>
        /// Loads the reservation list; only the most recent of overlapping loads is shown, and the selection is
        /// cleared together with the list it belonged to.
        /// </summary>
        private async Task ListReservationsAsync()
        {
            var request = _listRequests.Next();
            dataGridView1.IsLoading = true;
            try
            {
                var active = await _reservationService.GetActiveAsync();
                if (!_listRequests.IsLatest(request))
                {
                    return;
                }

                dataGridView1.DataSource = active;
                dataGridView1.ConfigureColumns(ColumnHeaders);
                dataGridView1.FormatDateTimeColumns(nameof(ReservationDetail.CreatedAt), nameof(ReservationDetail.ExpiresAt));
                dataGridView1.FitWideColumns(160, 76, nameof(ReservationDetail.BookTitle), nameof(ReservationDetail.MemberName));
                _selectedReservation = null;
                dataGridView1.SelectedItem = null;
                var ready = active.Count(r => r.Status == ReservationStatus.Ready);
                lblListeOzet.Text = string.Format(CultureInfo.CurrentCulture, "{0} etkin rezervasyon · {1} kitap ayrıldı", active.Count, ready);
            }
            finally
            {
                if (_listRequests.IsLatest(request))
                {
                    dataGridView1.IsLoading = false;
                }
            }
        }

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

        /// <summary>Cancels the selected reservation after confirmation; ignored while another write is pending.</summary>
        internal Task CancelReservationAsync() => this.RunExclusiveAsync(
            _write,
            async () =>
            {
                if (_selectedReservation is not { } reservation)
                {
                    Prompts.Warn("Lütfen iptal edilecek rezervasyonu seçin.");
                    return;
                }

                if (!Prompts.Confirm(string.Format(
                    CultureInfo.CurrentCulture,
                    "{0} adlı üyenin \"{1}\" için rezervasyonu (No {2}) iptal edilsin mi?",
                    reservation.MemberName,
                    reservation.BookTitle,
                    reservation.Id)))
                {
                    return;
                }

                await _reservationService.CancelAsync(reservation.Id);
                Prompts.Inform("Rezervasyon iptal edildi.");
                await ListReservationsAsync();
            },
            WriteButtons);

        /// <summary>Makes <paramref name="reservation"/> (a row of the list) the one to cancel.</summary>
        internal void SelectReservation(ReservationDetail reservation)
        {
            _selectedReservation = reservation;
            dataGridView1.SelectedItem = reservation;
        }

        private void SelectRow(int rowIndex)
        {
            if (rowIndex >= 0 && dataGridView1.Rows[rowIndex].DataBoundItem is ReservationDetail reservation)
            {
                SelectReservation(reservation);
            }
        }

        /// <summary>The status reads as Turkish text; a book held for the member stands out in the success color.</summary>
        internal void FormatStatus(object? sender, DataGridViewCellFormattingEventArgs e)
        {
            if (e.Value is not ReservationStatus status)
            {
                return;
            }

            e.Value = status == ReservationStatus.Ready ? "Kitap ayrıldı" : "Sırada";
            e.FormattingApplied = true;
            if (e.CellStyle is not null && status == ReservationStatus.Ready)
            {
                ThemeStyler.EmphasizeCell(e.CellStyle, Theme.Current.Success);
            }
        }

        private void BuildLayout()
        {
            Name = nameof(RezervasyonForm);

            var fields = FormLayout.FieldRow(
                "reserveFields",
                (FormLayout.Field("&Kitap", cmbKitaplar), 55),
                (FormLayout.Field("Ü&ye", cmbUyeler), 45),
                (FormLayout.FieldAction(btnRezervasyon), 0));
            var hint = FormLayout.Hint("Yalnızca ödünçteki kitaplar sıraya alınır. Kitap iade edilince sıradaki üyeye belirli bir süre ayrılır.");
            hint.Name = "reserveHint";
            hint.Margin = new Padding(0, Spacing.M, 0, 0);
            var reserveCard = FormLayout.Card("reserveCard", "Yeni rezervasyon", FormLayout.Stack("reserveStack", fields, hint), fill: false);

            btnIptal.Margin = Padding.Empty;
            lblListeOzet.Name = nameof(lblListeOzet);
            lblListeOzet.Margin = new Padding(Spacing.M, 0, 0, 0);
            var actions = FormLayout.Bar("actionBar", (btnIptal, false), (lblListeOzet, true));
            actions.Margin = new Padding(0, Spacing.M, 0, 0);
            dataGridView1.EmptyText = "Etkin rezervasyon yok. Ödünçteki bir kitabı yukarıdan sıraya ekleyebilirsiniz.";
            var listCard = FormLayout.Card("listCard", "Etkin rezervasyonlar", FormLayout.Page((dataGridView1, true), (actions, false)));

            Content.Controls.Add(FormLayout.Page((reserveCard, false), (listCard, true)));
            SetFooterNote("Süresi dolan ayırmalar sıradaki üyeye geçer.");
            SetFooterButtons(CreateCloseButton());
            FinishLayout();
        }
    }
}

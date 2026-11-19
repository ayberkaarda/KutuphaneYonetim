using System.Globalization;
using KutuphaneYonetim.Business.Services;
using KutuphaneYonetim.Domain;
using KutuphaneYonetim.Domain.Entities;
using KutuphaneYonetim.UI.Infrastructure;
using KutuphaneYonetim.UI.Theming;

namespace KutuphaneYonetim.UI
{
    /// <summary>Searches, lists, adds and deletes books.</summary>
    public sealed class KitapForm : AppDialog
    {
        private static readonly Dictionary<string, string> ColumnHeaders = new()
        {
            [nameof(Book.Id)] = "Kitap No",
            [nameof(Book.Title)] = "Kitap Adı",
            [nameof(Book.Author)] = "Yazar",
            [nameof(Book.Isbn)] = "ISBN",
            [nameof(Book.IsAvailable)] = "Durum",
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

        private readonly TextBox txtAd = new() { Name = nameof(txtAd), MaxLength = 200 };
        private readonly TextBox txtYazar = new() { Name = nameof(txtYazar), MaxLength = 200 };
        private readonly TextBox txtIsbn = new() { Name = nameof(txtIsbn), MaxLength = 17, PlaceholderText = "978-605-…" };
        private readonly TextBox txtAra = new() { Name = nameof(txtAra), PlaceholderText = "Kitap adı, yazar ya da ISBN", AccessibleName = "Ara" };
        private readonly ThemedComboBox cmbDurum = FormLayout.Picker(nameof(cmbDurum), 210);
        private readonly ThemedGrid dataGridView1 = new() { Name = nameof(dataGridView1), AccessibleName = "Kitap listesi" };
        private readonly ThemedButton btnEkle = FormLayout.Button(nameof(btnEkle), "Kitap Ekle", ButtonKind.Primary);
        private readonly ThemedButton btnAra = FormLayout.Button(nameof(btnAra), "Ara");
        private readonly ThemedButton btnSil = FormLayout.Button(nameof(btnSil), "Seçili Kitabı Sil", ButtonKind.Danger);
        private readonly ThemedButton btnOnceki = FormLayout.Button(nameof(btnOnceki), "‹ Önceki", ButtonKind.Subtle);
        private readonly ThemedButton btnSonraki = FormLayout.Button(nameof(btnSonraki), "Sonraki ›", ButtonKind.Subtle);
        private readonly Label lblSayfa = FormLayout.Hint(string.Empty);

        public KitapForm(IBookService bookService)
            : base("Kitaplar", "Kataloğa kitap ekleyin; ada, yazara ya da ISBN'e göre arayın ve durumuna göre süzün.", Glyphs.Library, new Size(1040, 640))
        {
            _bookService = bookService;
            BuildLayout();
            _pager = new Pager(btnOnceki, btnSonraki, lblSayfa);

            Load += async (_, _) => await this.RunAsync(LoadAsync);
            btnAra.Click += async (_, _) => await SearchFromFirstPageAsync();
            cmbDurum.SelectionChangeCommitted += async (_, _) => await SearchFromFirstPageAsync();
            txtAra.KeyDown += async (_, e) =>
            {
                if (e.KeyCode == Keys.Enter)
                {
                    e.SuppressKeyPress = true;
                    await SearchFromFirstPageAsync();
                }
            };
            btnOnceki.Click += async (_, _) =>
            {
                _pager.MovePrevious();
                await this.RunAsync(ListBooksAsync);
            };
            btnSonraki.Click += async (_, _) =>
            {
                _pager.MoveNext();
                await this.RunAsync(ListBooksAsync);
            };
            btnEkle.Click += async (_, _) => await AddBookAsync();
            btnSil.Click += async (_, _) => await DeleteBookAsync();
            dataGridView1.CellClick += (_, e) => SelectRow(e.RowIndex);
            dataGridView1.CurrentCellChanged += (_, _) =>
            {
                // Arrow keys choose a row too, but only while the user is in the list.
                if (dataGridView1.Focused && dataGridView1.CurrentRow is { } row)
                {
                    SelectRow(row.Index);
                }
            };
            dataGridView1.CellPainting += PaintAvailability;
        }

        private Control[] WriteButtons => [btnEkle, btnSil];

        /// <summary>Fills the status filter and lists the first page; runs when the window opens.</summary>
        internal async Task LoadAsync()
        {
            cmbDurum.BindOptions(AvailabilityOptions);
            await ListBooksAsync();
        }

        /// <summary>Loads the current page; only the most recent of overlapping loads is shown.</summary>
        internal async Task ListBooksAsync()
        {
            var request = _listRequests.Next();
            _selectedBook = null;
            var query = new BookQuery(txtAra.Text, cmbDurum.SelectedOption(BookAvailability.All), _pager.Page);
            dataGridView1.IsLoading = true;
            try
            {
                var result = await _bookService.SearchAsync(query);
                if (!_listRequests.IsLatest(request))
                {
                    return;
                }

                dataGridView1.EmptyText = string.IsNullOrWhiteSpace(query.Text) && query.Availability == BookAvailability.All
                    ? "Katalogda henüz kitap yok. Soldaki formdan ilk kitabı ekleyin."
                    : "Aramanıza uyan kitap bulunamadı.";
                dataGridView1.DataSource = result.Items;
                dataGridView1.ConfigureColumns(ColumnHeaders);
                dataGridView1.FitColumns(120, nameof(Book.Title), nameof(Book.Author));
                FitAvailabilityColumn();
                if (dataGridView1.Columns[nameof(Book.Title)] is { } title)
                {
                    title.FillWeight = 140; // Titles run longer than author names.
                }

                dataGridView1.SelectedItem = null;
                _pager.Show(result);
                _selectedBook = null;
            }
            finally
            {
                if (_listRequests.IsLatest(request))
                {
                    dataGridView1.IsLoading = false;
                }
            }
        }

        private async Task SearchFromFirstPageAsync()
        {
            _pager.Reset();
            await this.RunAsync(ListBooksAsync);
        }

        /// <summary>Adds the entered book; a second click while the first add is pending is ignored.</summary>
        internal Task AddBookAsync() => this.RunExclusiveAsync(
            _write,
            async () =>
            {
                if (string.IsNullOrWhiteSpace(txtAd.Text) || string.IsNullOrWhiteSpace(txtYazar.Text))
                {
                    Prompts.Warn("Lütfen kitap adı ve yazar alanlarını doldurunuz.");
                    (string.IsNullOrWhiteSpace(txtAd.Text) ? txtAd : txtYazar).Focus();
                    return;
                }

                await _bookService.AddAsync(txtAd.Text, txtYazar.Text, txtIsbn.Text);
                await ListBooksAsync();

                Prompts.Inform(string.Format(CultureInfo.CurrentCulture, "\"{0}\" kataloğa eklendi.", txtAd.Text.Trim()));
                txtAd.Clear();
                txtYazar.Clear();
                txtIsbn.Clear();
                txtAd.Focus();
            },
            WriteButtons);

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

        private void SelectRow(int rowIndex)
        {
            if (rowIndex >= 0 && dataGridView1.Rows[rowIndex].DataBoundItem is Book book)
            {
                _selectedBook = book;
                dataGridView1.SelectedItem = book;
            }
        }

        /// <summary>The availability column is painted as a word, so it is sized for the longer word.</summary>
        private void FitAvailabilityColumn()
        {
            if (dataGridView1.Columns.Cast<DataGridViewColumn>().FirstOrDefault(c => c.DataPropertyName == nameof(Book.IsAvailable)) is not { } column)
            {
                return;
            }

            var text = TextRenderer.MeasureText("Müsait değil", Theme.Fonts.Body).Width;
            var header = TextRenderer.MeasureText(column.HeaderText, Theme.Fonts.BodyStrong).Width;
            var padding = dataGridView1.DefaultCellStyle.Padding.Horizontal + Theme.Px(dataGridView1, 8 + Spacing.S + Spacing.S);
            column.AutoSizeMode = DataGridViewAutoSizeColumnMode.None;
            column.Width = Math.Max(text, header) + padding;
        }

        /// <summary>The availability column reads as a word with a colored dot instead of a check box.</summary>
        private void PaintAvailability(object? sender, DataGridViewCellPaintingEventArgs e)
        {
            if (e.RowIndex < 0 || e.ColumnIndex < 0 || e.Graphics is null
                || dataGridView1.Columns[e.ColumnIndex].DataPropertyName != nameof(Book.IsAvailable)
                || e.Value is not bool available)
            {
                return;
            }

            var p = Theme.Current;
            e.PaintBackground(e.CellBounds, (e.State & DataGridViewElementStates.Selected) != 0);
            var color = available ? p.Success : p.Warning;
            var text = available ? "Müsait" : "Müsait değil";
            var pad = e.CellStyle?.Padding.Left ?? 0;
            var dot = Theme.PxF(dataGridView1, 8);
            var bounds = e.CellBounds;
            Painting.FillDot(e.Graphics, new RectangleF(bounds.Left + pad, bounds.Top + ((bounds.Height - dot) / 2F), dot, dot), color);
            var textLeft = bounds.Left + pad + (int)dot + Theme.Px(dataGridView1, Spacing.S);
            TextRenderer.DrawText(
                e.Graphics,
                text,
                Theme.Fonts.Body,
                new Rectangle(textLeft, bounds.Top, Math.Max(0, bounds.Right - textLeft), bounds.Height),
                AvailabilityTextColor(p, color, (e.State & DataGridViewElementStates.Selected) != 0),
                TextFormatFlags.VerticalCenter | TextFormatFlags.SingleLine | TextFormatFlags.NoPrefix | TextFormatFlags.EndEllipsis);
            e.Handled = true;
        }

        /// <summary>
        /// Text color of the availability column: the status color, or the palette's own text pair under high
        /// contrast (HighlightText on the selected row, which is drawn on Highlight).
        /// </summary>
        internal static Color AvailabilityTextColor(ThemePalette palette, Color statusColor, bool selected) =>
            !palette.IsHighContrast ? statusColor : selected ? palette.OnAccent : palette.Text;

        private void BuildLayout()
        {
            Name = nameof(KitapForm);

            var editor = FormLayout.Fields(
                "bookEditor",
                [FormLayout.Field("Kitap &adı", FormLayout.Input(txtAd))],
                [FormLayout.Field("&Yazar", FormLayout.Input(txtYazar))],
                [FormLayout.Field("&ISBN", FormLayout.Input(txtIsbn), "İsteğe bağlı; ISBN-10 ya da ISBN-13.")]);
            btnEkle.Margin = new Padding(0, Spacing.XS, 0, 0);
            var editorCard = FormLayout.Card("editorCard", "Yeni kitap", FormLayout.Stack("editorStack", editor, FormLayout.Row(btnEkle)));

            var search = FormLayout.Input(txtAra);
            search.Margin = new Padding(0, 0, Spacing.S, 0);
            cmbDurum.AccessibleName = "Durum";
            cmbDurum.Margin = new Padding(0, 0, Spacing.S, 0);
            btnAra.Margin = Padding.Empty;
            var searchBar = FormLayout.Bar("searchBar", (search, true), (cmbDurum, false), (btnAra, false));
            searchBar.Margin = new Padding(0, 0, 0, Spacing.M);

            btnSil.Margin = Padding.Empty;
            btnOnceki.Margin = Padding.Empty;
            btnSonraki.Margin = Padding.Empty;
            lblSayfa.Name = nameof(lblSayfa);
            lblSayfa.Margin = new Padding(Spacing.S, 0, Spacing.S, 0);
            var actions = FormLayout.Bar("actionBar", (btnSil, false), (FormLayout.Spacer(), true), (btnOnceki, false), (lblSayfa, false), (btnSonraki, false));
            actions.Margin = new Padding(0, Spacing.M, 0, 0);

            var list = FormLayout.Page((searchBar, false), (dataGridView1, true), (actions, false));
            var listCard = FormLayout.Card("listCard", "Katalog", list);

            Content.Controls.Add(FormLayout.Split(editorCard, listCard));
            SetFooterNote("Ödünçte olan ya da ödünç geçmişi bulunan kitaplar silinemez.");
            SetFooterButtons(CreateCloseButton());
            FinishLayout();
        }
    }
}

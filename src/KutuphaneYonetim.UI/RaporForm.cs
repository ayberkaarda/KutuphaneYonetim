using System.Globalization;
using KutuphaneYonetim.Business.Reports;
using KutuphaneYonetim.UI.Infrastructure;
using KutuphaneYonetim.UI.Theming;

namespace KutuphaneYonetim.UI
{
    /// <summary>
    /// Overdue loans, most borrowed books (date range) and unpaid fines. The table on screen is exported
    /// to CSV exactly as shown.
    /// </summary>
    public sealed class RaporForm : AppDialog
    {
        internal enum ReportKind
        {
            OverdueLoans,
            MostBorrowed,
            UnpaidFines,
        }

        private static readonly ListOption<ReportKind>[] ReportOptions =
        [
            new("Geciken kitaplar", ReportKind.OverdueLoans),
            new("En çok ödünç alınanlar", ReportKind.MostBorrowed),
            new("Ödenmemiş cezalar", ReportKind.UnpaidFines),
        ];

        private static readonly Dictionary<ReportKind, string> FileNames = new()
        {
            [ReportKind.OverdueLoans] = "geciken-kitaplar",
            [ReportKind.MostBorrowed] = "en-cok-odunc-alinanlar",
            [ReportKind.UnpaidFines] = "odenmemis-cezalar",
        };

        private static readonly Dictionary<ReportKind, string> EmptyTexts = new()
        {
            [ReportKind.OverdueLoans] = "Geciken kitap yok; bütün açık ödünçlerin teslim tarihi henüz gelmedi.",
            [ReportKind.MostBorrowed] = "Seçilen tarih aralığında ödünç verilen kitap yok.",
            [ReportKind.UnpaidFines] = "Ödenmemiş ceza yok.",
        };

        /// <summary>Text columns that share the free width; the rest fit their values.</summary>
        private static readonly HashSet<string> WideHeaders = ["Kitap", "Üye", "Yazar"];

        private readonly IReportService _reportService;
        private readonly ICsvExporter _csvExporter;
        private readonly RequestSequence _reportRequests = new();
        private readonly ExclusiveOperation _export = new();
        private ReportTable? _shownTable;
        private ReportKind _shownKind;

        private readonly ThemedComboBox cmbRapor = FormLayout.Picker(nameof(cmbRapor), 240);
        private readonly DateTimePicker dtpBaslangic = FormLayout.DatePicker(nameof(dtpBaslangic));
        private readonly DateTimePicker dtpBitis = FormLayout.DatePicker(nameof(dtpBitis));
        private readonly NumericUpDown nudAdet = new() { Name = nameof(nudAdet), Width = 72, TextAlign = HorizontalAlignment.Right };
        private readonly ThemedButton btnGoster = FormLayout.Button(nameof(btnGoster), "Göster", ButtonKind.Primary);
        private readonly ThemedButton btnCsv = FormLayout.Button(nameof(btnCsv), "CSV Olarak Dışa Aktar");
        private readonly Label lblOzet = FormLayout.Hint(string.Empty);
        private readonly Label lblAralikIpucu = FormLayout.Hint(string.Empty);
        private readonly ThemedGrid dataGridView1 = new() { Name = nameof(dataGridView1), AccessibleName = "Rapor" };
        private Label _tableTitle = null!;

        public RaporForm(IReportService reportService, ICsvExporter csvExporter)
            : base("Raporlar", "Gecikenleri, en çok ödünç alınan kitapları ve ödenmemiş cezaları görün; tabloyu CSV olarak alın.", Glyphs.Report, new Size(1040, 660))
        {
            _reportService = reportService;
            _csvExporter = csvExporter;
            BuildLayout();

            Load += async (_, _) =>
            {
                InitializeFilters();
                await this.RunAsync(ShowReportAsync);
            };
            cmbRapor.SelectionChangeCommitted += async (_, _) =>
            {
                UpdateFilterControls();
                await this.RunAsync(ShowReportAsync);
            };
            btnGoster.Click += async (_, _) => await this.RunAsync(ShowReportAsync);
            btnCsv.Click += async (_, _) => await this.RunExclusiveAsync(_export, ExportCsvAsync, btnCsv);
        }

        /// <summary>The table on screen (what CSV export writes), from the most recent report request.</summary>
        internal ReportTable? ShownTable => _shownTable;

        /// <summary>The kind of <see cref="ShownTable"/>.</summary>
        internal ReportKind ShownKind => _shownKind;

        private ReportKind SelectedKind => cmbRapor.SelectedOption(ReportKind.OverdueLoans);

        /// <summary>Fills the report list and the default date range and count.</summary>
        internal void InitializeFilters()
        {
            cmbRapor.BindOptions(ReportOptions);
            dtpBitis.Value = DateTime.Today;
            dtpBaslangic.Value = DateTime.Today.AddDays(-30);
            nudAdet.Minimum = 1;
            nudAdet.Maximum = 100;
            nudAdet.Value = 10;
            UpdateFilterControls();
        }

        /// <summary>The date range and count apply to the most borrowed report only.</summary>
        internal void UpdateFilterControls()
        {
            var range = SelectedKind == ReportKind.MostBorrowed;
            dtpBaslangic.Enabled = range;
            dtpBitis.Enabled = range;
            nudAdet.Enabled = range;
            lblAralikIpucu.Text = range
                ? "Seçilen tarihler arasında en çok ödünç alınan kitaplar, ödünç sayısına göre sıralanır."
                : "Tarih aralığı ve kayıt sayısı yalnızca \"En çok ödünç alınanlar\" raporunda kullanılır.";
        }

        /// <summary>
        /// Loads the selected report. When reports are requested faster than they load, only the most recent
        /// request is shown, so the table and its kind (used for the CSV file name) always belong together.
        /// </summary>
        internal async Task ShowReportAsync()
        {
            var request = _reportRequests.Next();
            var kind = SelectedKind;
            dataGridView1.IsLoading = true;
            try
            {
                var table = kind switch
                {
                    ReportKind.MostBorrowed => ReportTables.MostBorrowed(
                        await _reportService.GetMostBorrowedAsync(dtpBaslangic.Value.Date, dtpBitis.Value.Date, (int)nudAdet.Value)),
                    ReportKind.UnpaidFines => ReportTables.UnpaidFines(await _reportService.GetUnpaidFinesAsync()),
                    _ => ReportTables.OverdueLoans(await _reportService.GetOverdueLoansAsync()),
                };
                if (!_reportRequests.IsLatest(request))
                {
                    return;
                }

                dataGridView1.EmptyText = EmptyTexts[kind];
                ShowTable(table);
                _shownTable = table;
                _shownKind = kind;
                _tableTitle.Text = ReportOptions.Single(o => o.Value == kind).Text;
                lblOzet.Text = string.Format(CultureInfo.CurrentCulture, "{0} kayıt", table.Rows.Count);
            }
            finally
            {
                if (_reportRequests.IsLatest(request))
                {
                    dataGridView1.IsLoading = false;
                }
            }
        }

        private void ShowTable(ReportTable table)
        {
            dataGridView1.DataSource = null;
            dataGridView1.Rows.Clear();
            dataGridView1.Columns.Clear();

            foreach (var header in table.Headers)
            {
                var column = new DataGridViewTextBoxColumn
                {
                    HeaderText = header,
                    SortMode = DataGridViewColumnSortMode.NotSortable,
                    AutoSizeMode = WideHeaders.Contains(header) ? DataGridViewAutoSizeColumnMode.Fill : DataGridViewAutoSizeColumnMode.AllCells,
                };
                if (WideHeaders.Contains(header))
                {
                    column.MinimumWidth = Theme.Px(dataGridView1, 140);
                }
                else
                {
                    column.MinimumWidth = UiExtensions.HeaderWidth(dataGridView1, header, wholeText: true);
                    if (IsNumeric(header))
                    {
                        column.DefaultCellStyle.Alignment = DataGridViewContentAlignment.MiddleRight;
                        column.HeaderCell.Style.Alignment = DataGridViewContentAlignment.MiddleRight;
                    }
                }

                dataGridView1.Columns.Add(column);
            }

            foreach (var row in table.Rows)
            {
                dataGridView1.Rows.Add(row.ToArray<object>());
            }

            dataGridView1.ClearSelection();
        }

        /// <summary>Counts, numbers and amounts are right aligned so their digits line up.</summary>
        private static bool IsNumeric(string header) =>
            header.EndsWith("(TL)", StringComparison.Ordinal) || header.EndsWith("(Gün)", StringComparison.Ordinal)
            || header.EndsWith(" No", StringComparison.Ordinal) || header is "Sıra" or "Ödünç Sayısı";

        private async Task ExportCsvAsync()
        {
            if (_shownTable is not { } table)
            {
                Prompts.Warn("Önce bir rapor görüntüleyin.");
                return;
            }

            using var dialog = new SaveFileDialog
            {
                Filter = "CSV dosyası (*.csv)|*.csv",
                DefaultExt = "csv",
                AddExtension = true,
                OverwritePrompt = true,
                FileName = string.Format(
                    CultureInfo.InvariantCulture, "{0}-{1:yyyyMMdd}.csv", FileNames[_shownKind], DateTime.Today),
            };
            if (dialog.ShowDialog(this) != DialogResult.OK)
            {
                return;
            }

            await _csvExporter.ExportAsync(table, dialog.FileName);
            Prompts.Inform(string.Format(
                CultureInfo.CurrentCulture, "{0} satır CSV dosyasına aktarıldı: {1}", table.Rows.Count, Path.GetFileName(dialog.FileName)));
        }

        private void BuildLayout()
        {
            Name = nameof(RaporForm);

            nudAdet.Font = Theme.Fonts.Body;
            btnCsv.Margin = new Padding(Spacing.S, Spacing.XS, 0, 0);
            var filters = FormLayout.FieldRow(
                "reportFilters",
                (FormLayout.Field("&Rapor", cmbRapor), 100),
                (FormLayout.Field("&Başlangıç", dtpBaslangic), 0),
                (FormLayout.Field("Bi&tiş", dtpBitis), 0),
                (FormLayout.Field("İ&lk", nudAdet), 0),
                (FormLayout.FieldAction(btnGoster), 0),
                (FormLayout.FieldAction(btnCsv), 0));
            lblAralikIpucu.Name = nameof(lblAralikIpucu);
            lblAralikIpucu.Margin = new Padding(0, Spacing.M, 0, 0);
            var filterCard = FormLayout.Card("filterCard", "Rapor seçimi", FormLayout.Stack("filterStack", filters, lblAralikIpucu), fill: false);

            lblOzet.Name = nameof(lblOzet);
            lblOzet.Margin = Padding.Empty;
            var summary = FormLayout.Bar("summaryBar", (lblOzet, true));
            summary.Margin = new Padding(0, Spacing.M, 0, 0);
            var tableCard = FormLayout.Card("tableCard", "Rapor", FormLayout.Page((dataGridView1, true), (summary, false)));
            _tableTitle = (Label)tableCard.Controls["tableCardTitle"]!;

            Content.Controls.Add(FormLayout.Page((filterCard, false), (tableCard, true)));
            SetFooterNote("CSV dosyası ekranda görünen tabloyu Excel'in açabileceği biçimde yazar.");
            SetFooterButtons(CreateCloseButton());
            FinishLayout();
        }
    }
}

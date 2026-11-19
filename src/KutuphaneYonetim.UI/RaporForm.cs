using System.Globalization;
using KutuphaneYonetim.Business.Reports;
using KutuphaneYonetim.UI.Infrastructure;

namespace KutuphaneYonetim.UI
{
    /// <summary>
    /// Overdue loans, most borrowed books (date range) and unpaid fines. The table on screen is exported
    /// to CSV exactly as shown.
    /// </summary>
    public partial class RaporForm : Form
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

        private readonly IReportService _reportService;
        private readonly ICsvExporter _csvExporter;
        private readonly RequestSequence _reportRequests = new();
        private readonly ExclusiveOperation _export = new();
        private ReportTable? _shownTable;
        private ReportKind _shownKind;

        public RaporForm(IReportService reportService, ICsvExporter csvExporter)
        {
            _reportService = reportService;
            _csvExporter = csvExporter;
            InitializeComponent();
        }

        /// <summary>The table on screen (what CSV export writes), from the most recent report request.</summary>
        internal ReportTable? ShownTable => _shownTable;

        /// <summary>The kind of <see cref="ShownTable"/>.</summary>
        internal ReportKind ShownKind => _shownKind;

        private ReportKind SelectedKind => cmbRapor.SelectedOption(ReportKind.OverdueLoans);

        private async void RaporForm_Load(object sender, EventArgs e)
        {
            InitializeFilters();
            await this.RunAsync(ShowReportAsync);
        }

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

        private async void cmbRapor_SelectionChangeCommitted(object sender, EventArgs e)
        {
            UpdateFilterControls();
            await this.RunAsync(ShowReportAsync);
        }

        private async void btnGoster_Click(object sender, EventArgs e) => await this.RunAsync(ShowReportAsync);

        private void UpdateFilterControls()
        {
            var range = SelectedKind == ReportKind.MostBorrowed;
            dtpBaslangic.Enabled = range;
            dtpBitis.Enabled = range;
            nudAdet.Enabled = range;
        }

        /// <summary>
        /// Loads the selected report. When reports are requested faster than they load, only the most recent
        /// request is shown, so the table and its kind (used for the CSV file name) always belong together.
        /// </summary>
        internal async Task ShowReportAsync()
        {
            var request = _reportRequests.Next();
            var kind = SelectedKind;
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

            ShowTable(table);
            _shownTable = table;
            _shownKind = kind;
            lblOzet.Text = string.Format(CultureInfo.CurrentCulture, "{0} kayıt", table.Rows.Count);
        }

        private void ShowTable(ReportTable table)
        {
            dataGridView1.DataSource = null;
            dataGridView1.Rows.Clear();
            dataGridView1.Columns.Clear();

            foreach (var header in table.Headers)
            {
                var column = new DataGridViewTextBoxColumn { HeaderText = header, SortMode = DataGridViewColumnSortMode.NotSortable };
                dataGridView1.Columns.Add(column);
            }

            foreach (var row in table.Rows)
            {
                dataGridView1.Rows.Add(row.ToArray<object>());
            }

            dataGridView1.AutoResizeColumns(DataGridViewAutoSizeColumnsMode.AllCells);
        }

        private async void btnCsv_Click(object sender, EventArgs e) => await this.RunExclusiveAsync(_export, ExportCsvAsync, btnCsv);

        private async Task ExportCsvAsync()
        {
            if (_shownTable is not { } table)
            {
                MessageBox.Show("Önce bir rapor görüntüleyin.", "Uyarı", MessageBoxButtons.OK, MessageBoxIcon.Warning);
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
            MessageBox.Show(
                string.Format(CultureInfo.CurrentCulture, "{0} satır CSV dosyasına aktarıldı.", table.Rows.Count),
                "Bilgi",
                MessageBoxButtons.OK,
                MessageBoxIcon.Information);
        }
    }
}

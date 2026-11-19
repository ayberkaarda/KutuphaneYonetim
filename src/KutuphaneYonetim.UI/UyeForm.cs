using System.ComponentModel;
using System.Globalization;
using KutuphaneYonetim.Business.Services;
using KutuphaneYonetim.Domain;
using KutuphaneYonetim.Domain.Entities;
using KutuphaneYonetim.UI.Infrastructure;

namespace KutuphaneYonetim.UI
{
    /// <summary>Searches, lists, adds and deletes members, shows their unpaid fines and takes fine payments.</summary>
    public partial class UyeForm : Form
    {
        private static readonly Dictionary<string, string> ColumnHeaders = new()
        {
            [nameof(MemberSummary.Id)] = "Üye No",
            [nameof(MemberSummary.FirstName)] = "Ad",
            [nameof(MemberSummary.LastName)] = "Soyad",
            [nameof(MemberSummary.ActiveLoanCount)] = "Açık Ödünç",
            [nameof(MemberSummary.UnpaidFine)] = "Ödenmemiş Ceza (TL)",
        };

        private static readonly ListOption<MemberFilter>[] FilterOptions =
        [
            new("Tüm üyeler", MemberFilter.All),
            new("Ödenmemiş cezası olanlar", MemberFilter.WithUnpaidFine),
            new("Açık ödüncü olanlar", MemberFilter.WithActiveLoan),
        ];

        private readonly IMemberService _memberService;
        private readonly Pager _pager;
        private readonly RequestSequence _listRequests = new();
        private readonly ExclusiveOperation _write = new();
        private IReadOnlyList<MemberSummary> _shownMembers = [];
        private MemberSummary? _selectedMember;

        public UyeForm(IMemberService memberService)
        {
            _memberService = memberService;
            InitializeComponent();
            _pager = new Pager(btnOnceki, btnSonraki, lblSayfa);
        }

        [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        internal UserPrompts Prompts { get; set; } = UserPrompts.Default;

        private Control[] WriteButtons => [btnEkle, btnSil, btnCezaOde];

        /// <summary>
        /// The member payment and deletion act on: the selected row, as long as it is still part of the list
        /// on screen. Searching, filtering and paging clear the selection.
        /// </summary>
        private MemberSummary? Target =>
            _selectedMember is { } member && _shownMembers.Contains(member) ? member : null;

        /// <summary>
        /// Loads the current page; only the most recent of overlapping loads is shown. The selection is
        /// cleared as soon as a new list is requested, so it never refers to rows of an older list.
        /// </summary>
        internal async Task ListMembersAsync()
        {
            var request = _listRequests.Next();
            ClearSelection();
            var query = new MemberQuery(txtAra.Text, cmbFiltre.SelectedOption(MemberFilter.All), _pager.Page);
            var result = await _memberService.SearchAsync(query);
            if (!_listRequests.IsLatest(request))
            {
                return;
            }

            _shownMembers = result.Items;
            dataGridView1.DataSource = result.Items;
            dataGridView1.ConfigureColumns(ColumnHeaders);
            dataGridView1.FormatMoneyColumns(nameof(MemberSummary.UnpaidFine));
            dataGridView1.ClearSelection();
            _pager.Show(result);
        }

        private void ClearSelection()
        {
            if (_selectedMember is { } member
                && txtAd.Text == member.FirstName
                && txtSoyad.Text == member.LastName)
            {
                // The name boxes only showed the selected member; leave text the user typed alone.
                txtAd.Clear();
                txtSoyad.Clear();
            }

            _selectedMember = null;
            dataGridView1.ClearSelection();
        }

        /// <summary>Lists the first page for the current search text and filter.</summary>
        internal async Task SearchFromFirstPageAsync()
        {
            _pager.Reset();
            await this.RunAsync(ListMembersAsync);
        }

        internal async Task MovePreviousPageAsync()
        {
            _pager.MovePrevious();
            await this.RunAsync(ListMembersAsync);
        }

        internal async Task MoveNextPageAsync()
        {
            _pager.MoveNext();
            await this.RunAsync(ListMembersAsync);
        }

        private async void UyeForm_Load(object sender, EventArgs e)
        {
            cmbFiltre.BindOptions(FilterOptions);
            await this.RunAsync(ListMembersAsync);
        }

        private async void btnAra_Click(object sender, EventArgs e) => await SearchFromFirstPageAsync();

        private async void cmbFiltre_SelectionChangeCommitted(object sender, EventArgs e) => await SearchFromFirstPageAsync();

        private async void txtAra_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.KeyCode == Keys.Enter)
            {
                e.SuppressKeyPress = true;
                await SearchFromFirstPageAsync();
            }
        }

        private async void btnOnceki_Click(object sender, EventArgs e) => await MovePreviousPageAsync();

        private async void btnSonraki_Click(object sender, EventArgs e) => await MoveNextPageAsync();

        private async void btnEkle_Click(object sender, EventArgs e) => await AddMemberAsync();

        /// <summary>Adds the entered member; a second click while the first add is pending is ignored.</summary>
        internal Task AddMemberAsync() => this.RunExclusiveAsync(
            _write,
            async () =>
            {
                if (string.IsNullOrWhiteSpace(txtAd.Text) || string.IsNullOrWhiteSpace(txtSoyad.Text))
                {
                    Prompts.Warn("Lütfen tüm alanları doldurun.");
                    return;
                }

                await _memberService.AddAsync(txtAd.Text, txtSoyad.Text);
                Prompts.Inform("Üye başarıyla eklendi.");
                await ListMembersAsync();

                txtAd.Clear();
                txtSoyad.Clear();
                txtAd.Focus();
            },
            WriteButtons);

        private void dataGridView1_CellClick(object sender, DataGridViewCellEventArgs e)
        {
            if (e.RowIndex >= 0 && dataGridView1.Rows[e.RowIndex].DataBoundItem is MemberSummary member)
            {
                SelectMember(member);
            }
        }

        /// <summary>Makes <paramref name="member"/> (a row of the list) the target of payment and deletion.</summary>
        internal void SelectMember(MemberSummary member)
        {
            _selectedMember = member;
            txtAd.Text = member.FirstName;
            txtSoyad.Text = member.LastName;
        }

        private async void btnSil_Click(object sender, EventArgs e) => await DeleteMemberAsync();

        /// <summary>Deletes the selected member after confirmation; ignored while another write is pending.</summary>
        internal Task DeleteMemberAsync() => this.RunExclusiveAsync(
            _write,
            async () =>
            {
                if (Target is not { } member)
                {
                    Prompts.Warn("Lütfen silinecek bir üye seçin.");
                    return;
                }

                if (!Prompts.Confirm(string.Format(
                    CultureInfo.CurrentCulture, "{0} (Üye No {1}) silinsin mi?", member.FullName, member.Id)))
                {
                    return;
                }

                await _memberService.DeleteAsync(member.Id);
                Prompts.Inform("Üye silindi.");
                await ListMembersAsync();

                txtAd.Clear();
                txtSoyad.Clear();
            },
            WriteButtons);

        private async void btnCezaOde_Click(object sender, EventArgs e) => await PayFinesAsync();

        /// <summary>Takes the fine payment of the selected member; ignored while another write is pending.</summary>
        internal Task PayFinesAsync() => this.RunExclusiveAsync(
            _write,
            async () =>
            {
                if (Target is not { } member)
                {
                    Prompts.Warn("Lütfen cezası ödenecek üyeyi seçin.");
                    return;
                }

                if (!Prompts.Confirm(string.Format(
                    CultureInfo.CurrentCulture,
                    "{0} (Üye No {1}) adlı üyenin tüm ödenmemiş cezaları ödendi olarak işaretlensin mi?",
                    member.FullName,
                    member.Id)))
                {
                    return;
                }

                var paid = await _memberService.PayFinesAsync(member.Id);
                Prompts.Inform(string.Format(CultureInfo.CurrentCulture, "{0:N2} TL ceza ödendi olarak kaydedildi.", paid));
                await ListMembersAsync();
            },
            WriteButtons);
    }
}

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
        private int? _selectedMemberId;

        public UyeForm(IMemberService memberService)
        {
            _memberService = memberService;
            InitializeComponent();
            _pager = new Pager(btnOnceki, btnSonraki, lblSayfa);
        }

        [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        internal UserPrompts Prompts { get; set; } = UserPrompts.Default;

        private Control[] WriteButtons => [btnEkle, btnSil, btnCezaOde];

        /// <summary>Loads the current page; only the most recent of overlapping loads is shown.</summary>
        internal async Task ListMembersAsync()
        {
            var request = _listRequests.Next();
            var query = new MemberQuery(txtAra.Text, cmbFiltre.SelectedOption(MemberFilter.All), _pager.Page);
            var result = await _memberService.SearchAsync(query);
            if (!_listRequests.IsLatest(request))
            {
                return;
            }

            dataGridView1.DataSource = result.Items;
            dataGridView1.ConfigureColumns(ColumnHeaders);
            dataGridView1.FormatMoneyColumns(nameof(MemberSummary.UnpaidFine));
            _pager.Show(result);
        }

        private async Task SearchFromFirstPageAsync()
        {
            _pager.Reset();
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

        private async void btnOnceki_Click(object sender, EventArgs e)
        {
            _pager.MovePrevious();
            await this.RunAsync(ListMembersAsync);
        }

        private async void btnSonraki_Click(object sender, EventArgs e)
        {
            _pager.MoveNext();
            await this.RunAsync(ListMembersAsync);
        }

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
            _selectedMemberId = member.Id;
            txtAd.Text = member.FirstName;
            txtSoyad.Text = member.LastName;
        }

        private async void btnSil_Click(object sender, EventArgs e) => await DeleteMemberAsync();

        /// <summary>Deletes the selected member after confirmation; ignored while another write is pending.</summary>
        internal Task DeleteMemberAsync() => this.RunExclusiveAsync(
            _write,
            async () =>
            {
                if (_selectedMemberId is not int memberId)
                {
                    Prompts.Warn("Lütfen silinecek bir üye seçin.");
                    return;
                }

                if (!Prompts.Confirm("Bu üyeyi silmek istediğinize emin misiniz?"))
                {
                    return;
                }

                await _memberService.DeleteAsync(memberId);
                Prompts.Inform("Üye silindi.");
                _selectedMemberId = null;
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
                if (_selectedMemberId is not int memberId)
                {
                    Prompts.Warn("Lütfen cezası ödenecek üyeyi seçin.");
                    return;
                }

                if (!Prompts.Confirm("Seçili üyenin tüm ödenmemiş cezaları ödendi olarak işaretlensin mi?"))
                {
                    return;
                }

                var paid = await _memberService.PayFinesAsync(memberId);
                Prompts.Inform(string.Format(CultureInfo.CurrentCulture, "{0:N2} TL ceza ödendi olarak kaydedildi.", paid));
                await ListMembersAsync();
            },
            WriteButtons);
    }
}

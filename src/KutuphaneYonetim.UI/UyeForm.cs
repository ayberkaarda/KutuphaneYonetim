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
        private int? _selectedMemberId;

        public UyeForm(IMemberService memberService)
        {
            _memberService = memberService;
            InitializeComponent();
            _pager = new Pager(btnOnceki, btnSonraki, lblSayfa);
        }

        private async Task ListMembersAsync()
        {
            var query = new MemberQuery(txtAra.Text, cmbFiltre.SelectedOption(MemberFilter.All), _pager.Page);
            var result = await _memberService.SearchAsync(query);

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

        private async void btnEkle_Click(object sender, EventArgs e)
        {
            if (string.IsNullOrWhiteSpace(txtAd.Text) || string.IsNullOrWhiteSpace(txtSoyad.Text))
            {
                MessageBox.Show("Lütfen tüm alanları doldurun.", "Uyarı", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            await this.RunAsync(async () =>
            {
                await _memberService.AddAsync(txtAd.Text, txtSoyad.Text);
                MessageBox.Show("Üye başarıyla eklendi.", "Bilgi", MessageBoxButtons.OK, MessageBoxIcon.Information);
                await ListMembersAsync();

                txtAd.Clear();
                txtSoyad.Clear();
                txtAd.Focus();
            });
        }

        private void dataGridView1_CellClick(object sender, DataGridViewCellEventArgs e)
        {
            if (e.RowIndex >= 0 && dataGridView1.Rows[e.RowIndex].DataBoundItem is MemberSummary member)
            {
                _selectedMemberId = member.Id;
                txtAd.Text = member.FirstName;
                txtSoyad.Text = member.LastName;
            }
        }

        private async void btnSil_Click(object sender, EventArgs e)
        {
            if (_selectedMemberId is not int memberId)
            {
                MessageBox.Show("Lütfen silinecek bir üye seçin.", "Uyarı", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            DialogResult result = MessageBox.Show("Bu üyeyi silmek istediğinize emin misiniz?", "Onay", MessageBoxButtons.YesNo, MessageBoxIcon.Question);
            if (result != DialogResult.Yes)
            {
                return;
            }

            await this.RunAsync(async () =>
            {
                await _memberService.DeleteAsync(memberId);
                MessageBox.Show("Üye silindi.", "Bilgi", MessageBoxButtons.OK, MessageBoxIcon.Information);
                _selectedMemberId = null;
                await ListMembersAsync();

                txtAd.Clear();
                txtSoyad.Clear();
            });
        }

        private async void btnCezaOde_Click(object sender, EventArgs e)
        {
            if (_selectedMemberId is not int memberId)
            {
                MessageBox.Show("Lütfen cezası ödenecek üyeyi seçin.", "Uyarı", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            DialogResult result = MessageBox.Show("Seçili üyenin tüm ödenmemiş cezaları ödendi olarak işaretlensin mi?", "Onay", MessageBoxButtons.YesNo, MessageBoxIcon.Question);
            if (result != DialogResult.Yes)
            {
                return;
            }

            await this.RunAsync(async () =>
            {
                var paid = await _memberService.PayFinesAsync(memberId);
                MessageBox.Show(
                    string.Format(CultureInfo.CurrentCulture, "{0:N2} TL ceza ödendi olarak kaydedildi.", paid),
                    "Bilgi",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Information);
                await ListMembersAsync();
            });
        }
    }
}

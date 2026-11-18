using KutuphaneYonetim.Business.Services;
using KutuphaneYonetim.Domain.Entities;
using KutuphaneYonetim.UI.Infrastructure;

namespace KutuphaneYonetim.UI
{
    /// <summary>Lists, adds and deletes members.</summary>
    public partial class UyeForm : Form
    {
        private static readonly Dictionary<string, string> ColumnHeaders = new()
        {
            [nameof(Member.Id)] = "Üye No",
            [nameof(Member.FirstName)] = "Ad",
            [nameof(Member.LastName)] = "Soyad",
        };

        private readonly IMemberService _memberService;
        private int? _selectedMemberId;

        public UyeForm(IMemberService memberService)
        {
            _memberService = memberService;
            InitializeComponent();
        }

        private async Task ListMembersAsync()
        {
            dataGridView1.DataSource = await _memberService.GetAllAsync();
            dataGridView1.ConfigureColumns(ColumnHeaders);
        }

        private async void UyeForm_Load(object sender, EventArgs e)
        {
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
            if (e.RowIndex >= 0 && dataGridView1.Rows[e.RowIndex].DataBoundItem is Member member)
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
    }
}

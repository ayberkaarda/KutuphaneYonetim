using System.ComponentModel;
using System.Globalization;
using KutuphaneYonetim.Business.Services;
using KutuphaneYonetim.Domain;
using KutuphaneYonetim.Domain.Entities;
using KutuphaneYonetim.UI.Infrastructure;
using KutuphaneYonetim.UI.Theming;

namespace KutuphaneYonetim.UI
{
    /// <summary>Searches, lists, adds and deletes members, shows their unpaid fines and takes fine payments.</summary>
    public sealed class UyeForm : AppDialog
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

        private readonly TextBox txtAd = new() { Name = nameof(txtAd), MaxLength = 100 };
        private readonly TextBox txtSoyad = new() { Name = nameof(txtSoyad), MaxLength = 100 };
        private readonly TextBox txtAra = new() { Name = nameof(txtAra), PlaceholderText = "Ad ya da soyad", AccessibleName = "Ara" };
        private readonly ThemedComboBox cmbFiltre = FormLayout.Picker(nameof(cmbFiltre), 220);
        private readonly ThemedGrid dataGridView1 = new() { Name = nameof(dataGridView1), AccessibleName = "Üye listesi" };
        private readonly ThemedButton btnEkle = FormLayout.Button(nameof(btnEkle), "Üye Ekle", ButtonKind.Primary);
        private readonly ThemedButton btnAra = FormLayout.Button(nameof(btnAra), "Ara");
        private readonly ThemedButton btnSil = FormLayout.Button(nameof(btnSil), "Seçili Üyeyi Sil", ButtonKind.Danger);
        private readonly ThemedButton btnCezaOde = FormLayout.Button(nameof(btnCezaOde), "Cezayı Öde");
        private readonly ThemedButton btnOnceki = FormLayout.Button(nameof(btnOnceki), "‹ Önceki", ButtonKind.Subtle);
        private readonly ThemedButton btnSonraki = FormLayout.Button(nameof(btnSonraki), "Sonraki ›", ButtonKind.Subtle);
        private readonly Label lblSayfa = FormLayout.Hint(string.Empty);
        private readonly Label lblSecimAd = FormLayout.Text("lblSecimAd");
        private readonly Label lblSecimBilgi = FormLayout.Hint(string.Empty);
        private readonly Label lblCezaBaslik = FormLayout.Hint("Ödenmemiş ceza");
        private readonly Label lblCeza = FormLayout.Value("lblCeza");

        public UyeForm(IMemberService memberService)
            : base("Üyeler", "Üye ekleyin ve arayın; açık ödünçleri ve ödenmemiş cezaları tek listede görün.", Glyphs.People, new Size(1040, 660), new Size(840, 580))
        {
            _memberService = memberService;
            IsListFocused = () => dataGridView1.Focused;
            BuildLayout();
            _pager = new Pager(btnOnceki, btnSonraki, lblSayfa);
            ShowSelection();

            Load += async (_, _) => await this.RunAsync(LoadAsync);
            btnAra.Click += async (_, _) => await SearchFromFirstPageAsync();
            cmbFiltre.SelectionChangeCommitted += async (_, _) => await SearchFromFirstPageAsync();
            txtAra.KeyDown += async (_, e) =>
            {
                if (e.KeyCode == Keys.Enter)
                {
                    e.SuppressKeyPress = true;
                    await SearchFromFirstPageAsync();
                }
            };
            btnOnceki.Click += async (_, _) => await MovePreviousPageAsync();
            btnSonraki.Click += async (_, _) => await MoveNextPageAsync();
            btnEkle.Click += async (_, _) => await AddMemberAsync();
            btnSil.Click += async (_, _) => await DeleteMemberAsync();
            btnCezaOde.Click += async (_, _) => await PayFinesAsync();
            dataGridView1.CellClick += (_, e) => SelectRow(e.RowIndex);
            dataGridView1.CurrentCellChanged += (_, _) =>
            {
                // Arrow keys choose a row too, but only while the user is in the list.
                if (!_binding && IsListFocused() && dataGridView1.CurrentRow is { } row)
                {
                    SelectRow(row.Index);
                }
            };
            dataGridView1.CellFormatting += HighlightFines;
        }

        /// <summary>Whether the user is working in the list; tests replace it because a window that is never shown has no focus.</summary>
        [Browsable(false)]
        [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        internal Func<bool> IsListFocused { get; set; }

        private bool _binding;

        private Control[] WriteButtons => [btnEkle, btnSil, btnCezaOde];

        /// <summary>
        /// The member payment and deletion act on: the selected row, as long as it is still part of the list
        /// on screen. Searching, filtering and paging clear the selection.
        /// </summary>
        private MemberSummary? Target =>
            _selectedMember is { } member && _shownMembers.Contains(member) ? member : null;

        /// <summary>Fills the filter and lists the first page; runs when the window opens.</summary>
        internal async Task LoadAsync()
        {
            cmbFiltre.BindOptions(FilterOptions);
            await ListMembersAsync();
        }

        /// <summary>
        /// Loads the current page; only the most recent of overlapping loads is shown. The selection is
        /// cleared as soon as a new list is requested, so it never refers to rows of an older list.
        /// </summary>
        internal async Task ListMembersAsync()
        {
            var request = _listRequests.Next();
            ClearSelection();
            var query = new MemberQuery(txtAra.Text, cmbFiltre.SelectedOption(MemberFilter.All), _pager.Page);
            dataGridView1.IsLoading = true;
            try
            {
                var result = await _memberService.SearchAsync(query);
                if (!_listRequests.IsLatest(request))
                {
                    return;
                }

                _shownMembers = result.Items;
                dataGridView1.EmptyText = string.IsNullOrWhiteSpace(query.Text) && query.Filter == MemberFilter.All
                    ? "Henüz üye yok. Soldaki formdan ilk üyeyi ekleyin."
                    : "Aramanıza uyan üye bulunamadı.";
                BindList(result.Items);
                _pager.Show(result);
            }
            finally
            {
                if (_listRequests.IsLatest(request))
                {
                    dataGridView1.IsLoading = false;
                }
            }
        }

        /// <summary>
        /// Shows the rows without choosing one: binding moves the current cell to the first row, which is
        /// not the user's choice, so selection by that event is suppressed and the model, the card and the
        /// row highlight are cleared together afterwards.
        /// </summary>
        private void BindList(IReadOnlyList<MemberSummary> items)
        {
            _binding = true;
            try
            {
                dataGridView1.DataSource = items;
                dataGridView1.ConfigureColumns(ColumnHeaders);
                dataGridView1.FormatMoneyColumns(nameof(MemberSummary.UnpaidFine));
                dataGridView1.FitColumns(120, nameof(MemberSummary.FirstName), nameof(MemberSummary.LastName));
            }
            finally
            {
                _binding = false;
            }

            ClearSelection();
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
            dataGridView1.SelectedItem = null;
            ShowSelection();
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

        /// <summary>Adds the entered member; a second click while the first add is pending is ignored.</summary>
        internal Task AddMemberAsync() => this.RunExclusiveAsync(
            _write,
            async () =>
            {
                if (string.IsNullOrWhiteSpace(txtAd.Text) || string.IsNullOrWhiteSpace(txtSoyad.Text))
                {
                    Prompts.Warn("Lütfen tüm alanları doldurun.");
                    (string.IsNullOrWhiteSpace(txtAd.Text) ? txtAd : txtSoyad).Focus();
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

        /// <summary>Makes <paramref name="member"/> (a row of the list) the target of payment and deletion.</summary>
        internal void SelectMember(MemberSummary member)
        {
            _selectedMember = member;
            dataGridView1.SelectedItem = member;
            txtAd.Text = member.FirstName;
            txtSoyad.Text = member.LastName;
            ShowSelection();
        }

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

        private void SelectRow(int rowIndex)
        {
            if (rowIndex >= 0 && dataGridView1.Rows[rowIndex].DataBoundItem is MemberSummary member)
            {
                SelectMember(member);
            }
        }

        /// <summary>The selected member's card: name, open loans and the unpaid fine with its severity.</summary>
        private void ShowSelection()
        {
            var p = Theme.Current;
            if (_selectedMember is not { } member)
            {
                lblSecimAd.Text = "Üye seçilmedi";
                lblSecimAd.ForeColor = p.TextMuted;
                lblSecimBilgi.Text = "Ceza ödemek ya da silmek için listeden bir üye seçin.";
                lblCezaBaslik.Visible = false;
                lblCeza.Visible = false;
                return;
            }

            lblSecimAd.Text = member.FullName;
            lblSecimAd.ForeColor = p.Text;
            lblSecimBilgi.Text = string.Format(CultureInfo.CurrentCulture, "Üye No {0} · {1} açık ödünç", member.Id, member.ActiveLoanCount);
            lblCezaBaslik.Visible = true;
            lblCeza.Visible = true;
            lblCeza.Text = member.UnpaidFine > 0
                ? string.Format(CultureInfo.CurrentCulture, "{0:N2} TL", member.UnpaidFine)
                : "Ödenmemiş cezası yok";
            lblCeza.ForeColor = member.UnpaidFine > 0 ? p.Danger : p.Success;
        }

        /// <summary>Unpaid fines stand out in the danger color and a stronger weight.</summary>
        private void HighlightFines(object? sender, DataGridViewCellFormattingEventArgs e)
        {
            if (e.RowIndex < 0 || e.CellStyle is null
                || dataGridView1.Columns[e.ColumnIndex].DataPropertyName != nameof(MemberSummary.UnpaidFine)
                || e.Value is not decimal fine || fine <= 0)
            {
                return;
            }

            e.CellStyle.ForeColor = Theme.Current.Danger;
            e.CellStyle.SelectionForeColor = Theme.Current.Danger;
            e.CellStyle.Font = Theme.Fonts.BodyStrong;
        }

        private void BuildLayout()
        {
            Name = nameof(UyeForm);

            var editor = FormLayout.Fields(
                "memberEditor",
                [FormLayout.Field("&Ad", FormLayout.Input(txtAd))],
                [FormLayout.Field("&Soyad", FormLayout.Input(txtSoyad))]);
            btnEkle.Margin = new Padding(0, Spacing.XS, 0, 0);
            var editorCard = FormLayout.Card("editorCard", "Yeni üye", FormLayout.Stack("editorStack", editor, FormLayout.Row(btnEkle)), fill: false);

            lblSecimAd.Font = Theme.Fonts.Subtitle;
            lblSecimBilgi.Margin = new Padding(0, Spacing.XS / 2, 0, Spacing.M);
            lblCezaBaslik.Name = nameof(lblCezaBaslik);
            lblCezaBaslik.Margin = Padding.Empty;
            lblCeza.Margin = Padding.Empty;
            btnCezaOde.Margin = new Padding(Spacing.S, 0, 0, 0);
            var fine = FormLayout.Stack("fineStack", lblCezaBaslik, lblCeza);
            var fineBar = FormLayout.Bar("fineBar", (fine, true), (btnCezaOde, false));
            fine.Anchor = AnchorStyles.Left | AnchorStyles.Right;
            btnCezaOde.Anchor = AnchorStyles.Right;
            var selection = FormLayout.Stack("selectionStack", lblSecimAd, lblSecimBilgi, fineBar);
            var selectionCard = FormLayout.Card("selectionCard", "Seçili üye", selection, fill: false);
            var side = FormLayout.Page((editorCard, false), (selectionCard, false));
            side.Name = "side";

            var search = FormLayout.Input(txtAra);
            search.Margin = new Padding(0, 0, Spacing.S, 0);
            cmbFiltre.AccessibleName = "Filtre";
            cmbFiltre.Margin = new Padding(0, 0, Spacing.S, 0);
            btnAra.Margin = Padding.Empty;
            var searchBar = FormLayout.Bar("searchBar", (search, true), (cmbFiltre, false), (btnAra, false));
            searchBar.Margin = new Padding(0, 0, 0, Spacing.M);

            btnSil.Margin = Padding.Empty;
            btnOnceki.Margin = Padding.Empty;
            btnSonraki.Margin = Padding.Empty;
            lblSayfa.Name = nameof(lblSayfa);
            lblSayfa.Margin = new Padding(Spacing.S, 0, Spacing.S, 0);
            var actions = FormLayout.Bar("actionBar", (btnSil, false), (FormLayout.Spacer(), true), (btnOnceki, false), (lblSayfa, false), (btnSonraki, false));
            actions.Margin = new Padding(0, Spacing.M, 0, 0);

            var list = FormLayout.Page((searchBar, false), (dataGridView1, true), (actions, false));
            var listCard = FormLayout.Card("listCard", "Üye listesi", list);

            Content.Controls.Add(FormLayout.Split(side, listCard));
            SetFooterNote("Açık ödüncü ya da ödünç geçmişi olan üyeler silinemez.");
            SetFooterButtons(CreateCloseButton());
            FinishLayout();
        }
    }
}

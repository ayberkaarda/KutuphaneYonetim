using System.Globalization;
using KutuphaneYonetim.UI.Infrastructure;
using KutuphaneYonetim.UI.Theming;

namespace KutuphaneYonetim.UI
{
    /// <summary>
    /// Main menu with the application header and grouped tiles that open the book, member, loan,
    /// reservation and report screens. Each screen opens in its own window.
    /// </summary>
    public sealed class Form1 : Form
    {
        private readonly IFormFactory _formFactory;

        public Form1(IFormFactory formFactory)
        {
            _formFactory = formFactory;
            BuildLayout();
        }

        /// <summary>Opens a screen in its own window, next to the ones already open.</summary>
        private void Open<T>()
            where T : Form => _formFactory.Create<T>().Show();

        private void BuildLayout()
        {
            SuspendLayout();
            AutoScaleDimensions = new SizeF(96F, 96F);
            AutoScaleMode = AutoScaleMode.Dpi;
            Name = nameof(Form1);
            Text = AppDialog.AppName;
            Font = Theme.Fonts.Body;
            Icon = Theme.AppIcon;
            StartPosition = FormStartPosition.CenterScreen;
            ClientSize = new Size(800, 640);
            MinimumSize = SizeFromClientSize(new Size(640, 560));

            var tiles = new TableLayoutPanel
            {
                Name = "menu",
                Dock = DockStyle.Top,
                AutoSize = true,
                AutoSizeMode = AutoSizeMode.GrowAndShrink,
                ColumnCount = 2,
                Margin = Padding.Empty,
            };
            tiles.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
            tiles.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));

            AddSection(tiles, "Koleksiyon ve üyeler",
                Tile("btnKitap", "Kitaplar", "Kitap ekleyin, arayın; rafta, ödünçte ya da ayrılmış olanları süzün.", Glyphs.Library, Open<KitapForm>),
                Tile("btnUye", "Üyeler", "Üyeleri yönetin, açık ödünçleri ve ödenmemiş cezaları görün.", Glyphs.People, Open<UyeForm>));
            AddSection(tiles, "Ödünç işlemleri",
                Tile("btnOdunc", "Ödünç ve İade", "Teslim tarihiyle ödünç verin; iadede gecikme cezası kaydedilir.", Glyphs.Lending, Open<OduncForm>),
                Tile("btnRezervasyon", "Rezervasyonlar", "Ödünçteki kitaplar için sıra açın, ayrılan kitapları izleyin.", Glyphs.Reservation, Open<RezervasyonForm>));
            AddSection(tiles, "Raporlar",
                Tile("btnRapor", "Raporlar", "Gecikenler, en çok ödünç alınanlar ve cezalar; CSV olarak alın.", Glyphs.Report, Open<RaporForm>));

            var body = new Panel
            {
                Name = "body",
                Dock = DockStyle.Fill,
                AutoScroll = true,
                Padding = new Padding(Spacing.XL, Spacing.XS, Spacing.XL - Spacing.M, Spacing.S),
            };
            body.Controls.Add(tiles);

            var exit = FormLayout.Button("btnCikis", "Çıkış", ButtonKind.Secondary);
            exit.Click += (_, _) => Close();
            exit.Anchor = AnchorStyles.Right;
            var note = FormLayout.Hint("Her ekran kendi penceresinde açılır ve Esc ile kapanır.");
            note.Name = "footerNote";
            note.Anchor = AnchorStyles.Left;
            note.Margin = new Padding(Spacing.XS, 0, Spacing.M, 0);
            var footer = new TableLayoutPanel
            {
                Name = "footer",
                Dock = DockStyle.Bottom,
                AutoSize = true,
                AutoSizeMode = AutoSizeMode.GrowAndShrink,
                ColumnCount = 2,
                Padding = new Padding(Spacing.XL - Spacing.XS, Spacing.M, Spacing.XL - Spacing.XS, Spacing.M),
                BackColor = Theme.Current.Surface,
            };
            footer.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            footer.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            footer.Paint += (_, e) => FormLayout.DrawDivider(footer, e.Graphics, top: true);
            footer.Controls.Add(note, 0, 0);
            footer.Controls.Add(exit, 1, 0);

            Controls.Add(body);
            Controls.Add(footer);
            Controls.Add(BuildHeader());

            ThemeStyler.Apply(this);
            ResumeLayout(false);
            PerformLayout();
        }

        /// <summary>The application header: logo, name in the title face, a short line and today's date.</summary>
        private static TableLayoutPanel BuildHeader()
        {
            var header = FormLayout.Header(
                AppDialog.AppName,
                "Kitapları, üyeleri ve ödünç hareketlerini tek yerden yönetin.",
                string.Empty,
                Theme.Logo);
            header.Padding = new Padding(Spacing.XL, Spacing.XL, Spacing.XL, Spacing.XL - Spacing.XS);
            header.Controls["headerTitle"]!.Font = Theme.Fonts.Display;
            var badge = header.Controls["headerBadge"]!;
            badge.Size = new Size(52, 52);
            badge.Margin = new Padding(0, 0, Spacing.L, 0);

            var today = FormLayout.Hint(DateTime.Today.ToString("d MMMM yyyy, dddd", CultureInfo.CurrentCulture));
            today.Name = "today";
            today.Anchor = AnchorStyles.Right | AnchorStyles.Bottom;
            today.Margin = new Padding(Spacing.L, 0, 0, Spacing.XS / 2);
            header.Controls.Add(today, 2, 0);
            header.SetRowSpan(today, 2);
            return header;
        }

        private static TileButton Tile(string name, string title, string description, string glyph, Action open)
        {
            var tile = new TileButton(name, title, description, glyph);
            tile.Click += (_, _) => open();
            return tile;
        }

        private static void AddSection(TableLayoutPanel grid, string title, params TileButton[] tiles)
        {
            var heading = FormLayout.SectionTitle(title);
            heading.Name = "section" + grid.RowStyles.Count.ToString(CultureInfo.InvariantCulture);
            heading.Font = Theme.Fonts.BodyStrong;
            heading.ForeColor = Theme.Current.TextSecondary;
            heading.Margin = new Padding(0, Spacing.L, 0, Spacing.S);
            grid.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            grid.Controls.Add(heading, 0, grid.RowStyles.Count - 1);
            grid.SetColumnSpan(heading, grid.ColumnCount);

            grid.RowStyles.Add(new RowStyle(SizeType.Absolute, Sizes.TileHeight + Spacing.M));
            for (var i = 0; i < tiles.Length; i++)
            {
                grid.Controls.Add(tiles[i], i, grid.RowStyles.Count - 1);
            }
        }
    }
}

using KutuphaneYonetim.Business;
using KutuphaneYonetim.Domain.Entities;
using KutuphaneYonetim.UI.Infrastructure;
using KutuphaneYonetim.UI.Tests.Support;
using KutuphaneYonetim.UI.Theming;
using static KutuphaneYonetim.UI.Tests.Support.StaThread;

namespace KutuphaneYonetim.UI.Tests;

/// <summary>
/// Every screen is built with test doubles and laid out without being shown: window handles are
/// created for real layout and tab order, but no window becomes visible and no message box opens.
/// </summary>
public sealed class ScreenTests
{
    private static readonly string[] LastStops = ["btnKapat", "btnCikis"];

    public static TheoryData<string> ScreenNames =>
    [
        nameof(Form1), nameof(KitapForm), nameof(UyeForm), nameof(OduncForm), nameof(RezervasyonForm), nameof(RaporForm),
    ];

    [Theory]
    [MemberData(nameof(ScreenNames))]
    public void Screen_HasTheApplicationIdentity(string name) => Run(() =>
    {
        using var form = Create(name);

        Assert.EndsWith(AppDialog.AppName, form.Text, StringComparison.Ordinal);
        Assert.NotNull(form.Icon);
        Assert.Equal(FormBorderStyle.Sizable, form.FormBorderStyle);
        Assert.True(form.MinimumSize.Width > 0 && form.MinimumSize.Height > 0, "A resizable screen needs a minimum size.");
        Assert.Equal(AutoScaleMode.Dpi, form.AutoScaleMode);
        Assert.Null(form.BackgroundImage);
        Assert.False(form.Visible);
    });

    [Theory]
    [MemberData(nameof(ScreenNames))]
    public void Screen_LaysOutInsideItsWindow(string name) => Run(() =>
    {
        using var form = Create(name);
        CreateHandles(form);
        form.PerformLayout();

        var overflow = Descendants(form)
            .Where(control => control.Parent is Form or Panel or TableLayoutPanel or FlowLayoutPanel)
            .Where(control => control.Parent is not ScrollableControl { AutoScroll: true }) // A scrolling area may be taller.
            .Where(control => control is not InfoBar && !IsInsideParent(control)) // The message strip is hidden until used.
            .Select(control => $"{control.Name} ({control.GetType().Name}) {control.Bounds} in {control.Parent!.Name} {control.Parent.ClientRectangle}")
            .ToList();

        Assert.Empty(overflow);
        Assert.False(form.Visible);
    });

    [Theory]
    [MemberData(nameof(ScreenNames))]
    public void Screen_StaysInsideItsWindowAtTheMinimumSize(string name) => Run(() =>
    {
        using var form = Create(name);
        form.Size = form.MinimumSize;
        CreateHandles(form);
        form.PerformLayout();

        var cards = Descendants(form).OfType<CardPanel>().ToList();
        var client = form.ClientRectangle;
        foreach (var card in cards)
        {
            var bounds = form.RectangleToClient(card.RectangleToScreen(card.ClientRectangle));
            Assert.True(client.Contains(bounds), $"{name}.{card.Name} {bounds} leaves the window {client} at the minimum size.");
        }
    });

    public static TheoryData<string, int, int, float> DialogsOnSmallScreens
    {
        get
        {
            var data = new TheoryData<string, int, int, float>();
            foreach (var (width, height) in new[] { (1024, 600), (1366, 768) })
            {
                foreach (var scale in new[] { 1F, 1.25F, 1.5F, 2F })
                {
                    foreach (var name in new[] { nameof(KitapForm), nameof(UyeForm), nameof(OduncForm), nameof(RezervasyonForm), nameof(RaporForm) })
                    {
                        data.Add(name, width, height, scale);
                    }
                }
            }

            return data;
        }
    }

    [Theory]
    [MemberData(nameof(DialogsOnSmallScreens))]
    public void LendingScreen_FitsWorkingAreaAndKeepsActionsReachable(string name, int screenWidth, int screenHeight, float fontScale) => Run(() =>
    {
        // Larger fonts stand in for a different font, DPI or text scale; the working area for a small screen.
        var previous = Theme.Current;
        var previousScale = Theme.RenderScale;
        var previousArea = AppDialog.WorkingAreaOf;
        Theme.Use(previous, fontScale);
        var area = new Rectangle(0, 0, screenWidth, screenHeight);
        AppDialog.WorkingAreaOf = _ => area;
        try
        {
            using var form = (AppDialog)Create(name);
            CreateHandles(form);

            Assert.True(form.Width <= area.Width && form.Height <= area.Height, $"{name} opens at {form.Size}, larger than the working area {area.Size}.");
            Assert.True(form.MinimumSize.Width <= area.Width && form.MinimumSize.Height <= area.Height, $"{name} requires {form.MinimumSize}, larger than the working area {area.Size}.");

            foreach (var size in new[] { form.Size, form.MinimumSize })
            {
                form.Size = size;
                form.ShowStatus(StatusKind.Warning, "Ölçüm sınırın dışında." + Environment.NewLine + "Değeri düzeltin.");
                form.PerformLayout();

                var body = Find<ScrollableControl>(form, "body");
                var content = Find<Panel>(form, "content");
                var unreachable = Descendants(form)
                    .Where(control => control is not (Label or Panel or TableLayoutPanel or FlowLayoutPanel or GroupBox or InfoBar or ScrollBar))
                    .Where(control => control.Parent is not NumericUpDown)
                    .Where(control => control is not TextBox { Parent: TextField }) // Judged through the frame.
                    .Select(control => (Control: control, Problem: Problem(form, body, content, control)))
                    .Where(item => item.Problem is not null)
                    .Select(item => $"{item.Control.Name} ({item.Control.GetType().Name}): {item.Problem}")
                    .ToList();

                Assert.True(unreachable.Count == 0, $"{name} at {form.Size}: " + string.Join(" || ", unreachable));
                form.ClearStatus();
            }
        }
        finally
        {
            AppDialog.WorkingAreaOf = previousArea;
            Theme.Use(previous, previousScale);
        }
    });

    /// <summary>
    /// Why a control cannot be used: it is cut off by a container that does not scroll, or it lies outside the area the
    /// scrolling body can reach. Containers of zero size cut off everything they hold.
    /// </summary>
    private static string? Problem(Form form, ScrollableControl body, Panel content, Control control)
    {
        if (control.Width <= 0 || control.Height <= 0)
        {
            return null; // Never shown.
        }

        var offset = control.Location;
        for (var ancestor = control.Parent; ancestor is not null && ancestor != form; ancestor = ancestor.Parent)
        {
            var client = ancestor == body ? body.DisplayRectangle.Size : ancestor.ClientSize; // The body scrolls.
            if (offset.X + control.Width > client.Width + 1 || offset.Y + control.Height > client.Height + 1 || offset.X < -1 || offset.Y < -1)
            {
                return $"cut off by {ancestor.Name} {ancestor.ClientSize}";
            }

            offset = new Point(offset.X + ancestor.Left, offset.Y + ancestor.Top);
        }

        if (control.Parent == form || !IsInside(body, control))
        {
            // Header and footer stay put: they must be fully inside the window.
            return form.ClientRectangle.Contains(form.RectangleToClient(control.RectangleToScreen(control.ClientRectangle))) ? null : "outside the window";
        }

        // Inside the body: reachable when the body scrolls over everything the content needs.
        var needed = content.Bounds;
        var scrollable = body.AutoScroll && body.DisplayRectangle.Height >= needed.Bottom && body.DisplayRectangle.Width >= needed.Right;
        return scrollable ? null : $"the body {body.ClientSize} does not scroll over {needed}";
    }

    private static bool IsInside(Control container, Control control)
    {
        for (var parent = control.Parent; parent is not null; parent = parent.Parent)
        {
            if (parent == container)
            {
                return true;
            }
        }

        return false;
    }

    [Theory]
    [MemberData(nameof(ScreenNames))]
    public void Screen_UsesThemedControlsAndAKeyboardWayOut(string name) => Run(() =>
    {
        using var form = Create(name);

        var buttons = Descendants(form).OfType<Button>().ToList();
        Assert.NotEmpty(buttons);
        Assert.All(buttons, button => Assert.True(button is ThemedButton or TileButton, $"{button.Name} is not themed."));
        Assert.All(buttons, button => Assert.False(string.IsNullOrWhiteSpace(button.Text), $"{button.Name} has no text."));
        Assert.All(Descendants(form).OfType<ComboBox>(), combo => Assert.IsType<ThemedComboBox>(combo));
        Assert.All(Descendants(form).OfType<DataGridView>(), grid => Assert.IsType<ThemedGrid>(grid));
        Assert.All(
            Descendants(form).OfType<TextBox>().Where(box => box.Parent is not NumericUpDown),
            box => Assert.IsType<TextField>(box.Parent));
        if (form is AppDialog)
        {
            Assert.NotNull(form.CancelButton); // Esc closes every screen.
        }
    });

    [Theory]
    [InlineData(nameof(Form1), "btnKitap")]
    [InlineData(nameof(KitapForm), "txtAd")]
    [InlineData(nameof(UyeForm), "txtAd")]
    [InlineData(nameof(OduncForm), "cmbKitaplar")]
    [InlineData(nameof(RezervasyonForm), "cmbKitaplar")]
    [InlineData(nameof(RaporForm), "cmbRapor")]
    public void TabOrder_StartsAtTheFirstInputAndEndsInTheFooter(string name, string first) => Run(() =>
    {
        using var form = Create(name);
        CreateHandles(form);

        var order = TabOrder(form);

        Assert.Equal(first, order[0].Name);
        Assert.Contains(order[^1].Name, LastStops);
        Assert.Equal(order.Count, order.Distinct().Count());
    });

    [Theory]
    [InlineData(nameof(KitapForm), new[] { "txtAd", "txtYazar", "txtIsbn", "btnEkle", "txtAra", "cmbDurum", "btnAra", "dataGridView1", "btnSil" })]
    [InlineData(nameof(UyeForm), new[] { "txtAd", "txtSoyad", "btnEkle", "btnCezaOde", "txtAra", "cmbFiltre", "btnAra", "dataGridView1", "btnSil" })]
    [InlineData(nameof(OduncForm), new[] { "cmbKitaplar", "cmbUyeler", "dtpTeslim", "btnOduncAl", "dataGridView1", "btnIadeEt" })]
    [InlineData(nameof(RaporForm), new[] { "cmbRapor", "dtpBaslangic", "dtpBitis", "nudAdet", "btnGoster", "btnCsv", "dataGridView1" })]
    public void TabOrder_FollowsTheReadingOrder(string name, string[] expected) => Run(() =>
    {
        using var form = Create(name);
        CreateHandles(form);

        var names = TabOrder(form).Select(control => control.Name).Where(expected.Contains).ToList();

        Assert.Equal(expected, names);
    });

    [Fact]
    public void FieldCaptions_NameTheirInputs() => Run(() =>
    {
        using var form = new KitapForm(new ControlledBookService());

        Assert.Equal("Kitap adı", Find<TextBox>(form, "txtAd").AccessibleName);
        Assert.Equal("Yazar", Find<TextBox>(form, "txtYazar").AccessibleName);
        Assert.Equal("Ara", Find<TextBox>(form, "txtAra").AccessibleName);
        Assert.False(Descendants(form).OfType<TextField>().Any(field => field.CanSelect), "A text field frame must hand the focus to its text box.");
    });

    [Fact]
    public void MissingInput_IsReportedInlineWithoutADialog() => Run(() =>
    {
        var books = new ControlledBookService();
        using var form = new KitapForm(books);
        var dialogs = NoDialogs.Requested.Count;

        var add = form.AddBookAsync();

        Assert.True(add.IsCompletedSuccessfully);
        Assert.Equal(0, books.AddCalls);
        Assert.True(form.StatusBar.IsShown);
        Assert.Equal(StatusKind.Warning, form.StatusBar.Kind);
        Assert.Equal("Lütfen kitap adı ve yazar alanlarını doldurunuz.", form.StatusBar.Text);
        Assert.Equal(form.StatusBar.Text, form.StatusBar.AccessibleName);
        Assert.Equal(dialogs, NoDialogs.Requested.Count);
    });

    [Fact]
    public void NextWrite_ClearsThePreviousMessage() => Run(() =>
    {
        var books = new ControlledBookService();
        using var form = new KitapForm(books);
        Assert.True(form.AddBookAsync().IsCompletedSuccessfully);
        Assert.True(form.StatusBar.IsShown);
        Find<TextBox>(form, "txtAd").Text = "Kitap";
        Find<TextBox>(form, "txtYazar").Text = "Yazar";

        _ = form.AddBookAsync(); // Never completes; the old warning must be gone meanwhile.

        Assert.Equal(1, books.AddCalls);
        Assert.False(form.StatusBar.IsShown);
    });

    [Fact]
    public void Errors_AreShownInlineInTheScreen() => Run(() =>
    {
        using var form = new KitapForm(new ControlledBookService());
        var dialogs = NoDialogs.Requested.Count;

        ErrorReporter.Show(new BusinessRuleException("Bu kitap ödünçte olduğu için silinemez."), form);
        Assert.Equal(StatusKind.Warning, form.StatusBar.Kind);
        Assert.Equal("Bu kitap ödünçte olduğu için silinemez.", form.StatusBar.Text);

        ErrorReporter.Show(new InvalidOperationException("raw technical text"), form);
        Assert.Equal(StatusKind.Error, form.StatusBar.Kind);
        Assert.StartsWith("Beklenmeyen bir hata oluştu.", form.StatusBar.Text, StringComparison.Ordinal);
        Assert.DoesNotContain("raw technical text", form.StatusBar.Text, StringComparison.Ordinal);

        Assert.Equal(dialogs, NoDialogs.Requested.Count);
    });

    [Fact]
    public void DeleteQuestion_GoesThroughTheDialogHelperAndNamesTheMember() => Run(() =>
    {
        var members = new ControlledMemberService { AnswerImmediately = true };
        members.Members.Add(new MemberSummary { Id = 7, FirstName = "Ayşe", LastName = "Yılmaz" });
        using var form = new UyeForm(members);
        Assert.True(form.ListMembersAsync().IsCompletedSuccessfully);
        form.SelectMember(members.Members[0]);
        var before = NoDialogs.Requested.Count;

        Assert.True(form.DeleteMemberAsync().IsCompletedSuccessfully);

        var question = Assert.Single(NoDialogs.Requested.Skip(before), request => request.Contains("Üye No 7", StringComparison.Ordinal));
        Assert.StartsWith("Onay: ", question, StringComparison.Ordinal);
        Assert.Contains("Ayşe Yılmaz", question, StringComparison.Ordinal);
        Assert.Empty(members.Deleted); // The fixture answers "No".
    });

    [Fact]
    public void CancelQuestion_NamesTheMemberAndTheBook() => Run(() =>
    {
        var reservations = new PendingReservationService();
        var prompts = new RecordedPrompts();
        using var form = new RezervasyonForm(null!, null!, reservations) { Prompts = prompts.Prompts };
        form.SelectReservation(new ReservationDetail { Id = 8, BookId = 3, BookTitle = "Kayıp Mektuplar", MemberName = "Ayşe Yılmaz" });

        _ = form.CancelReservationAsync();

        var question = Assert.Single(prompts.Questions);
        Assert.Contains("Ayşe Yılmaz", question, StringComparison.Ordinal);
        Assert.Contains("Kayıp Mektuplar", question, StringComparison.Ordinal);
        Assert.Equal(1, reservations.CancelCalls);
    });

    [Fact]
    public void Grid_SaysWhenItLoadsAndWhenNothingWasFound() => Run(() =>
    {
        var books = new ControlledBookService();
        using var form = new KitapForm(books);
        var grid = Find<ThemedGrid>(form, "dataGridView1");
        Find<TextBox>(form, "txtAra").Text = "yok";

        var list = form.ListBooksAsync();
        Assert.True(grid.IsLoading);

        books.Respond("yok");
        Assert.True(list.IsCompletedSuccessfully);
        Assert.False(grid.IsLoading);
        Assert.Equal(0, grid.Rows.Count);
        Assert.Equal("Aramanıza uyan kitap bulunamadı.", grid.EmptyText);
    });

    [Fact]
    public void Grid_HighlightsOnlyTheRowTheScreenActsOn() => Run(() =>
    {
        var members = new ControlledMemberService { AnswerImmediately = true };
        members.Members.Add(new MemberSummary { Id = 1, FirstName = "Ali", LastName = "Kaya" });
        members.Members.Add(new MemberSummary { Id = 2, FirstName = "Ayşe", LastName = "Yılmaz", UnpaidFine = 3m });
        using var form = new UyeForm(members);
        CreateHandles(form);
        Assert.True(form.ListMembersAsync().IsCompletedSuccessfully);
        var grid = Find<ThemedGrid>(form, "dataGridView1");
        Assert.Empty(grid.SelectedRows);

        form.SelectMember(members.Members[1]);
        Assert.Same(members.Members[1], Assert.Single(grid.SelectedRows.Cast<DataGridViewRow>()).DataBoundItem);
        Assert.Equal("Ayşe Yılmaz", Find<Label>(form, "lblSecimAd").Text);
        Assert.EndsWith(" TL", Find<Label>(form, "lblCeza").Text, StringComparison.Ordinal);

        Assert.True(form.SearchFromFirstPageAsync().IsCompletedSuccessfully);
        Assert.Empty(grid.SelectedRows);
        Assert.Equal("Üye seçilmedi", Find<Label>(form, "lblSecimAd").Text);
    });

    [Fact]
    public void MessageDialog_AnswersWithKeysAndIsNeverShown() => Run(() =>
    {
        using var dialog = new MessageDialog("Silinsin mi?", "Onay", MessageBoxButtons.YesNo, MessageBoxIcon.Question);

        Assert.False(dialog.Visible);
        Assert.Equal(DialogResult.Yes, Assert.IsType<ThemedButton>(dialog.AcceptButton).DialogResult);
        Assert.Equal(DialogResult.No, Assert.IsType<ThemedButton>(dialog.CancelButton).DialogResult);
        Assert.Equal(AppDialog.AppName, dialog.Text);
        Assert.True(dialog.ClientSize.Width > 0 && dialog.ClientSize.Height > 0);
    });

    private static Form Create(string name) => name switch
    {
        nameof(Form1) => new Form1(new UnusedFormFactory()),
        nameof(KitapForm) => new KitapForm(new ControlledBookService()),
        nameof(UyeForm) => new UyeForm(new ControlledMemberService()),
        nameof(OduncForm) => new OduncForm(new ControlledBookService(), new ControlledMemberService(), new PendingLoanService()),
        nameof(RezervasyonForm) => new RezervasyonForm(new ControlledBookService(), new ControlledMemberService(), new PendingReservationService()),
        nameof(RaporForm) => new RaporForm(new ControlledReportService(), null!),
        _ => throw new ArgumentOutOfRangeException(nameof(name), name, null),
    };

    /// <summary>
    /// Creates the window handles of a form that is never shown, so layout and tab order are real.
    /// The handles stay hidden: a control only becomes visible when its form is shown.
    /// </summary>
    private static void CreateHandles(Control control)
    {
        _ = control.Handle;
        foreach (Control child in control.Controls)
        {
            CreateHandles(child);
        }
    }

    private static IEnumerable<Control> Descendants(Control control)
    {
        foreach (Control child in control.Controls)
        {
            yield return child;
            foreach (var nested in Descendants(child))
            {
                yield return nested;
            }
        }
    }

    private static bool IsInsideParent(Control control)
    {
        const int Tolerance = 1;
        var area = control.Parent!.ClientRectangle;
        area.Inflate(Tolerance, Tolerance);
        return control.Width == 0 || control.Height == 0 || area.Contains(control.Bounds); // An empty label has no size.
    }

    /// <summary>Focusable controls in the order Tab visits them.</summary>
    private static List<Control> TabOrder(Form form)
    {
        var order = new List<Control>();
        Control? current = null;
        while ((current = form.GetNextControl(current, forward: true)) is not null)
        {
            if (current.CanSelectWhenHidden())
            {
                order.Add(current);
            }
        }

        return order;
    }

    private sealed class UnusedFormFactory : IFormFactory
    {
        public T Create<T>()
            where T : Form => throw new InvalidOperationException("No screen is opened in these tests.");
    }
}

internal static class SelectionExtensions
{
    /// <summary>
    /// Whether Tab can stop on the control once the window is shown: a tab stop that is enabled and
    /// not a container, label, frame or the inner parts of a number box.
    /// </summary>
    public static bool CanSelectWhenHidden(this Control control) =>
        control.TabStop
        && control.Enabled
        && control is not (Label or Panel or TableLayoutPanel or FlowLayoutPanel or GroupBox or UserControl or TextField or GlyphBadge or InfoBar)
        && !control.GetType().Name.Contains("UpDownEdit", StringComparison.Ordinal)
        && !control.GetType().Name.Contains("UpDownButtons", StringComparison.Ordinal)
        && control.Parent is not NumericUpDown;
}

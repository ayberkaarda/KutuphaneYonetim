using System.Globalization;
using KutuphaneYonetim.Domain.Entities;
using KutuphaneYonetim.UI;
using KutuphaneYonetim.UI.Infrastructure;
using KutuphaneYonetim.UI.Theming;

namespace KutuphaneYonetim.Screenshots;

/// <summary>The screens shown in the README, each prepared with synthetic data as a librarian would see it.</summary>
internal static class Screens
{
    /// <summary>File name (kebab-case, without extension), whether the window has a minimize box, and how to build it.</summary>
    public static IReadOnlyList<(string Name, bool MinimizeBox, Func<SyntheticLibrary, Form> Build)> All { get; } =
    [
        ("ana-sayfa", true, Home),
        ("kitaplar", true, Books),
        ("uyeler", true, Members),
        ("odunc-iade", true, Loans),
        ("rezervasyonlar", true, Reservations),
        ("raporlar", true, Reports),
        ("onay-penceresi", false, Confirmation),
    ];

    private static Form1 Home(SyntheticLibrary library) => new(new UnusedFormFactory());

    private static KitapForm Books(SyntheticLibrary library)
    {
        var form = new KitapForm(library.Books);
        Run(form.LoadAsync());
        return form;
    }

    private static UyeForm Members(SyntheticLibrary library)
    {
        var form = new UyeForm(library.Members);
        Run(form.LoadAsync());

        // The member with an unpaid fine is selected, as after a click on the row.
        var grid = Find<DataGridView>(form, "dataGridView1");
        var row = grid.Rows.Cast<DataGridViewRow>().First(r => r.DataBoundItem is MemberSummary { UnpaidFine: > 0, ActiveLoanCount: > 0 });
        form.SelectMember((MemberSummary)row.DataBoundItem!);
        return form;
    }

    private static OduncForm Loans(SyntheticLibrary library)
    {
        var form = new OduncForm(library.Books, library.Members, library.Loans);
        form.PrepareDueDate();
        Run(form.LoadAsync());

        // A late return with a waiting reservation: the result appears inline above the lists.
        var overdue = library.Loans.GetAllAsync().Result.First(l => l.IsActive && l.OverdueDays > 0 && l.OverdueDays < 5);
        form.SelectLoan(overdue);
        Run(form.ReturnAsync());
        return form;
    }

    private static RezervasyonForm Reservations(SyntheticLibrary library)
    {
        var form = new RezervasyonForm(library.Books, library.Members, library.Reservations);
        Run(form.LoadAsync());
        return form;
    }

    private static RaporForm Reports(SyntheticLibrary library)
    {
        var form = new RaporForm(library.Reports, library.Csv);
        form.InitializeFilters();
        Find<ComboBox>(form, "cmbRapor").SelectedIndex = 1; // En çok ödünç alınanlar.
        form.UpdateFilterControls();
        Run(form.ShowReportAsync());
        return form;
    }

    /// <summary>
    /// The question asked before a fine payment. The dialog sizes itself from its content, so its size
    /// is measured at 1x and kept for the 2x image, where the capture scales spacing and size together.
    /// </summary>
    private static MessageDialog Confirmation(SyntheticLibrary library)
    {
        var scale = Theme.RenderScale;
        Size logical;
        Theme.Use(Theme.Current, 1F);
        using (var probe = CreateConfirmation())
        {
            logical = probe.ClientSize;
        }

        Theme.Use(Theme.Current, scale);
        var dialog = CreateConfirmation();
        dialog.ClientSize = logical;
        return dialog;

        static MessageDialog CreateConfirmation() => new(
            string.Format(CultureInfo.CurrentCulture, "{0} (Üye No {1}) adlı üyenin tüm ödenmemiş cezaları ödendi olarak işaretlensin mi?", "Burak Şen", 2),
            "Onay",
            MessageBoxButtons.YesNo,
            MessageBoxIcon.Question);
    }

    /// <summary>The in-memory services complete synchronously; anything else is a bug in the tool.</summary>
    private static void Run(Task task)
    {
        if (!task.IsCompleted)
        {
            throw new InvalidOperationException("A screen did not finish loading synchronously.");
        }

        task.GetAwaiter().GetResult();
    }

    internal static T Find<T>(Control root, string name)
        where T : Control =>
        root.Controls.Find(name, searchAllChildren: true).OfType<T>().SingleOrDefault()
        ?? throw new InvalidOperationException(string.Format(CultureInfo.InvariantCulture, "Control '{0}' was not found.", name));

    /// <summary>The main menu never opens a screen while it is drawn.</summary>
    private sealed class UnusedFormFactory : IFormFactory
    {
        public T Create<T>()
            where T : Form => throw new InvalidOperationException("Screens are not opened while capturing.");
    }
}

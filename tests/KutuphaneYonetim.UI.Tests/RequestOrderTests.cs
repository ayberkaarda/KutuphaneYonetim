using KutuphaneYonetim.Business.Reports;
using KutuphaneYonetim.Domain.Entities;
using KutuphaneYonetim.UI.Tests.Support;
using static KutuphaneYonetim.UI.Tests.Support.StaThread;

namespace KutuphaneYonetim.UI.Tests;

/// <summary>Out-of-order responses: only the most recent request may reach the screen.</summary>
public sealed class RequestOrderTests
{
    [Fact]
    public void KitapForm_LatestRequestWins() => Run(() =>
    {
        var books = new ControlledBookService();
        using var form = new KitapForm(books);
        var search = Find<TextBox>(form, "txtAra");
        var grid = Find<DataGridView>(form, "dataGridView1");

        search.Text = "eski";
        var first = form.ListBooksAsync();
        search.Text = "yeni";
        var second = form.ListBooksAsync();

        var latest = books.Respond("yeni", new Book { Id = 2, Title = "Yeni" });
        books.Respond("eski", new Book { Id = 1, Title = "Eski" });

        Assert.True(first.IsCompletedSuccessfully && second.IsCompletedSuccessfully);
        Assert.Same(latest.Items, grid.DataSource);
    });

    [Fact]
    public void UyeForm_LatestRequestWins() => Run(() =>
    {
        var members = new ControlledMemberService();
        using var form = new UyeForm(members);
        var search = Find<TextBox>(form, "txtAra");
        var grid = Find<DataGridView>(form, "dataGridView1");

        search.Text = "Ali";
        var first = form.ListMembersAsync();
        search.Text = "Ayşe";
        var second = form.ListMembersAsync();

        var latest = members.Respond("Ayşe", new MemberSummary { Id = 2, FirstName = "Ayşe" });
        members.Respond("Ali", new MemberSummary { Id = 1, FirstName = "Ali" });

        Assert.True(first.IsCompletedSuccessfully && second.IsCompletedSuccessfully);
        Assert.Same(latest.Items, grid.DataSource);
    });

    [Fact]
    public void RaporForm_LatestRequestWins_TableAndKindComeFromTheSameRequest() => Run(() =>
    {
        var reports = new ControlledReportService();
        using var form = new RaporForm(reports, null!);
        form.InitializeFilters();
        var picker = Find<ComboBox>(form, "cmbRapor");

        picker.SelectedIndex = 0; // Overdue loans.
        var first = form.ShowReportAsync();
        picker.SelectedIndex = 2; // Unpaid fines.
        var second = form.ShowReportAsync();

        reports.RespondUnpaid(new LoanDetail { Id = 7, MemberName = "Ayşe", BookTitle = "Kitap", FineAmount = 3m });
        reports.RespondOverdue(new LoanDetail { Id = 9, MemberName = "Ali", BookTitle = "Kitap" });

        Assert.True(first.IsCompletedSuccessfully && second.IsCompletedSuccessfully);
        Assert.Equal(RaporForm.ReportKind.UnpaidFines, form.ShownKind);
        Assert.Equal(ReportTables.UnpaidFines([]).Headers, form.ShownTable!.Headers);
        Assert.Equal("7", Assert.Single(form.ShownTable.Rows)[0]);
    });

    [Fact]
    public void OduncForm_InitialLoadFinishesLast_KeepsPostLendList() => Run(() =>
    {
        var books = new ControlledBookService();
        books.Books.Add(new Book { Id = 1, Title = "Kitap", IsAvailable = true });
        var members = new ControlledMemberService();
        members.All.Add(new Member { Id = 4, FirstName = "Ayşe", LastName = "Yılmaz" });
        var loans = new ControlledLoanService();
        using var form = new OduncForm(books, members, loans) { Prompts = new RecordedPrompts().Prompts };
        var grid = Find<DataGridView>(form, "dataGridView1");

        // The pickers are filled; the first loan list is still on its way when the user lends.
        var load = form.LoadAsync();
        var lend = form.LendAsync();
        Assert.Equal([1], loans.Lent);

        var latest = loans.RespondList(1, new LoanDetail { Id = 9, BookId = 1, MemberId = 4 });
        loans.RespondList(0);

        Assert.True(load.IsCompletedSuccessfully && lend.IsCompletedSuccessfully);
        Assert.Same(latest, grid.DataSource);
    });

    [Fact]
    public void RezervasyonForm_InitialLoadFinishesLast_KeepsPostReserveList() => Run(() =>
    {
        var books = new ControlledBookService();
        books.Books.Add(new Book { Id = 3, Title = "Kitap", IsAvailable = false });
        var members = new ControlledMemberService();
        members.All.Add(new Member { Id = 4, FirstName = "Ayşe", LastName = "Yılmaz" });
        var reservations = new ControlledReservationService();
        var prompts = new RecordedPrompts();
        using var form = new RezervasyonForm(books, members, reservations) { Prompts = prompts.Prompts };
        var grid = Find<DataGridView>(form, "dataGridView1");

        // The pickers are filled; the first reservation list is still on its way when the user reserves.
        var load = form.LoadAsync();
        var reserve = form.ReserveAsync();
        Assert.Equal([3], reservations.Reserved);

        var created = new ReservationDetail { Id = 8, BookId = 3, MemberId = 4 };
        var latest = reservations.RespondList(1, created);
        form.SelectReservation(created);
        reservations.RespondList(0);

        Assert.True(load.IsCompletedSuccessfully && reserve.IsCompletedSuccessfully);
        Assert.Same(latest, grid.DataSource);

        // The stale response did not clear the selection made on the list that is shown.
        _ = form.CancelReservationAsync();
        Assert.Empty(prompts.Warnings);
        Assert.Single(prompts.Questions);
    });

    [Fact]
    public void OduncForm_EarlierPickerLoadFinishesLast_KeepsLatestPickers() => Run(() =>
    {
        var books = new ControlledBookService { ControlAvailable = true };
        using var form = new OduncForm(books, new ControlledMemberService(), new ControlledLoanService());

        var first = form.LoadAsync();
        var second = form.LoadAsync();

        var latest = books.RespondAvailable(1, new Book { Id = 2, Title = "Yeni" });
        books.RespondAvailable(0, new Book { Id = 1, Title = "Eski" });

        Assert.False(first.IsFaulted || second.IsFaulted);
        Assert.Same(latest, Find<ComboBox>(form, "cmbKitaplar").DataSource);
    });
}

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
}

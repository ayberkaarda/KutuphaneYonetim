using KutuphaneYonetim.Domain.Entities;
using KutuphaneYonetim.UI.Tests.Support;
using KutuphaneYonetim.UI.Theming;
using static KutuphaneYonetim.UI.Tests.Support.StaThread;

namespace KutuphaneYonetim.UI.Tests;

/// <summary>Payment and deletion act only on a member that is selected in the list currently shown.</summary>
public sealed class MemberSelectionTests
{
    private static ControlledMemberService ThreeMembers()
    {
        // Two members per page: Ali and Ayşe on page 1, Can on page 2.
        var members = new ControlledMemberService { AnswerImmediately = true };
        members.Members.Add(new MemberSummary { Id = 1, FirstName = "Ali", LastName = "Kaya", UnpaidFine = 1.5m });
        members.Members.Add(new MemberSummary { Id = 2, FirstName = "Ayşe", LastName = "Yılmaz", UnpaidFine = 3m });
        members.Members.Add(new MemberSummary { Id = 3, FirstName = "Can", LastName = "Demir", UnpaidFine = 4.5m });
        return members;
    }

    [Fact]
    public void SearchOrPaging_ClearsPaymentAndDeleteTarget() => Run(() =>
    {
        var members = ThreeMembers();
        var prompts = new RecordedPrompts();
        using var form = new UyeForm(members) { Prompts = prompts.Prompts };
        var search = Find<TextBox>(form, "txtAra");
        Assert.True(form.ListMembersAsync().IsCompletedSuccessfully);

        // Ali is selected, then a search leaves only Ayşe on screen: nobody may be paid for or deleted.
        form.SelectMember(members.Members[0]);
        search.Text = "Ayşe";
        Assert.True(form.SearchFromFirstPageAsync().IsCompletedSuccessfully);
        Assert.True(form.PayFinesAsync().IsCompletedSuccessfully);
        Assert.True(form.DeleteMemberAsync().IsCompletedSuccessfully);

        // Ali is selected again, then the next page is shown: same.
        search.Text = string.Empty;
        Assert.True(form.SearchFromFirstPageAsync().IsCompletedSuccessfully);
        form.SelectMember(members.Members[0]);
        Assert.True(form.MoveNextPageAsync().IsCompletedSuccessfully);
        Assert.True(form.PayFinesAsync().IsCompletedSuccessfully);

        Assert.Empty(members.Paid);
        Assert.Empty(members.Deleted);
        Assert.Empty(prompts.Questions);
        Assert.Equal(3, prompts.Warnings.Count);

        // A member selected on the page shown is the target, and the question names them.
        form.SelectMember(members.Members[2]);
        _ = form.PayFinesAsync();
        Assert.Equal([3], members.Paid);
        Assert.Contains("Can Demir", Assert.Single(prompts.Questions), StringComparison.Ordinal);
    });

    [Fact]
    public void Delete_ConfirmationNamesTheSelectedMember() => Run(() =>
    {
        var members = ThreeMembers();
        var prompts = new RecordedPrompts();
        using var form = new UyeForm(members) { Prompts = prompts.Prompts };
        Assert.True(form.ListMembersAsync().IsCompletedSuccessfully);

        form.SelectMember(members.Members[1]);
        _ = form.DeleteMemberAsync();

        Assert.Equal([2], members.Deleted);
        Assert.Contains("Ayşe Yılmaz", Assert.Single(prompts.Questions), StringComparison.Ordinal);
    });

    [Fact]
    public void FocusedRebind_DoesNotRestorePaymentOrDeleteTarget() => Run(() =>
    {
        var members = new ControlledMemberService();
        var prompts = new RecordedPrompts();
        using var form = new UyeForm(members) { Prompts = prompts.Prompts, IsListFocused = () => true };
        CreateHandles(form);
        var ali = new MemberSummary { Id = 1, FirstName = "Ali", LastName = "Kaya", UnpaidFine = 1.5m };
        var ayse = new MemberSummary { Id = 2, FirstName = "Ayşe", LastName = "Yılmaz", UnpaidFine = 3m };
        var grid = Find<ThemedGrid>(form, "dataGridView1");

        // Ali is selected, a new search starts and its answer arrives while the list has the focus.
        _ = form.ListMembersAsync();
        members.Respond(string.Empty, ali);
        form.SelectMember(ali);
        Find<TextBox>(form, "txtAra").Text = "A";
        var search = form.ListMembersAsync();
        members.Respond("A", ayse, ali);
        Assert.True(search.IsCompletedSuccessfully);

        Assert.Equal(2, grid.Rows.Count);
        Assert.Empty(grid.SelectedRows);
        Assert.Equal("Üye seçilmedi", Find<Label>(form, "lblSecimAd").Text);
        Assert.True(form.PayFinesAsync().IsCompletedSuccessfully);
        Assert.True(form.DeleteMemberAsync().IsCompletedSuccessfully);
        Assert.Empty(members.Paid);
        Assert.Empty(members.Deleted);
        Assert.Empty(prompts.Questions);

        // The user picking a row in the list afterwards still works.
        grid.CurrentCell = grid.Rows[1].Cells[0];
        Assert.Equal("Ali Kaya", Find<Label>(form, "lblSecimAd").Text);
    });

    private static void CreateHandles(Control control)
    {
        _ = control.Handle;
        foreach (Control child in control.Controls)
        {
            CreateHandles(child);
        }
    }
}

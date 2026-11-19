using KutuphaneYonetim.Domain.Entities;
using KutuphaneYonetim.UI.Infrastructure;
using KutuphaneYonetim.UI.Tests.Support;
using static KutuphaneYonetim.UI.Tests.Support.StaThread;

namespace KutuphaneYonetim.UI.Tests;

/// <summary>A second click on a write button while the first write is pending must not write twice.</summary>
public sealed class ExclusiveWriteTests
{
    [Fact]
    public void ExclusiveOperation_Failure_RestoresControlsAndAllowsNextRun() => Run(() =>
    {
        var operation = new ExclusiveOperation();
        using var enabled = new Button();
        using var disabled = new Button { Enabled = false };
        var pending = new TaskCompletionSource();

        var run = operation.RunAsync(() => pending.Task, enabled, disabled);
        Assert.True(operation.IsRunning);
        Assert.False(enabled.Enabled);
        Assert.False(operation.RunAsync(() => Task.CompletedTask).Result);

        pending.SetException(new InvalidOperationException("boom"));

        Assert.IsType<InvalidOperationException>(run.Exception?.InnerException);
        Assert.False(operation.IsRunning);
        Assert.True(enabled.Enabled);
        Assert.False(disabled.Enabled);
        Assert.True(operation.RunAsync(() => Task.CompletedTask).Result);
    });

    [Fact]
    public void KitapForm_AddWhilePending_AddsOnce() => Run(() =>
    {
        var books = new ControlledBookService();
        using var form = new KitapForm(books) { Prompts = new RecordedPrompts().Prompts };
        Find<TextBox>(form, "txtAd").Text = "Kitap";
        Find<TextBox>(form, "txtYazar").Text = "Yazar";

        var first = form.AddBookAsync();
        var second = form.AddBookAsync();

        Assert.Equal(1, books.AddCalls);
        Assert.False(first.IsCompleted);
        Assert.True(second.IsCompleted);
        Assert.False(Find<Button>(form, "btnEkle").Enabled);
        Assert.False(Find<Button>(form, "btnSil").Enabled);
    });

    [Fact]
    public void UyeForm_PayWhilePending_PaysOnce() => Run(() =>
    {
        var members = new ControlledMemberService { AnswerImmediately = true };
        members.Members.Add(new MemberSummary { Id = 3, FirstName = "Ayşe", LastName = "Yılmaz", UnpaidFine = 4.5m });
        using var form = new UyeForm(members) { Prompts = new RecordedPrompts().Prompts };
        Assert.True(form.ListMembersAsync().IsCompletedSuccessfully);
        form.SelectMember(members.Members[0]);

        var first = form.PayFinesAsync();
        var second = form.PayFinesAsync();
        var third = form.DeleteMemberAsync();

        Assert.Equal([3], members.Paid);
        Assert.Empty(members.Deleted);
        Assert.False(first.IsCompleted);
        Assert.True(second.IsCompleted && third.IsCompleted);
        Assert.False(Find<Button>(form, "btnCezaOde").Enabled);
    });

    [Fact]
    public void OduncForm_ReturnWhilePending_ReturnsOnce() => Run(() =>
    {
        var loans = new PendingLoanService();
        using var form = new OduncForm(null!, null!, loans) { Prompts = new RecordedPrompts().Prompts };
        form.SelectLoan(new LoanDetail { Id = 5, BookId = 3 });

        var first = form.ReturnAsync();
        var second = form.ReturnAsync();
        var third = form.LendAsync();

        Assert.Equal((0, 1), (loans.LendCalls, loans.ReturnCalls));
        Assert.False(first.IsCompleted);
        Assert.True(second.IsCompleted && third.IsCompleted);
        Assert.False(Find<Button>(form, "btnIadeEt").Enabled);
    });

    [Fact]
    public void RezervasyonForm_CancelWhilePending_CancelsOnce() => Run(() =>
    {
        var reservations = new PendingReservationService();
        using var form = new RezervasyonForm(null!, null!, reservations) { Prompts = new RecordedPrompts().Prompts };
        form.SelectReservation(new ReservationDetail { Id = 8, BookId = 3 });

        var first = form.CancelReservationAsync();
        var second = form.CancelReservationAsync();
        var third = form.ReserveAsync();

        Assert.Equal((0, 1), (reservations.ReserveCalls, reservations.CancelCalls));
        Assert.False(first.IsCompleted);
        Assert.True(second.IsCompleted && third.IsCompleted);
        Assert.False(Find<Button>(form, "btnIptal").Enabled);
    });
}

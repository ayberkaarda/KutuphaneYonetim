using KutuphaneYonetim.Business;
using KutuphaneYonetim.Business.Services;
using KutuphaneYonetim.Domain;

namespace KutuphaneYonetim.Data.Tests;

public sealed partial class ConcurrencyTests
{
    [Fact]
    public async Task PayFines_ConcurrentReturn_ReportsExactlyClosedAmount()
    {
        await using var db = await CreateDatabaseAsync();
        var loans = db.Get<ILoanService>();
        var members = db.Get<IMemberService>();

        // Member 3 owes 3.00 for book 4 and is two days late with book 5.
        await loans.LendAsync(4, 3, Start.Date, Ct);
        await loans.LendAsync(5, 3, Start.Date, Ct);
        _clock.AdvanceDays(2);
        Assert.Equal(3.00m, (await loans.ReturnAsync(4, Ct)).Fine);

        // The payment stops right before it marks fines as paid; book 5 comes back late meanwhile.
        var pause = WaitPoint.BeforeCommandContaining("UPDATE", "[ceza_odeme_tarihi]");
        var payment = pause.Run(() => members.PayFinesAsync(3, Ct));
        await pause.WaitUntilReachedAsync(payment);
        var returned = loans.ReturnAsync(5, Ct);
        await db.WaitUntilFinishedOrBlockedAsync(returned, Ct);
        pause.Release();

        var paid = await payment;
        Assert.Equal(3.00m, (await returned).Fine);

        // The reported amount is exactly what was marked paid; the rest is still owed.
        var fines = (await loans.GetAllAsync(Ct)).Where(l => l.MemberId == 3 && l.FineAmount > 0).ToList();
        Assert.Equal(fines.Where(l => l.FinePaidDate is not null).Sum(l => l.FineAmount!.Value), paid);
        Assert.Equal(
            fines.Where(l => l.FinePaidDate is null).Sum(l => l.FineAmount!.Value),
            (await members.SearchAsync(new MemberQuery(), Ct)).Items.Single(m => m.Id == 3).UnpaidFine);
    }

    [Fact]
    public async Task PayFines_ConcurrentCalls_OnlyOneReportsPayment()
    {
        await using var db = await CreateDatabaseAsync();
        var loans = db.Get<ILoanService>();
        var members = db.Get<IMemberService>();

        await loans.LendAsync(4, 3, Start.Date, Ct);
        _clock.AdvanceDays(2);
        Assert.Equal(3.00m, (await loans.ReturnAsync(4, Ct)).Fine);

        // Two clerks pay the same fines at the same time.
        var pause = WaitPoint.BeforeCommandContaining("UPDATE", "[ceza_odeme_tarihi]");
        var first = pause.Run(() => PaidOrRejectedAsync(members, 3));
        await pause.WaitUntilReachedAsync(first);
        var second = PaidOrRejectedAsync(members, 3);
        await db.WaitUntilFinishedOrBlockedAsync(second, Ct);
        pause.Release();

        var outcomes = await Task.WhenAll(first, second);
        Assert.Equal([3.00m], outcomes.Where(paid => paid is not null).Select(paid => paid!.Value));
        Assert.Single(outcomes, paid => paid is null);
    }

    private static async Task<decimal?> PaidOrRejectedAsync(IMemberService members, int memberId)
    {
        try
        {
            return await members.PayFinesAsync(memberId, Ct);
        }
        catch (BusinessRuleException)
        {
            return null;
        }
    }
}

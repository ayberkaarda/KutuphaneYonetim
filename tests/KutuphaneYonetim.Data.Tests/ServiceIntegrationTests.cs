using KutuphaneYonetim.Business;
using KutuphaneYonetim.Business.Services;

namespace KutuphaneYonetim.Data.Tests;

/// <summary>Business services on top of the real repositories, using SQLite in-memory.</summary>
public sealed class ServiceIntegrationTests : IAsyncLifetime
{
    private readonly SqliteDatabase _db = new();

    public ValueTask InitializeAsync() => _db.InitializeAsync();

    public ValueTask DisposeAsync() => _db.DisposeAsync();

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task LendReturnAndDelete_FollowTheBusinessRules()
    {
        var books = _db.Get<IBookService>();
        var members = _db.Get<IMemberService>();
        var loans = _db.Get<ILoanService>();

        var book = await books.AddAsync("  Deneme Kitabı ", " Deneme Yazar ", Ct);
        var member = await members.AddAsync(" Ali ", " Veli ", Ct);
        Assert.Equal("Deneme Kitabı", book.Title);

        await loans.LendAsync(book.Id, member.Id, Ct);
        await Assert.ThrowsAsync<BusinessRuleException>(() => loans.LendAsync(book.Id, 1, Ct));
        Assert.DoesNotContain(await books.GetAvailableAsync(Ct), b => b.Id == book.Id);

        var deleteBook = await Assert.ThrowsAsync<BusinessRuleException>(() => books.DeleteAsync(book.Id, Ct));
        Assert.Contains("ödünçte", deleteBook.Message, StringComparison.Ordinal);
        var deleteMember = await Assert.ThrowsAsync<BusinessRuleException>(() => members.DeleteAsync(member.Id, Ct));
        Assert.Contains("iade etmediği", deleteMember.Message, StringComparison.Ordinal);

        await loans.ReturnAsync(book.Id, Ct);
        await Assert.ThrowsAsync<BusinessRuleException>(() => loans.ReturnAsync(book.Id, Ct));

        // History remains, so deletion is still refused.
        var history = await Assert.ThrowsAsync<BusinessRuleException>(() => books.DeleteAsync(book.Id, Ct));
        Assert.Contains("geçmişi", history.Message, StringComparison.Ordinal);

        // Records without loans can be deleted.
        var spare = await books.AddAsync("Yedek", "Yazar", Ct);
        await books.DeleteAsync(spare.Id, Ct);
        Assert.DoesNotContain(await books.GetAllAsync(Ct), b => b.Id == spare.Id);
    }
}

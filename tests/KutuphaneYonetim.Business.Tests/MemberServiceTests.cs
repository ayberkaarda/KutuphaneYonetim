using KutuphaneYonetim.Business.Services;
using KutuphaneYonetim.Business.Validation;
using KutuphaneYonetim.Domain;
using KutuphaneYonetim.Domain.Entities;
using KutuphaneYonetim.Domain.Repositories;
using Moq;

namespace KutuphaneYonetim.Business.Tests;

public sealed class MemberServiceTests
{
    private readonly Mock<IMemberRepository> _members = new(MockBehavior.Strict);
    private readonly Mock<ILoanRepository> _loans = new(MockBehavior.Strict);
    private readonly Mock<IReservationRepository> _reservations = new(MockBehavior.Strict);
    private static readonly DateTimeOffset Now = new(2026, 3, 14, 9, 30, 0, TimeSpan.Zero);

    private readonly MemberService _service;

    public MemberServiceTests()
    {
        _service = new MemberService(_members.Object, _loans.Object, _reservations.Object, new MemberInputValidator(), new FixedTimeProvider(Now));
    }

    [Fact]
    public async Task AddAsync_ValidInput_TrimsAndStoresMember()
    {
        Member? stored = null;
        _members.Setup(m => m.AddAsync(It.IsAny<Member>(), It.IsAny<CancellationToken>()))
            .Callback<Member, CancellationToken>((member, _) => stored = member)
            .ReturnsAsync(5);

        var result = await _service.AddAsync(" Ayşe ", "Yılmaz  ", TestContext.Current.CancellationToken);

        Assert.Equal(5, result.Id);
        Assert.NotNull(stored);
        Assert.Equal("Ayşe", stored.FirstName);
        Assert.Equal("Yılmaz", stored.LastName);
    }

    [Theory]
    [InlineData("", "Soyad", "Ad alanı boş bırakılamaz.")]
    [InlineData("  ", "Soyad", "Ad alanı boş bırakılamaz.")]
    [InlineData("Ad", "", "Soyad alanı boş bırakılamaz.")]
    [InlineData("Ad", null, "Soyad alanı boş bırakılamaz.")]
    public async Task AddAsync_MissingField_IsRejectedWithoutTouchingTheDatabase(
        string firstName, string? lastName, string expectedMessage)
    {
        var error = await Assert.ThrowsAsync<BusinessRuleException>(
            () => _service.AddAsync(firstName, lastName!, TestContext.Current.CancellationToken));

        Assert.Equal(expectedMessage, error.Message);
        _members.VerifyNoOtherCalls();
    }

    [Theory]
    [InlineData(51, 1, "Ad en fazla 50 karakter olabilir.")]
    [InlineData(1, 51, "Soyad en fazla 50 karakter olabilir.")]
    public async Task AddAsync_NameLongerThanLimit_IsRejected(int firstLength, int lastLength, string expectedMessage)
    {
        var error = await Assert.ThrowsAsync<BusinessRuleException>(() => _service.AddAsync(
            new string('a', firstLength), new string('b', lastLength), TestContext.Current.CancellationToken));

        Assert.Equal(expectedMessage, error.Message);
    }

    [Fact]
    public async Task DeleteAsync_UnknownMember_IsRejected()
    {
        _members.Setup(m => m.GetByIdAsync(3, It.IsAny<CancellationToken>())).ReturnsAsync((Member?)null);

        var error = await Assert.ThrowsAsync<BusinessRuleException>(
            () => _service.DeleteAsync(3, TestContext.Current.CancellationToken));

        Assert.Equal("Seçilen üye bulunamadı.", error.Message);
    }

    [Fact]
    public async Task DeleteAsync_MemberWithOpenLoan_IsRejected()
    {
        _members.Setup(m => m.GetByIdAsync(3, It.IsAny<CancellationToken>())).ReturnsAsync(new Member { Id = 3 });
        _loans.Setup(l => l.HasActiveLoanForMemberAsync(3, It.IsAny<CancellationToken>())).ReturnsAsync(true);

        var error = await Assert.ThrowsAsync<BusinessRuleException>(
            () => _service.DeleteAsync(3, TestContext.Current.CancellationToken));

        Assert.Equal("Bu üyenin iade etmediği kitap bulunduğu için silinemez.", error.Message);
        _members.Verify(m => m.DeleteAsync(It.IsAny<int>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task DeleteAsync_MemberWithLoanHistory_IsRejected()
    {
        _members.Setup(m => m.GetByIdAsync(3, It.IsAny<CancellationToken>())).ReturnsAsync(new Member { Id = 3 });
        _loans.Setup(l => l.HasActiveLoanForMemberAsync(3, It.IsAny<CancellationToken>())).ReturnsAsync(false);
        _loans.Setup(l => l.HasAnyLoanForMemberAsync(3, It.IsAny<CancellationToken>())).ReturnsAsync(true);

        var error = await Assert.ThrowsAsync<BusinessRuleException>(
            () => _service.DeleteAsync(3, TestContext.Current.CancellationToken));

        Assert.Equal("Bu üyenin ödünç geçmişi bulunduğu için silinemez.", error.Message);
        _members.Verify(m => m.DeleteAsync(It.IsAny<int>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task DeleteAsync_MemberWithReservationHistory_IsRejected()
    {
        _members.Setup(m => m.GetByIdAsync(3, It.IsAny<CancellationToken>())).ReturnsAsync(new Member { Id = 3 });
        _loans.Setup(l => l.HasActiveLoanForMemberAsync(3, It.IsAny<CancellationToken>())).ReturnsAsync(false);
        _loans.Setup(l => l.HasAnyLoanForMemberAsync(3, It.IsAny<CancellationToken>())).ReturnsAsync(false);
        _reservations.Setup(r => r.HasAnyForMemberAsync(3, It.IsAny<CancellationToken>())).ReturnsAsync(true);

        var error = await Assert.ThrowsAsync<BusinessRuleException>(
            () => _service.DeleteAsync(3, TestContext.Current.CancellationToken));

        Assert.Equal("Bu üyenin rezervasyon kaydı bulunduğu için silinemez.", error.Message);
        _members.Verify(m => m.DeleteAsync(It.IsAny<int>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task DeleteAsync_MemberWithoutLoans_IsDeleted()
    {
        _members.Setup(m => m.GetByIdAsync(3, It.IsAny<CancellationToken>())).ReturnsAsync(new Member { Id = 3 });
        _loans.Setup(l => l.HasActiveLoanForMemberAsync(3, It.IsAny<CancellationToken>())).ReturnsAsync(false);
        _loans.Setup(l => l.HasAnyLoanForMemberAsync(3, It.IsAny<CancellationToken>())).ReturnsAsync(false);
        _reservations.Setup(r => r.HasAnyForMemberAsync(3, It.IsAny<CancellationToken>())).ReturnsAsync(false);
        _members.Setup(m => m.DeleteAsync(3, It.IsAny<CancellationToken>())).ReturnsAsync(true);

        await _service.DeleteAsync(3, TestContext.Current.CancellationToken);

        _members.Verify(m => m.DeleteAsync(3, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task PayFinesAsync_UnpaidFines_ReturnsTheAmountTheRepositoryClosed()
    {
        _members.Setup(m => m.GetByIdAsync(3, It.IsAny<CancellationToken>())).ReturnsAsync(new Member { Id = 3 });
        _loans.Setup(l => l.PayFinesAsync(3, Now.UtcDateTime, It.IsAny<CancellationToken>())).ReturnsAsync(12.50m);

        var paid = await _service.PayFinesAsync(3, TestContext.Current.CancellationToken);

        Assert.Equal(12.50m, paid);
        _loans.Verify(l => l.PayFinesAsync(3, Now.UtcDateTime, It.IsAny<CancellationToken>()), Times.Once);
        _loans.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task PayFinesAsync_NothingUnpaid_IsRejected()
    {
        _members.Setup(m => m.GetByIdAsync(3, It.IsAny<CancellationToken>())).ReturnsAsync(new Member { Id = 3 });
        _loans.Setup(l => l.PayFinesAsync(3, Now.UtcDateTime, It.IsAny<CancellationToken>())).ReturnsAsync(0m);

        var error = await Assert.ThrowsAsync<BusinessRuleException>(
            () => _service.PayFinesAsync(3, TestContext.Current.CancellationToken));

        Assert.Equal("Bu üyenin ödenmemiş cezası bulunmuyor.", error.Message);
    }

    [Fact]
    public async Task PayFinesAsync_UnknownMember_IsRejected()
    {
        _members.Setup(m => m.GetByIdAsync(3, It.IsAny<CancellationToken>())).ReturnsAsync((Member?)null);

        var error = await Assert.ThrowsAsync<BusinessRuleException>(
            () => _service.PayFinesAsync(3, TestContext.Current.CancellationToken));

        Assert.Equal("Seçilen üye bulunamadı.", error.Message);
        _loans.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task SearchAsync_NormalisesTextAndPaging()
    {
        MemberQuery? sent = null;
        _members.Setup(m => m.SearchAsync(It.IsAny<MemberQuery>(), It.IsAny<CancellationToken>()))
            .Callback<MemberQuery, CancellationToken>((q, _) => sent = q)
            .ReturnsAsync(new PagedResult<MemberSummary>([], 0, 1, 1));

        await _service.SearchAsync(
            new MemberQuery("  Ayşe Y ", MemberFilter.WithUnpaidFine, Page: -3, PageSize: 0), TestContext.Current.CancellationToken);

        Assert.Equal(new MemberQuery("Ayşe Y", MemberFilter.WithUnpaidFine, 1, 1), sent);
    }

    [Fact]
    public async Task SearchAsync_PageBeyondTheEnd_ReturnsLastPage()
    {
        _members.Setup(m => m.SearchAsync(It.Is<MemberQuery>(q => q.Page == 4), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new PagedResult<MemberSummary>([], 3, 4, 2));
        _members.Setup(m => m.SearchAsync(It.Is<MemberQuery>(q => q.Page == 2), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new PagedResult<MemberSummary>([new MemberSummary { Id = 3 }], 3, 2, 2));

        var result = await _service.SearchAsync(new MemberQuery(Page: 4, PageSize: 2), TestContext.Current.CancellationToken);

        Assert.Equal(2, result.Page);
        Assert.Equal(3, Assert.Single(result.Items).Id);
    }
}

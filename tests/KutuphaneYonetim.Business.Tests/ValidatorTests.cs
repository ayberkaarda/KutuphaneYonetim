using KutuphaneYonetim.Business.Services;
using KutuphaneYonetim.Business.Validation;
using Microsoft.Extensions.DependencyInjection;

namespace KutuphaneYonetim.Business.Tests;

public sealed class ValidatorTests
{
    [Theory]
    [InlineData(1, true)]
    [InlineData(100, true)]
    [InlineData(101, false)]
    public void BookInputValidator_TitleLengthBoundary(int length, bool expectedValid)
    {
        var result = new BookInputValidator().Validate(BookInput.Create(new string('x', length), "Yazar"));

        Assert.Equal(expectedValid, result.IsValid);
    }

    [Theory]
    [InlineData(50, true)]
    [InlineData(51, false)]
    public void MemberInputValidator_NameLengthBoundary(int length, bool expectedValid)
    {
        var result = new MemberInputValidator().Validate(MemberInput.Create(new string('x', length), new string('y', length)));

        Assert.Equal(expectedValid, result.IsValid);
    }

    [Fact]
    public void BookInputValidator_StopsAtFirstFailure()
    {
        var result = new BookInputValidator().Validate(BookInput.Create(" ", " "));

        var error = Assert.Single(result.Errors);
        Assert.Equal("Kitap adı alanı boş bırakılamaz.", error.ErrorMessage);
    }

    [Fact]
    public void Inputs_AreTrimmedAndNullBecomesEmpty()
    {
        Assert.Equal(new BookInput("a", string.Empty), BookInput.Create("  a  ", null));
        Assert.Equal(new MemberInput(string.Empty, "b"), MemberInput.Create(null, " b"));
    }

    [Fact]
    public void LoanInputValidator_AcceptsPositiveIds()
    {
        var now = new DateTime(2026, 1, 1, 12, 0, 0, DateTimeKind.Unspecified);
        Assert.True(new LoanInputValidator().Validate(new LoanInput(1, 1, now, now.Date.AddDays(14))).IsValid);
    }

    [Theory]
    [InlineData(-1, false)]
    [InlineData(0, true)] // Due the same day.
    [InlineData(365, true)]
    [InlineData(366, false)]
    public void LoanInputValidator_DueDateBoundaries(int days, bool expectedValid)
    {
        var now = new DateTime(2026, 1, 1, 18, 45, 0, DateTimeKind.Unspecified);

        var result = new LoanInputValidator().Validate(new LoanInput(1, 1, now, now.Date.AddDays(days)));

        Assert.Equal(expectedValid, result.IsValid);
    }

    [Fact]
    public void LibraryPolicy_Defaults_AreValid()
    {
        var policy = new LibraryPolicy();

        Assert.Empty(policy.Validate());
        Assert.Equal(14, policy.LoanPeriodDays);
        Assert.Equal(0m, policy.MaxUnpaidFine);
    }

    [Theory]
    [InlineData(0, 1, 0, "LoanPeriodDays 1 ile 365 arasında olmalıdır.")]
    [InlineData(366, 1, 0, "LoanPeriodDays 1 ile 365 arasında olmalıdır.")]
    [InlineData(14, -0.01, 0, "DailyFine 0 ile 10000 arasında olmalıdır.")]
    [InlineData(14, 1, -1, "MaxUnpaidFine negatif olamaz.")]
    [InlineData(14, 1, 0, "ReservationHoldDays 1 ile 30 arasında olmalıdır.", 0)]
    [InlineData(14, 1, 0, "ReservationHoldDays 1 ile 30 arasında olmalıdır.", 31)]
    public void LibraryPolicy_InvalidValues_AreReported(int period, double daily, double maxUnpaid, string expected, int holdDays = 3)
    {
        var policy = new LibraryPolicy
        {
            LoanPeriodDays = period,
            DailyFine = (decimal)daily,
            MaxUnpaidFine = (decimal)maxUnpaid,
            ReservationHoldDays = holdDays,
        };

        Assert.Equal(expected, Assert.Single(policy.Validate()));
    }

    [Fact]
    public void AddBusinessServices_InvalidPolicy_IsRefused()
    {
        var services = new ServiceCollection();

        Assert.Throws<ArgumentException>(() => services.AddBusinessServices(new LibraryPolicy { LoanPeriodDays = 0 }));
    }

    [Fact]
    public void AddBusinessServices_ResolvesServicesWithValidators()
    {
        var services = new ServiceCollection();
        services.AddSingleton(new Moq.Mock<Domain.Repositories.IBookRepository>().Object);
        services.AddSingleton(new Moq.Mock<Domain.Repositories.IMemberRepository>().Object);
        services.AddSingleton(new Moq.Mock<Domain.Repositories.ILoanRepository>().Object);
        services.AddSingleton(new Moq.Mock<Domain.Repositories.IReservationRepository>().Object);
        services.AddBusinessServices();

        using var provider = services.BuildServiceProvider(new ServiceProviderOptions { ValidateOnBuild = true });

        Assert.NotNull(provider.GetRequiredService<IBookService>());
        Assert.NotNull(provider.GetRequiredService<IMemberService>());
        Assert.NotNull(provider.GetRequiredService<ILoanService>());
        Assert.NotNull(provider.GetRequiredService<IReservationService>());
    }

    [Theory]
    [InlineData("9789750719387", true)]
    [InlineData("9786053600008", true)]
    [InlineData("0306406152", true)]
    [InlineData("080442957X", true)]
    [InlineData("9789750719388", false)]
    [InlineData("0306406151", false)]
    [InlineData("03064061X2", false)] // X is only allowed as the ISBN-10 check digit.
    [InlineData("123", false)]
    public void IsbnFormat_ValidatesCheckDigit(string isbn, bool expectedValid)
    {
        Assert.Equal(expectedValid, IsbnFormat.IsValid(isbn));
    }

    [Fact]
    public void PagedResult_PageCount_RoundsUpAndIsAtLeastOne()
    {
        Assert.Equal(1, new Domain.PagedResult<int>([], 0, 1, 50).PageCount);
        Assert.Equal(1, new Domain.PagedResult<int>([], 50, 1, 50).PageCount);
        Assert.Equal(2, new Domain.PagedResult<int>([], 51, 1, 50).PageCount);
    }
}

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
        Assert.True(new LoanInputValidator().Validate(new LoanInput(1, 1)).IsValid);
    }

    [Fact]
    public void AddBusinessServices_ResolvesServicesWithValidators()
    {
        var services = new ServiceCollection();
        services.AddSingleton(new Moq.Mock<Domain.Repositories.IBookRepository>().Object);
        services.AddSingleton(new Moq.Mock<Domain.Repositories.IMemberRepository>().Object);
        services.AddSingleton(new Moq.Mock<Domain.Repositories.ILoanRepository>().Object);
        services.AddBusinessServices();

        using var provider = services.BuildServiceProvider(new ServiceProviderOptions { ValidateOnBuild = true });

        Assert.NotNull(provider.GetRequiredService<IBookService>());
        Assert.NotNull(provider.GetRequiredService<IMemberService>());
        Assert.NotNull(provider.GetRequiredService<ILoanService>());
    }
}

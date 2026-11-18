using FluentValidation;
using KutuphaneYonetim.Business.Services;
using KutuphaneYonetim.Business.Validation;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace KutuphaneYonetim.Business;

public static class BusinessServiceCollectionExtensions
{
    /// <summary>Registers the business services. Repositories must be registered separately.</summary>
    public static IServiceCollection AddBusinessServices(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.TryAddSingleton(TimeProvider.System);
        services.AddSingleton<IValidator<BookInput>, BookInputValidator>();
        services.AddSingleton<IValidator<MemberInput>, MemberInputValidator>();
        services.AddSingleton<IValidator<LoanInput>, LoanInputValidator>();
        services.AddSingleton<IBookService, BookService>();
        services.AddSingleton<IMemberService, MemberService>();
        services.AddSingleton<ILoanService, LoanService>();
        return services;
    }
}
